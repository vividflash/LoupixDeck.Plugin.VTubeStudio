using System.Text.Json;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Vts;

/// <summary>One hotkey of the model or of an item.</summary>
internal sealed record HotkeyInfo(string HotkeyId, string Name, string Type, string File);

/// <summary>
/// The hotkey lists of the loaded model and of each Live2D item in the scene, plus the tracked on/off
/// state of item expression hotkeys (VTube Studio has no request for it, only HotkeyTriggeredEvent).
/// Lists are refetched when the service becomes connected, on ModelLoadedEvent / ModelConfigChangedEvent
/// and when the item list changes; everything is cleared whenever the service is not connected.
/// Item states start off, flip on every item hotkey event of an expression toggle, and are keyed by
/// hotkeyID alone (IDs are unique). Readers only ever see cached data.
/// </summary>
internal sealed class HotkeyCache : IDisposable
{
    internal const string ToggleExpressionType = "ToggleExpression";
    private static readonly string[] FileSuffixes = [".exp3.json", ".motion3.json"];
    private const string RemoveAllExpressionsAction = "RemoveAllExpressions";

    private readonly VtsService _service;
    private readonly ItemCache _items;
    private readonly IPluginHost _host;
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();

    private HotkeyInfo[] _model = [];
    private Dictionary<string, HotkeyInfo[]> _itemLists = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string[]> _instances = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _on = [];
    private bool _running;
    private bool _pending;
    private int _generation;
    private long _startedSeq;
    private long _appliedSeq;
    private bool _disposed;
    private bool _loggedItemEvent;

    internal HotkeyCache(VtsService service, ItemCache items, IPluginHost host)
    {
        _service = service;
        _items = items;
        _host = host;
        service.StatusChanged += OnStatusChanged;
        service.EventReceived += OnEvent;
        items.Changed += RequestRefetch;
    }

    /// <summary>Raised when a list or a tracked state actually changed, never while a lock is held.</summary>
    internal event Action? Changed;

    /// <summary>Finds a hotkey of the model by ID, name or expression/motion file (case-insensitive), first match in list order.</summary>
    internal HotkeyInfo? FindModelHotkey(string key)
    {
        HotkeyInfo[] list;
        lock (_lock) list = _model;
        return Match(list, key);
    }

    /// <summary>Finds a hotkey of the item with this file name by ID, name or expression/motion file (case-insensitive), first match.</summary>
    internal HotkeyInfo? FindItemHotkey(string itemFileName, string key)
    {
        HotkeyInfo[]? list;
        lock (_lock) _itemLists.TryGetValue(itemFileName, out list);
        return list is null ? null : Match(list, key);
    }

    /// <summary>The tracked state of an item expression hotkey: off unless an event switched it on.</summary>
    internal bool IsItemHotkeyOn(string hotkeyId)
    {
        lock (_lock) return _on.Contains(hotkeyId);
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
    /// Fetches the lists now and completes when the cache holds the answer. Runs next to the background
    /// loop without touching its coalescing. Never throws.
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
                _host.Logger.Warn($"VTube Studio: hotkey lists not loaded ({ex.Message})");
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
        _items.Changed -= RequestRefetch;
        _cts.Cancel();
    }

    // ID first, then name, then the file with or without its suffix: a name never loses to a file.
    private static HotkeyInfo? Match(HotkeyInfo[] list, string key)
    {
        key = key.Trim();
        if (key.Length == 0) return null;
        const StringComparison cmp = StringComparison.OrdinalIgnoreCase;
        return list.FirstOrDefault(h => string.Equals(h.HotkeyId, key, cmp))
               ?? list.FirstOrDefault(h => string.Equals(h.Name, key, cmp))
               ?? list.FirstOrDefault(h => FileMatches(h.File, key));
    }

    private static bool FileMatches(string file, string key)
    {
        if (file.Length == 0) return false;
        const StringComparison cmp = StringComparison.OrdinalIgnoreCase;
        if (string.Equals(file, key, cmp)) return true;
        foreach (var suffix in FileSuffixes)
            if (file.EndsWith(suffix, cmp) && string.Equals(file[..^suffix.Length], key, cmp)) return true;
        return false;
    }

    private void OnStatusChanged()
    {
        if (_service.Status == VtsStatus.Connected) RequestRefetch();
        else Clear();
    }

    private void OnEvent(string type, JsonElement data)
    {
        switch (type)
        {
            case "ModelLoadedEvent":
            case "ModelConfigChangedEvent":
                RequestRefetch();
                break;
            case "HotkeyTriggeredEvent":
                OnHotkeyTriggered(data);
                break;
        }
    }

    private void OnHotkeyTriggered(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object) return;
        if (!data.TryGetProperty("isLive2DItem", out var isItem) || isItem.ValueKind != JsonValueKind.True) return;

