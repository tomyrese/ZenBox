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
    private readonly Func<RemoteStateDto>? _stateSnapshotProvider;
    private readonly List<IWebSocketConnection> _clients = new();
    private readonly object _clientsLock = new();
    private WebSocketServer? _server;

    public int Port { get; }
    public bool IsRunning { get; private set; }

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
                    SendCurrentStateToClient(socket);
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

    public void BroadcastState()
    {
        if (_stateSnapshotProvider is null || !IsRunning)
        {
            return;
        }

        string json;
        try
        {
            var state = _stateSnapshotProvider();
            json = JsonSerializer.Serialize(state, JsonOptions);
        }
        catch
        {
            return;
        }

        List<IWebSocketConnection> snapshot;
        lock (_clientsLock)
        {
            snapshot = _clients.ToList();
        }

        foreach (var client in snapshot)
        {
            try
            {
                if (client.IsAvailable)
                {
                    _ = client.Send(json);
                }
            }
            catch
            {
            }
        }
    }

    private void SendCurrentStateToClient(IWebSocketConnection socket)
    {
        if (_stateSnapshotProvider is null)
        {
            return;
        }

        try
        {
            if (Application.Current?.Dispatcher is { } dispatcher)
            {
                _ = dispatcher.InvokeAsync(() =>
                {
                    var state = _stateSnapshotProvider();
                    var json = JsonSerializer.Serialize(state, JsonOptions);
                    if (socket.IsAvailable)
                    {
                        _ = socket.Send(json);
                    }
                });
            }
            else
            {
                var state = _stateSnapshotProvider();
                var json = JsonSerializer.Serialize(state, JsonOptions);
                _ = socket.Send(json);
            }
        }
        catch
        {
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

            if (string.Equals(payload.Action, "PING", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(payload.Action, "GET_STATE", StringComparison.OrdinalIgnoreCase))
            {
                SendCurrentStateToClient(socket);
                return;
            }

            if (Application.Current?.Dispatcher is { } dispatcher)
            {
                _ = dispatcher.InvokeAsync(() =>
                {
                    _commandDispatcher(payload);
                    BroadcastState();
                });
            }
            else
            {
                _commandDispatcher(payload);
                BroadcastState();
            }
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
        }
    }
}
