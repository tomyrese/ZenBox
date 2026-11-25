using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using ZenBox.Models;

namespace ZenBox.Services;

public sealed class RemoteControlServer : IDisposable
{
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class RemoteClientSession : IDisposable
    {
        public required string SessionId { get; init; }
        public required WebSocket Socket { get; init; }
        public required string RemoteAddress { get; init; }
        public string DeviceName { get; set; } = "Điện thoại";
        public SemaphoreSlim SendLock { get; } = new(1, 1);

        public void Dispose()
        {
            SendLock.Dispose();
            try
            {
                Socket.Dispose();
            }
            catch
            {
            }
        }
    }

    private readonly ErrorHandlingService _errorService;
    private readonly Action<RemoteCommandPayload> _commandDispatcher;
    private readonly Func<RemoteStateDto>? _stateSnapshotProvider;
    private readonly ConcurrentDictionary<string, RemoteClientSession> _sessions = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public int Port { get; }
    public bool IsRunning { get; private set; }
    public int ConnectedClientCount => _sessions.Count;

    public event EventHandler<int>? ConnectedClientsChanged;

    public RemoteControlServer(
        int port,
        ErrorHandlingService errorService,
        Action<RemoteCommandPayload> commandDispatcher,
        Func<RemoteStateDto>? stateSnapshotProvider = null)
    {
        Port = port;
        _errorService = errorService;
        _commandDispatcher = commandDispatcher;
        _stateSnapshotProvider = stateSnapshotProvider;
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();
            IsRunning = true;

            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            IsRunning = false;
            _errorService.LogError(ErrorCategory.UnexpectedSystemError, $"Start RemoteControlServer on port {Port}", ex);
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(token);
                tcpClient.NoDelay = true;
                _ = Task.Run(() => HandleIncomingTcpAsync(tcpClient, token), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    _errorService.LogError(ErrorCategory.UnexpectedSystemError, "AcceptTcpClientAsync", ex);
                }
            }
        }
    }

    private async Task HandleIncomingTcpAsync(TcpClient tcpClient, CancellationToken token)
    {
        var remoteIp = (tcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "unknown";

        try
        {
            var stream = tcpClient.GetStream();
            var headerBuffer = new byte[8192];
            int totalRead = 0;
            int headerEndIndex = -1;

            using var headerTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            headerTimeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            while (totalRead < headerBuffer.Length)
            {
                int read = await stream.ReadAsync(
                    headerBuffer.AsMemory(totalRead, headerBuffer.Length - totalRead),
                    headerTimeoutCts.Token);

                if (read <= 0)
                {
                    tcpClient.Dispose();
                    return;
                }

                totalRead += read;
                headerEndIndex = FindHeaderDelimiter(headerBuffer, totalRead);
                if (headerEndIndex >= 0)
                {
                    break;
                }
            }

            if (headerEndIndex < 0)
            {
                tcpClient.Dispose();
                return;
            }

            var headerText = Encoding.ASCII.GetString(headerBuffer, 0, headerEndIndex);
            var lines = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                tcpClient.Dispose();
                return;
            }

            var requestParts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var method = requestParts.Length > 0 ? requestParts[0].ToUpperInvariant() : "GET";
            var path = requestParts.Length > 1 ? requestParts[1] : "/";

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                var colon = lines[i].IndexOf(':');
                if (colon > 0)
                {
                    headers[lines[i][..colon].Trim()] = lines[i][(colon + 1)..].Trim();
                }
            }

            // 1. Nâng cấp lên kết nối WebSocket thời gian thực cho nhiều điện thoại cùng lúc
            if (headers.TryGetValue("Upgrade", out var upgradeVal) &&
                upgradeVal.Contains("websocket", StringComparison.OrdinalIgnoreCase) &&
                headers.TryGetValue("Sec-WebSocket-Key", out var wsKey) &&
                !string.IsNullOrWhiteSpace(wsKey))
            {
                var acceptKey = ComputeWebSocketAcceptKey(wsKey);
                var handshakeResponse =
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

                var handshakeBytes = Encoding.ASCII.GetBytes(handshakeResponse);
                await stream.WriteAsync(handshakeBytes, token);
                await stream.FlushAsync(token);

                var ws = WebSocket.CreateFromStream(
                    stream,
                    isServer: true,
                    subProtocol: null,
                    keepAliveInterval: TimeSpan.FromSeconds(20));

                await RunWebSocketSessionAsync(tcpClient, ws, remoteIp, token);
                return;
            }

            // 2. Xử lý CORS Preflight cho ứng dụng di động / trình duyệt
            if (method == "OPTIONS")
            {
                await WriteHttpResponseAsync(stream, 204, "No Content", "text/plain", string.Empty, token);
                tcpClient.Dispose();
                return;
            }

            // 3. Xử lý gửi lệnh dự phòng qua HTTP POST /zenbox-cmd
            if (method == "POST" && path.StartsWith("/zenbox-cmd", StringComparison.OrdinalIgnoreCase))
            {
                int contentLength = 0;
                if (headers.TryGetValue("Content-Length", out var lenStr))
                {
                    _ = int.TryParse(lenStr, out contentLength);
                }

                int bodyStartOffset = headerEndIndex + 4;
                int alreadyReadBodyBytes = Math.Max(0, totalRead - bodyStartOffset);
                var bodyBytes = new byte[Math.Max(contentLength, alreadyReadBodyBytes)];

                if (alreadyReadBodyBytes > 0)
                {
                    Buffer.BlockCopy(headerBuffer, bodyStartOffset, bodyBytes, 0, alreadyReadBodyBytes);
                }

                int bodyOffset = alreadyReadBodyBytes;
                while (bodyOffset < contentLength)
                {
                    int r = await stream.ReadAsync(
                        bodyBytes.AsMemory(bodyOffset, contentLength - bodyOffset),
                        headerTimeoutCts.Token);
                    if (r <= 0) break;
                    bodyOffset += r;
                }

                var rawBody = Encoding.UTF8.GetString(bodyBytes, 0, Math.Min(bodyBytes.Length, Math.Max(contentLength, bodyOffset)));
                await DispatchRawCommandAsync(null, rawBody);

                var latestState = await GetSnapshotSafeAsync();
                var respJson = JsonSerializer.Serialize(latestState, JsonOptions);
                await WriteHttpResponseAsync(stream, 200, "OK", "application/json; charset=utf-8", respJson, token);
                tcpClient.Dispose();
                return;
            }

            // 4. Trả về thông tin tự động dò tìm thiết bị ZenBox trên mạng Wi-Fi (GET /zenbox-info hoặc GET /)
            var stateSnapshot = await GetSnapshotSafeAsync();
            var allIps = NetworkAddressHelper.GetRealLanIpAddresses();
            var info = new RemoteDiscoveryInfoDto
            {
                App = "ZenBox",
                ServerName = Environment.MachineName,
                Ip = allIps.FirstOrDefault() ?? "127.0.0.1",
                AllIps = allIps,
                Port = Port,
                ConnectedClients = ConnectedClientCount,
                State = stateSnapshot
            };

            var infoJson = JsonSerializer.Serialize(info, JsonOptions);
            await WriteHttpResponseAsync(stream, 200, "OK", "application/json; charset=utf-8", infoJson, token);
            tcpClient.Dispose();
        }
        catch
        {
            try
            {
                tcpClient.Dispose();
            }
            catch
            {
            }
        }
    }

    private async Task RunWebSocketSessionAsync(
        TcpClient tcpClient,
        WebSocket webSocket,
        string remoteIp,
        CancellationToken token)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var session = new RemoteClientSession
        {
            SessionId = sessionId,
            Socket = webSocket,
            RemoteAddress = remoteIp
        };

        _sessions[sessionId] = session;
        NotifyClientsCountChanged();
        BroadcastState();

        var buffer = new byte[4096];
        using var messageStream = new MemoryStream();

        try
        {
            while (!token.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                messageStream.SetLength(0);
                ValueWebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(buffer.AsMemory(), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        try
                        {
                            await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        }
                        catch
                        {
                        }
                        return;
                    }

                    if (result.Count > 0)
                    {
                        messageStream.Write(buffer, 0, result.Count);
                    }
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text && messageStream.Length > 0)
                {
                    var rawJson = Encoding.UTF8.GetString(messageStream.GetBuffer(), 0, (int)messageStream.Length);
                    await DispatchRawCommandAsync(session, rawJson);
                }
            }
        }
        catch
        {
            // Điện thoại ngắt kết nối hoặc tắt màn hình
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
            session.Dispose();
            try
            {
                tcpClient.Dispose();
            }
            catch
            {
            }

            NotifyClientsCountChanged();
            BroadcastState();
        }
    }

    private async Task DispatchRawCommandAsync(RemoteClientSession? senderSession, string rawJson)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<RemoteCommandPayload>(rawJson, JsonOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Action))
            {
                return;
            }

            if (senderSession is not null && !string.IsNullOrWhiteSpace(payload.DeviceName))
            {
                senderSession.DeviceName = payload.DeviceName;
            }

            if (string.Equals(payload.Action, "PING", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(payload.Action, "GET_STATE", StringComparison.OrdinalIgnoreCase))
            {
                if (senderSession is not null)
                {
                    var state = await GetSnapshotSafeAsync();
                    var json = JsonSerializer.Serialize(state, JsonOptions);
                    await SendToSessionSafeAsync(senderSession, Encoding.UTF8.GetBytes(json));
                }
                return;
            }

            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(() => _commandDispatcher(payload));
            }
            else
            {
                _commandDispatcher(payload);
            }

            BroadcastState();
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.UnexpectedSystemError, $"Dispatch remote message: {rawJson}", ex);
        }
    }

    public void BroadcastState()
    {
        if (!IsRunning || _sessions.IsEmpty || _stateSnapshotProvider is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var state = await GetSnapshotSafeAsync();
                if (state is null)
                {
                    return;
                }

                var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state, JsonOptions));
                var tasks = _sessions.Values.Select(s => SendToSessionSafeAsync(s, payloadBytes));
                await Task.WhenAll(tasks);
            }
            catch
            {
            }
        });
    }

    private async Task<RemoteStateDto?> GetSnapshotSafeAsync()
    {
        if (_stateSnapshotProvider is null)
        {
            return null;
        }

        RemoteStateDto dto;
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dto = await dispatcher.InvokeAsync(_stateSnapshotProvider);
        }
        else
        {
            dto = _stateSnapshotProvider();
        }

        dto.ConnectedClients = ConnectedClientCount;
        return dto;
    }

    private static async Task SendToSessionSafeAsync(RemoteClientSession session, byte[] utf8Bytes)
    {
        if (session.Socket.State != WebSocketState.Open)
        {
            return;
        }

        await session.SendLock.WaitAsync();
        try
        {
            if (session.Socket.State == WebSocketState.Open)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await session.Socket.SendAsync(
                    utf8Bytes.AsMemory(),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cts.Token);
            }
        }
        catch
        {
        }
        finally
        {
            session.SendLock.Release();
        }
    }

    private void NotifyClientsCountChanged()
    {
        try
        {
            ConnectedClientsChanged?.Invoke(this, ConnectedClientCount);
        }
        catch
        {
        }
    }

    private static async Task WriteHttpResponseAsync(
        NetworkStream stream,
        int statusCode,
        string statusText,
        string contentType,
        string body,
        CancellationToken token)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header =
            $"HTTP/1.1 {statusCode} {statusText}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
            "Access-Control-Allow-Headers: Content-Type\r\n" +
            "Connection: close\r\n\r\n";

        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, token);
        if (bodyBytes.Length > 0)
        {
            await stream.WriteAsync(bodyBytes, token);
        }
        await stream.FlushAsync(token);
    }

    private static string ComputeWebSocketAcceptKey(string secWebSocketKey)
    {
        var combined = secWebSocketKey.Trim() + WebSocketGuid;
        var hash = SHA1.HashData(Encoding.ASCII.GetBytes(combined));
        return Convert.ToBase64String(hash);
    }

    private static int FindHeaderDelimiter(byte[] buffer, int length)
    {
        for (int i = 0; i <= length - 4; i++)
        {
            if (buffer[i] == '\r' &&
                buffer[i + 1] == '\n' &&
                buffer[i + 2] == '\r' &&
                buffer[i + 3] == '\n')
            {
                return i;
            }
        }
        return -1;
    }

    public void Dispose()
    {
        IsRunning = false;
        try
        {
            _cts?.Cancel();
        }
        catch
        {
        }

        try
        {
            _listener?.Stop();
        }
        catch
        {
        }

        foreach (var kv in _sessions.ToArray())
        {
            if (_sessions.TryRemove(kv.Key, out var s))
            {
                s.Dispose();
            }
        }

        _cts?.Dispose();
    }
}