        var id = Str(data, "hotkeyID");
        var action = Str(data, "hotkeyAction");
        bool changed;
        bool log;
        lock (_lock)
        {
            log = !_loggedItemEvent;
            _loggedItemEvent = true;
            changed = ApplyItemEvent(id, action);
        }
        if (log)
            _host.Logger.Info($"VTube Studio: first item hotkey event: modelID '{Str(data, "modelID")}', modelName '{Str(data, "modelName")}', hotkeyID '{id}'");
        if (changed) RaiseChanged();
    }

    // Caller holds the lock. Item states are keyed by hotkeyID: only hotkeys known from an item list are tracked.
    private bool ApplyItemEvent(string? id, string? action)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var (file, hotkey) = LookupItemHotkey(id);
        if (hotkey is null || file is null) return false;

        if (action == RemoveAllExpressionsAction)
        {
            var removed = false;
            foreach (var h in _itemLists[file]) removed |= _on.Remove(h.HotkeyId);
            return removed;
        }

        if (action != ToggleExpressionType && !(action is null && hotkey.Type == ToggleExpressionType)) return false;
        if (!_on.Remove(hotkey.HotkeyId)) _on.Add(hotkey.HotkeyId);
        return true;
    }

    private (string? File, HotkeyInfo? Hotkey) LookupItemHotkey(string id)
    {
        foreach (var (file, list) in _itemLists)
        {
            var hotkey = list.FirstOrDefault(h => h.HotkeyId == id);
            if (hotkey is not null) return (file, hotkey);
        }
        return (null, null);
    }

    private void Clear()
    {
        bool changed;
        lock (_lock)
        {
            _generation++;
            changed = _model.Length > 0 || _itemLists.Count > 0 || _on.Count > 0;
            _model = [];
            _itemLists = new(StringComparer.OrdinalIgnoreCase);
            _instances = new(StringComparer.OrdinalIgnoreCase);
            _on = [];
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
                    _host.Logger.Warn($"VTube Studio: hotkey lists not loaded ({ex.Message})");
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

        // One request for the model, then one per distinct Live2D item file, one after the other.
        var model = await FetchListAsync(null, ct).ConfigureAwait(false);
        var scene = _items.Items.Where(i => i.Type == "Live2D").ToArray();
        var lists = new Dictionary<string, HotkeyInfo[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in scene.Select(i => i.FileName).Distinct(StringComparer.OrdinalIgnoreCase))
            lists[file] = await FetchListAsync(file, ct).ConfigureAwait(false);
        var instances = scene.GroupBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(i => i.InstanceId).Order().ToArray(), StringComparer.OrdinalIgnoreCase);

        bool changed;
        lock (_lock)
        {
            // Dropped to not-connected while the requests ran, or a newer fetch already applied: stale.
            if (generation != _generation || _disposed || seq < _appliedSeq) return;
            _appliedSeq = seq;
            changed = !_model.SequenceEqual(model);
            _model = model;

            // An item that left the scene or came back as a new instance starts off again.
            foreach (var (file, list) in _itemLists)
            {
                var gone = !instances.TryGetValue(file, out var now)
                           || !_instances.TryGetValue(file, out var before) || !before.SequenceEqual(now);
                if (!gone) continue;
                foreach (var h in list) changed |= _on.Remove(h.HotkeyId);
            }

            changed |= !SameLists(_itemLists, lists);
            _itemLists = lists;
            _instances = instances;
        }
        if (changed) RaiseChanged();
    }

    private async Task<HotkeyInfo[]> FetchListAsync(string? itemFile, CancellationToken ct)
    {
        object request = itemFile is null ? new { } : new { live2DItemFileName = itemFile };
        try
        {
            var data = await _service.RequestAsync("HotkeysInCurrentModelRequest", request, ct).ConfigureAwait(false);
            return Parse(data);
        }
        catch (VtsApiException ex)
        {
            _host.Logger.Warn($"VTube Studio: hotkeys of {(itemFile is null ? "the model" : $"item '{itemFile}'")} not loaded ({ex.Message})");
            return [];
        }
    }

    private static bool SameLists(Dictionary<string, HotkeyInfo[]> a, Dictionary<string, HotkeyInfo[]> b) =>
        a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var other) && p.Value.SequenceEqual(other));

    private static HotkeyInfo[] Parse(JsonElement data)
    {
        var list = new List<HotkeyInfo>();
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("availableHotkeys", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in arr.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var id = Str(e, "hotkeyID");
                if (string.IsNullOrEmpty(id)) continue;
                list.Add(new HotkeyInfo(id, Str(e, "name") ?? "", Str(e, "type") ?? "", Str(e, "file") ?? ""));
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
            _host.Logger.Error("VTube Studio: hotkey change handler failed", ex);
        }
    }
}
