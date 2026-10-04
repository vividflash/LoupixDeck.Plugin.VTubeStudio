using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace LoupixDeck.Plugin.VTubeStudio.Vts;

/// <summary>
/// Request/response client for the VTube Studio websocket API. One background receive loop
/// matches responses to requests by <c>requestID</c>; everything else is raised as an event.
/// </summary>
internal sealed class VtsClient : IAsyncDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _requestTimeout;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Response>> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _stateLock = new();

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _loopCts;
    private Task _loopTask = Task.CompletedTask;
    private volatile bool _connected;
    private bool _disposed;
    private long _nextId;

    internal VtsClient(TimeSpan? requestTimeout = null)
    {
        _requestTimeout = requestTimeout ?? DefaultTimeout;
    }

    internal bool IsConnected => _connected;

    /// <summary>Raised for messages that do not answer a pending request (messageType, data).</summary>
    internal event Action<string, JsonElement>? EventReceived;

    /// <summary>Raised once per connection when the socket closes or fails.</summary>
    internal event Action? Disconnected;

    internal async Task ConnectAsync(Uri uri, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_connected) throw new InvalidOperationException("Already connected.");

        var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(uri, ct).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        var cts = new CancellationTokenSource();
        lock (_stateLock)
        {
            _socket = socket;
            _loopCts = cts;
            _connected = true;
        }
        _loopTask = Task.Run(() => ReceiveLoopAsync(socket, cts.Token));
    }

    internal async Task<JsonElement> RequestAsync(string messageType, object? data, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var socket = _socket;
        if (!_connected || socket is null) throw new InvalidOperationException("Not connected.");

        var requestId = "r" + Interlocked.Increment(ref _nextId).ToString("x");
        var tcs = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = tcs;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout ?? _requestTimeout);
        try
        {
            // Registered before sending; a disconnect that raced us has already swept _pending.
            if (!_connected) throw new InvalidOperationException("Not connected.");
            await SendAsync(socket, BuildRequest(requestId, messageType, data), timeoutCts.Token)
                .ConfigureAwait(false);
            var response = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);

            if (response.MessageType == "APIError")
            {
                var errorId = -1;
                var message = "";
                if (response.Data.ValueKind == JsonValueKind.Object)
                {
                    if (response.Data.TryGetProperty("errorID", out var id) && id.TryGetInt32(out var parsed))
                        errorId = parsed;
                    if (response.Data.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                        message = m.GetString() ?? "";
                }
                throw new VtsApiException(errorId, message);
            }
            return response.Data;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"No response to {messageType} within the timeout.");
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    private static byte[] BuildRequest(string requestId, string messageType, object? data)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("apiName", "VTubeStudioPublicAPI");
            writer.WriteString("apiVersion", "1.0");
            writer.WriteString("requestID", requestId);
            writer.WriteString("messageType", messageType);
            if (data is not null)
            {
                writer.WritePropertyName("data");
                JsonSerializer.Serialize(writer, data, data.GetType());
            }
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private async Task SendAsync(ClientWebSocket socket, byte[] payload, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await socket.SendAsync(payload, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException)
        {
            throw new IOException("Connection to VTube Studio lost.", ex);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        try
        {
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

                try
                {
                    Dispatch(message.GetBuffer().AsMemory(0, (int)message.Length));
                }
                catch
                {
                    // A bad message must not take the connection down.
                }
            }
        }
        catch
        {
            // Socket failure or cancellation: handled below.
        }
        finally
        {
            MarkDisconnected(new IOException("Connection to VTube Studio closed."));
            socket.Abort();
        }
    }

    private void Dispatch(ReadOnlyMemory<byte> json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return;

        var messageType = root.TryGetProperty("messageType", out var mt) && mt.ValueKind == JsonValueKind.String
            ? mt.GetString() ?? "" : "";
        var data = root.TryGetProperty("data", out var d) ? d.Clone() : default;

        if (root.TryGetProperty("requestID", out var rid) && rid.ValueKind == JsonValueKind.String
            && _pending.TryGetValue(rid.GetString()!, out var tcs))
        {
            tcs.TrySetResult(new Response(messageType, data));
            return;
        }

        try
        {
            EventReceived?.Invoke(messageType, data);
        }
        catch
        {
            // Subscriber errors stay with the subscriber.
        }
    }

    private void MarkDisconnected(Exception reason)
    {
        bool raise;
        lock (_stateLock)
        {
            raise = _connected;
            _connected = false;
        }
        foreach (var pair in _pending)
            pair.Value.TrySetException(reason);
        if (!raise) return;
        try
        {
            Disconnected?.Invoke();
        }
        catch
        {
            // Subscriber errors stay with the subscriber.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        var socket = _socket;
        var cts = _loopCts;
        try { cts?.Cancel(); } catch (ObjectDisposedException) { }
        socket?.Abort();
        try { await _loopTask.ConfigureAwait(false); } catch { }
        MarkDisconnected(new ObjectDisposedException(nameof(VtsClient)));
        socket?.Dispose();
        cts?.Dispose();
    }

    private readonly record struct Response(string MessageType, JsonElement Data);
}
