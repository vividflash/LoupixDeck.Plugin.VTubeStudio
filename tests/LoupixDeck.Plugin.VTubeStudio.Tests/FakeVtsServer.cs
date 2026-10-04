using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

/// <summary>One request seen by <see cref="FakeVtsServer"/>.</summary>
internal sealed record RecordedRequest(string MessageType, string RequestId, JsonElement Data, JsonElement Envelope);

/// <summary>
/// Local VTube Studio stand-in: an HttpListener websocket server on a free loopback port.
/// Handlers run concurrently per request. A messageType without a handler gets no answer.
/// </summary>
internal sealed class FakeVtsServer : IAsyncDisposable
{
    private readonly int _port;
    private readonly ConcurrentDictionary<string, Func<JsonElement, Task<ServerReply>>> _handlers = new();
    private readonly ConcurrentDictionary<ServerConnection, byte> _connections = new();
    private readonly List<RecordedRequest> _requests = new();
    private readonly object _lock = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task _acceptLoop = Task.CompletedTask;

    internal FakeVtsServer()
    {
        _port = GetFreePort();
        Start();
    }

    /// <summary>A currently unused loopback port.</summary>
    internal static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    internal Uri Uri => new($"ws://127.0.0.1:{_port}/");

    internal int ConnectionCount => _connections.Count;

    internal IReadOnlyList<RecordedRequest> Requests
    {
        get { lock (_lock) return _requests.ToArray(); }
    }

    /// <summary>Answers <paramref name="messageType"/> with the data returned by <paramref name="handler"/>.</summary>
    internal void On(string messageType, Func<JsonElement, object?> handler) =>
        _handlers[messageType] = data => Task.FromResult(ServerReply.Ok(handler(data)));

    /// <summary>Like <see cref="On"/>, but the handler may delay or choose an error reply.</summary>
    internal void OnAsync(string messageType, Func<JsonElement, Task<ServerReply>> handler) =>
        _handlers[messageType] = handler;

    /// <summary>Answers <paramref name="messageType"/> with an <c>APIError</c>.</summary>
    internal void OnError(string messageType, int errorId, string message) =>
        _handlers[messageType] = _ => Task.FromResult(ServerReply.Error(errorId, message));

    /// <summary>Sends an unsolicited message (no matching requestID) to every connected client.</summary>
    internal async Task PushEventAsync(string messageType, object? data)
    {
        var json = Envelope(messageType, "event-" + Guid.NewGuid().ToString("N"), data);
        foreach (var connection in _connections.Keys)
            await connection.SendAsync(json, 0).ConfigureAwait(false);
    }

    /// <summary>Aborts every open socket; the listener keeps accepting.</summary>
    internal void DropConnections()
    {
        foreach (var connection in _connections.Keys)
            connection.Abort();
    }

    /// <summary>Closes all sockets and stops listening. <see cref="Start"/> resumes on the same port.</summary>
    internal void Stop()
    {
        var listener = _listener;
        if (listener is null) return;
        _listener = null;
        _cts?.Cancel();
        DropConnections();
        try { listener.Abort(); } catch { }
        try { _acceptLoop.Wait(TimeSpan.FromSeconds(5)); } catch { }
        _cts?.Dispose();
        _cts = null;
    }

    internal void Start()
    {
        if (_listener is not null) return;
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        listener.Start();
        _listener = listener;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, ct));
    }

    /// <summary>Waits until <paramref name="condition"/> holds; fails the test after <paramref name="timeout"/>.</summary>
    internal static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Condition not met in time.");
            await Task.Delay(5).ConfigureAwait(false);
        }
    }

    internal Task WaitForRequestsAsync(int count) => WaitUntilAsync(() => Requests.Count >= count);

    internal Task WaitForConnectionsAsync(int count) => WaitUntilAsync(() => ConnectionCount >= count);

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch
            {
                return;
            }
            _ = Task.Run(() => HandleConnectionAsync(context, ct));
        }
    }

    private async Task HandleConnectionAsync(HttpListenerContext context, CancellationToken ct)
    {
        if (!context.Request.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        WebSocket socket;
        try
        {
            socket = (await context.AcceptWebSocketAsync(null).ConfigureAwait(false)).WebSocket;
        }
        catch
        {
            return;
        }

        var connection = new ServerConnection(socket);
        _connections[connection] = 0;
        try
        {
            var buffer = new byte[8 * 1024];
            using var message = new MemoryStream();
            while (!ct.IsCancellationRequested)
            {
                message.SetLength(0);
                ValueWebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                using var doc = JsonDocument.Parse(message.ToArray());
                var envelope = doc.RootElement.Clone();
                var messageType = envelope.GetProperty("messageType").GetString() ?? "";
                var requestId = envelope.GetProperty("requestID").GetString() ?? "";
                var data = envelope.TryGetProperty("data", out var d) ? d : default;

                lock (_lock) _requests.Add(new RecordedRequest(messageType, requestId, data, envelope));
                if (_handlers.TryGetValue(messageType, out var handler))
                    _ = Task.Run(() => AnswerAsync(connection, handler, messageType, requestId, data));
            }
        }
        catch
        {
            // Connection dropped or server stopped.
        }
        finally
        {
            _connections.TryRemove(connection, out _);
            connection.Abort();
        }
    }

    private static async Task AnswerAsync(ServerConnection connection, Func<JsonElement, Task<ServerReply>> handler,
        string messageType, string requestId, JsonElement data)
    {
        try
        {
            var reply = await handler(data).ConfigureAwait(false);
            string json;
            if (reply.IsError)
                json = Envelope("APIError", requestId, new { errorID = reply.ErrorId, message = reply.ErrorMessage });
            else
                json = Envelope(messageType.EndsWith("Request") ? messageType[..^"Request".Length] + "Response" : messageType,
                    requestId, reply.Data ?? new { });
            await connection.SendAsync(json, 4096).ConfigureAwait(false);
        }
        catch
        {
            // Socket gone while answering.
        }
    }

    private static string Envelope(string messageType, string requestId, object? data) =>
        JsonSerializer.Serialize(new
        {
            apiName = "VTubeStudioPublicAPI",
            apiVersion = "1.0",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            messageType,
            requestID = requestId,
            data = data ?? new { },
            unknownField = "ignored",
        });

    private sealed class ServerConnection(WebSocket socket)
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        /// <summary>Sends as text; <paramref name="chunk"/> above 0 splits it into several frames.</summary>
        internal async Task SendAsync(string json, int chunk)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (chunk <= 0) chunk = bytes.Length;
                for (var offset = 0; offset < bytes.Length; offset += chunk)
                {
                    var count = Math.Min(chunk, bytes.Length - offset);
                    await socket.SendAsync(bytes.AsMemory(offset, count), WebSocketMessageType.Text,
                        offset + count >= bytes.Length, CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        internal void Abort()
        {
            try { socket.Abort(); } catch { }
        }
    }
}

/// <summary>What a <see cref="FakeVtsServer"/> handler answers with.</summary>
internal sealed record ServerReply(object? Data, int ErrorId, string ErrorMessage, bool IsError)
{
    internal static ServerReply Ok(object? data) => new(data, 0, "", false);

    internal static ServerReply Error(int errorId, string message) => new(null, errorId, message, true);
}
