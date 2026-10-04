using System.Text.Json;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Vts;

/// <summary>
/// Keeps a <see cref="VtsClient"/> connected and authenticated in a background loop and exposes
/// status, version and the loaded model. Each connection attempt uses a fresh client.
/// </summary>
internal sealed class VtsService : IDisposable
{
    internal const string PluginName = "LoupixDeck";
    internal const string PluginDeveloper = "vividflash";
    internal const string HostKey = "host";
    internal const string PortKey = "port";
    internal const long DefaultPort = 8001;
    internal const string TokenKey = "auth_token";

    internal const string IconResource = "icon128.png";

    private static readonly Lazy<string?> PluginIcon = new(LoadIcon);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TokenRequestTimeout = TimeSpan.FromSeconds(60);

    private readonly IPluginHost _host;
    private readonly TimeSpan _retryInterval;
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();

    private VtsClient? _client;
    private VtsStatus _status = VtsStatus.Offline;
    private string? _modelName;
    private string? _version;
    private Task _loop = Task.CompletedTask;
    private bool _started;
    private bool _disposed;
    private bool _offlineLogged;

    internal VtsService(IPluginHost host, TimeSpan? retryInterval = null)
    {
        _host = host;
        _retryInterval = retryInterval ?? TimeSpan.FromSeconds(3);
    }

    internal VtsStatus Status
    {
        get { lock (_lock) return _status; }
    }

    internal string? CurrentModelName
    {
        get { lock (_lock) return _modelName; }
    }

    internal string? VTubeStudioVersion
    {
        get { lock (_lock) return _version; }
    }

    /// <summary>Raised on every status change and model change, never while a lock is held.</summary>
    internal event Action? StatusChanged;

    /// <summary>Events from VTube Studio (messageType, data).</summary>
    internal event Action<string, JsonElement>? EventReceived;

    internal void Start()
    {
        lock (_lock)
        {
            if (_started || _disposed) return;
            _started = true;
        }
        var ct = _cts.Token;
        _loop = Task.Run(() => LoopAsync(ct));
    }

    /// <summary>Asks VTube Studio for a token (the user has to click Allow there), stores it and authenticates.</summary>
    internal async Task AuthoriseAsync(CancellationToken ct)
    {
        VtsClient client;
        lock (_lock)
        {
            if (_client is null || !_client.IsConnected)
                throw new InvalidOperationException("VTube Studio is offline");
            client = _client;
        }

        try
        {
            var tokenRequest = new Dictionary<string, object> { ["pluginName"] = PluginName, ["pluginDeveloper"] = PluginDeveloper };
            var icon = PluginIcon.Value;
            if (icon is not null) tokenRequest["pluginIcon"] = icon;
            var data = await client.RequestAsync("AuthenticationTokenRequest", tokenRequest, ct, TokenRequestTimeout)
                .ConfigureAwait(false);
            var token = data.ValueKind == JsonValueKind.Object
                        && data.TryGetProperty("authenticationToken", out var t)
                        && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("VTube Studio sent no token");

            _host.Settings.Set(TokenKey, token);
            _host.Settings.Save();
            await AuthenticateAsync(client, ct).ConfigureAwait(false);
        }
        catch (VtsApiException)
        {
            throw new InvalidOperationException("Access was denied in VTube Studio");
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException("No answer from VTube Studio");
        }
        catch (IOException)
        {
            throw new InvalidOperationException("VTube Studio is offline");
        }

        if (Status != VtsStatus.Connected)
            throw new InvalidOperationException("VTube Studio did not accept the token");
    }

    internal void ForgetToken()
    {
        _host.Settings.Remove(TokenKey);
        _host.Settings.Save();
        var changed = false;
        lock (_lock)
        {
            if (_status == VtsStatus.Connected)
            {
                _status = VtsStatus.NotAuthorised;
                changed = true;
            }
        }
        if (changed) RaiseStatusChanged();
    }

