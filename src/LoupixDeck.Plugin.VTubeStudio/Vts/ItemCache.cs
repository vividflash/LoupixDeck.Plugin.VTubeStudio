using System.Text.Json;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Vts;

/// <summary>One item instance in the VTube Studio scene.</summary>
internal sealed record ItemInfo(string FileName, string InstanceId, string Type);

/// <summary>
/// The item instances in the current scene. Refetched when the service becomes connected and on
/// ItemEvent / ModelLoadedEvent; cleared whenever the service is not connected. Readers only ever
/// see the cached list.
/// </summary>
internal sealed class ItemCache : IDisposable
{
    private readonly VtsService _service;
    private readonly IPluginHost _host;
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();

    private ItemInfo[] _items = [];
    private bool _running;
    private bool _pending;
    private int _generation;
    private long _startedSeq;
    private long _appliedSeq;
    private bool _disposed;

    internal ItemCache(VtsService service, IPluginHost host)
    {
        _service = service;
        _host = host;
        service.StatusChanged += OnStatusChanged;
        service.EventReceived += OnEvent;
    }

    /// <summary>Raised when the cached list actually changed, never while a lock is held.</summary>
    internal event Action? Changed;

    internal IReadOnlyList<ItemInfo> Items
    {
        get { lock (_lock) return _items; }
    }

    /// <summary>
    /// Finds an item, case-insensitive: by exact file name, then by file name containing the key,
    /// then by all words of the key being contained in the file name. First in scene order wins.
    /// </summary>
    internal ItemInfo? Find(string key)
    {
        key = key.Trim();
        if (key.Length == 0) return null;
        var items = Items;
        const StringComparison cmp = StringComparison.OrdinalIgnoreCase;
        var words = key.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return items.FirstOrDefault(i => string.Equals(i.FileName, key, cmp))
               ?? items.FirstOrDefault(i => i.FileName.Contains(key, cmp))
               ?? items.FirstOrDefault(i => words.All(w => i.FileName.Contains(w, cmp)));
    }

    /// <summary>Starts a refetch in the background. Requests during a running fetch collapse into one more fetch.</summary>
    internal void RequestRefetch()
    {
        lock (_lock)
        {
            if (_disposed) return;
            if (_running)
            {
                _pending = true;
                return;
            }
            _running = true;
        }
        _ = Task.Run(FetchLoopAsync);
    }

    /// <summary>
    /// Fetches the list now and completes when the cache holds the answer. Runs next to the background
    /// loop without touching its coalescing; a result older than an already applied one is dropped. Never throws.
    /// </summary>
    internal async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            await FetchOnceAsync(linked.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (!_cts.IsCancellationRequested && !ct.IsCancellationRequested)
                _host.Logger.Warn($"VTube Studio: item list not loaded ({ex.Message})");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _service.StatusChanged -= OnStatusChanged;
        _service.EventReceived -= OnEvent;
        _cts.Cancel();
    }

    private void OnStatusChanged()
    {
        if (_service.Status == VtsStatus.Connected) RequestRefetch();
        else Clear();
    }

    private void OnEvent(string type, JsonElement data)
    {
        if (type is "ItemEvent" or "ModelLoadedEvent") RequestRefetch();
    }

    private void Clear()
    {
        bool changed;
        lock (_lock)
        {
            _generation++;
            changed = _items.Length > 0;
            _items = [];
        }
        if (changed) RaiseChanged();
    }

    private async Task FetchLoopAsync()
    {
        while (true)
        {
            try
            {
                await FetchOnceAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!_cts.IsCancellationRequested)
                    _host.Logger.Warn($"VTube Studio: item list not loaded ({ex.Message})");
            }

            lock (_lock)
            {
                if (!_pending || _disposed)
                {
                    _pending = false;
                    _running = false;
                    return;
                }
                _pending = false;
            }
        }
    }

    private async Task FetchOnceAsync(CancellationToken ct)
    {
        int generation;
        long seq;
        lock (_lock)
        {
            generation = _generation;
            seq = ++_startedSeq;
        }
        if (_service.Status != VtsStatus.Connected)
        {
            Clear();
            return;
        }

        var data = await _service.RequestAsync("ItemListRequest",
            new { includeAvailableSpots = false, includeItemInstancesInScene = true, includeAvailableItemFiles = false }, ct)
            .ConfigureAwait(false);
        var items = Parse(data);

        bool changed;
        lock (_lock)
        {
            // Dropped to not-connected while the request ran, or a newer fetch already applied: stale.
            if (generation != _generation || _disposed || seq < _appliedSeq) return;
            _appliedSeq = seq;
            changed = !_items.SequenceEqual(items);
            if (changed) _items = items;
        }
        if (changed) RaiseChanged();
    }

    private static ItemInfo[] Parse(JsonElement data)
    {
        var list = new List<ItemInfo>();
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("itemInstancesInScene", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in arr.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var file = Str(e, "fileName");
                var id = Str(e, "instanceID");
                if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(id)) continue;
                list.Add(new ItemInfo(file, id, Str(e, "type") ?? ""));
            }
        }
        return [.. list];
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            _host.Logger.Error("VTube Studio: item change handler failed", ex);
        }
    }
}
