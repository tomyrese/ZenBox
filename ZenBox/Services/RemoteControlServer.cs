using System.Text.Json;
using System.Windows;
using Fleck;
using ZenBox.Models;

namespace ZenBox.Services;

public sealed class RemoteControlServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ErrorHandlingService _errorService;
    private readonly Action<RemoteCommandPayload> _commandDispatcher;
    private readonly List<IWebSocketConnection> _clients = new();
    private readonly object _clientsLock = new();
    private WebSocketServer? _server;

    public int Port { get; }
    public bool IsRunning { get; private set; }

    public RemoteControlServer(
        int port,
        ErrorHandlingService errorService,
        Action<RemoteCommandPayload> commandDispatcher)
    {
        Port = port;
        _errorService = errorService;
        _commandDispatcher = commandDispatcher;
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        try
        {
            FleckLog.Level = LogLevel.Warn;
            _server = new WebSocketServer($"ws://0.0.0.0:{Port}");
            _server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    lock (_clientsLock)
                    {
                        _clients.Add(socket);
                    }
                };

                socket.OnClose = () =>
                {
                    lock (_clientsLock)
                    {
                        _clients.Remove(socket);
                    }
                };

                socket.OnMessage = message => HandleIncomingMessage(socket, message);
            });

            IsRunning = true;
        }
        catch (Exception ex)
        {
            IsRunning = false;
            _errorService.LogError(ErrorCategory.UnexpectedSystemError, $"Start Fleck WebSocketServer on port {Port}", ex);
        }
    }

    private void HandleIncomingMessage(IWebSocketConnection socket, string rawJson)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<RemoteCommandPayload>(rawJson, JsonOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Action))
            {
                return;
            }

            if (Application.Current?.Dispatcher is { } dispatcher)
            {
                _ = dispatcher.InvokeAsync(() => _commandDispatcher(payload));
            }
            else
            {
                _commandDispatcher(payload);
            }

            _ = socket.Send("{\"status\":\"ok\"}");
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.UnexpectedSystemError, $"Parse WebSocket message: {rawJson}", ex);
        }
    }

    public void Dispose()
    {
        IsRunning = false;
        lock (_clientsLock)
        {
            foreach (var client in _clients.ToList())
            {
                try
                {
                    client.Close();
                }
                catch
                {
                    // Bỏ qua lỗi khi đóng socket
                }
            }
            _clients.Clear();
        }

        try
        {
            _server?.Dispose();
        }
        catch
        {
            // Bỏ qua lỗi khi giải phóng server
        }
    }
}