    internal Task<JsonElement> RequestAsync(string messageType, object? data, CancellationToken ct)
    {
        VtsClient? client;
        VtsStatus status;
        lock (_lock)
        {
            client = _client;
            status = _status;
        }
        if (client is null || status == VtsStatus.Offline)
            throw new InvalidOperationException("VTube Studio is offline");
        if (status != VtsStatus.Connected)
            throw new InvalidOperationException("Not authorised in VTube Studio");
        return client.RequestAsync(messageType, data, ct);
    }

    /// <summary>Drops the current connection so changed host/port settings apply.</summary>
    internal void Reconnect()
    {
        VtsClient? client;
        lock (_lock) client = _client;
        if (client is null) return;
        _ = DisposeClientAsync(client);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _cts.Cancel();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // The loop ends through cancellation.
        }

        VtsClient? client;
        lock (_lock) client = _client;
        if (client is not null) DisposeClientAsync(client).Wait(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _host.Logger.Error("VTube Studio: connection loop failed", ex);
            }

            try
            {
                await Task.Delay(_retryInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>One connection from connect attempt to disconnect. Returns when the connection is gone.</summary>
    private async Task RunSessionAsync(CancellationToken ct)
    {
        var client = new VtsClient();
        var gone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnDisconnected()
        {
            SetOffline(client);
            gone.TrySetResult();
        }
        void OnEvent(string type, JsonElement data) => HandleEvent(client, type, data);

        lock (_lock) _client = client;
        client.Disconnected += OnDisconnected;
        client.EventReceived += OnEvent;
        try
        {
            try
            {
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(ConnectTimeout);
                await client.ConnectAsync(BuildUri(), connectCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                if (!_offlineLogged)
                {
                    _offlineLogged = true;
                    _host.Logger.Warn($"VTube Studio: not reachable, retrying ({ex.Message})");
                }
                return;
            }

            _offlineLogged = false;
            try
            {
                await AuthenticateAsync(client, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _host.Logger.Warn($"VTube Studio: setup failed ({ex.Message})");
                return;
            }

            await gone.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            client.Disconnected -= OnDisconnected;
            client.EventReceived -= OnEvent;
            await DisposeClientAsync(client).ConfigureAwait(false);
            SetOffline(client);
        }
    }

    private static string? LoadIcon()
    {
        using var stream = typeof(VtsService).Assembly.GetManifestResourceStream(IconResource);
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private Uri BuildUri() =>
        new($"ws://{ReadHost(_host.Settings)}:{ReadPort(_host.Settings)}");

    internal static string ReadHost(IPluginSettings settings)
    {
        var host = settings.Get<string>(HostKey, "localhost");
        return string.IsNullOrWhiteSpace(host) ? "localhost" : host.Trim();
    }

    internal static long ReadPort(IPluginSettings settings)
    {
        var port = settings.Get<long>(PortKey, DefaultPort);
        return port is > 0 and <= 65535 ? port : DefaultPort;
    }

    private async Task AuthenticateAsync(VtsClient client, CancellationToken ct)
    {
        var token = _host.Settings.Get<string>(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            SetStatus(client, VtsStatus.NotAuthorised);
            return;
        }

        bool authenticated;
        try
        {
            var data = await client.RequestAsync("AuthenticationRequest",
                new { pluginName = PluginName, pluginDeveloper = PluginDeveloper, authenticationToken = token }, ct)
                .ConfigureAwait(false);
            authenticated = data.ValueKind == JsonValueKind.Object
                            && data.TryGetProperty("authenticated", out var a) && a.ValueKind == JsonValueKind.True;
        }
        catch (VtsApiException ex)
        {
            _host.Logger.Warn($"VTube Studio: authentication failed ({ex.Message})");
            SetStatus(client, VtsStatus.NotAuthorised);
            return;
        }

        if (!authenticated)
        {
            _host.Settings.Remove(TokenKey);
            _host.Settings.Save();
            _host.Logger.Info("VTube Studio: stored token was rejected and has been removed");
            SetStatus(client, VtsStatus.NotAuthorised);
            return;
        }

        await LoadStateAsync(client, ct).ConfigureAwait(false);
        SetStatus(client, VtsStatus.Connected);
    }

    private async Task LoadStateAsync(VtsClient client, CancellationToken ct)
    {
        try
        {
            var state = await client.RequestAsync("APIStateRequest", null, ct).ConfigureAwait(false);
            if (state.ValueKind == JsonValueKind.Object && state.TryGetProperty("vTubeStudioVersion", out var v)
                && v.ValueKind == JsonValueKind.String)
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_client, client)) _version = v.GetString();
                }
            }

            await client.RequestAsync("EventSubscriptionRequest",
                new { eventName = "ModelLoadedEvent", subscribe = true, config = new { } }, ct).ConfigureAwait(false);
            await client.RequestAsync("EventSubscriptionRequest",
                new { eventName = "HotkeyTriggeredEvent", subscribe = true, config = new { } }, ct).ConfigureAwait(false);
            await client.RequestAsync("EventSubscriptionRequest",
                new { eventName = "ItemEvent", subscribe = true, config = new { } }, ct).ConfigureAwait(false);
            await client.RequestAsync("EventSubscriptionRequest",
                new { eventName = "ModelConfigChangedEvent", subscribe = true, config = new { } }, ct).ConfigureAwait(false);

            var model = await client.RequestAsync("CurrentModelRequest", null, ct).ConfigureAwait(false);
            ApplyModel(client, model);
        }
        catch (Exception ex) when (ex is VtsApiException or TimeoutException)
        {
            _host.Logger.Warn($"VTube Studio: could not read initial state ({ex.Message})");
        }
    }

    private void HandleEvent(VtsClient client, string type, JsonElement data)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(_client, client)) return;
        }
        if (type == "ModelLoadedEvent") ApplyModel(client, data);
        try
        {
            EventReceived?.Invoke(type, data);
        }
        catch (Exception ex)
        {
            _host.Logger.Error("VTube Studio: event handler failed", ex);
        }
    }

    /// <summary>Reads <c>modelLoaded</c>/<c>modelName</c> from a CurrentModelResponse or ModelLoadedEvent payload.</summary>
    private void ApplyModel(VtsClient client, JsonElement data)
    {
        string? name = null;
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("modelLoaded", out var loaded) && loaded.ValueKind == JsonValueKind.True
            && data.TryGetProperty("modelName", out var n) && n.ValueKind == JsonValueKind.String)
            name = n.GetString();
        if (string.IsNullOrEmpty(name)) name = null;

        bool changed;
        lock (_lock)
        {
            changed = ReferenceEquals(_client, client) && _modelName != name;
            if (changed) _modelName = name;
        }
        if (changed) RaiseStatusChanged();
    }

    private void SetStatus(VtsClient client, VtsStatus status)
    {
        bool changed;
        lock (_lock)
        {
            changed = ReferenceEquals(_client, client) && client.IsConnected && _status != status;
            if (changed) _status = status;
        }
        if (changed) RaiseStatusChanged();
    }

    private void SetOffline(VtsClient client)
    {
        bool changed;
        lock (_lock)
        {
            if (!ReferenceEquals(_client, client)) return;
            _client = null;
            changed = _status != VtsStatus.Offline || _modelName is not null;
            _status = VtsStatus.Offline;
            _modelName = null;
        }
        if (changed) RaiseStatusChanged();
    }

    private void RaiseStatusChanged()
    {
        try
        {
            StatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _host.Logger.Error("VTube Studio: status handler failed", ex);
        }
    }

    private static async Task DisposeClientAsync(VtsClient client)
    {
        try
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Already gone.
        }
    }
}
