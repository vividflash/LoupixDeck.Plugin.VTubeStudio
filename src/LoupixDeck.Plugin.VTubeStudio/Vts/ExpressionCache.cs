using System.Text.Json;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Vts;

/// <summary>One expression of the loaded model.</summary>
internal sealed record ExpressionInfo(string Name, string File, bool Active);

/// <summary>
/// The expressions of the current model and whether each is active. Refetched when the service
/// becomes connected, on ModelLoadedEvent / HotkeyTriggeredEvent and after activation requests;
/// cleared whenever the service is not connected. Readers only ever see the cached list.
/// </summary>
internal sealed class ExpressionCache : IDisposable
{
    internal const string FileSuffix = ".exp3.json";

    private readonly VtsService _service;
    private readonly IPluginHost _host;
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();

    private ExpressionInfo[] _items = [];
    private bool _running;
    private bool _pending;
    private int _generation;
    private bool _disposed;

    internal ExpressionCache(VtsService service, IPluginHost host)
    {
        _service = service;
        _host = host;
        service.StatusChanged += OnStatusChanged;
        service.EventReceived += OnEvent;
    }

    /// <summary>Raised when the cached list actually changed, never while a lock is held.</summary>
    internal event Action? Changed;

    internal IReadOnlyList<ExpressionInfo> Expressions
    {
        get { lock (_lock) return _items; }
    }

    /// <summary>Finds an expression by file, file without ".exp3.json" or name (case-insensitive, in that order).</summary>
    internal ExpressionInfo? Find(string key)
    {
        key = key.Trim();
        if (key.Length == 0) return null;
        var items = Expressions;
        const StringComparison cmp = StringComparison.OrdinalIgnoreCase;
        return items.FirstOrDefault(e => string.Equals(e.File, key, cmp))
               ?? items.FirstOrDefault(e => string.Equals(StripSuffix(e.File), key, cmp))
               ?? items.FirstOrDefault(e => string.Equals(e.Name, key, cmp));
    }

    /// <summary>Sets the cached state right after a successful activation request.</summary>
    internal void SetActive(string file, bool active)
    {
        bool changed;
        lock (_lock)
        {
            var index = Array.FindIndex(_items, e => e.File == file);
            changed = index >= 0 && _items[index].Active != active;
            if (changed)
            {
                var copy = (ExpressionInfo[])_items.Clone();
                copy[index] = copy[index] with { Active = active };
                _items = copy;
            }
        }
        if (changed) RaiseChanged();
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

    private static string StripSuffix(string file) =>
        file.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase) ? file[..^FileSuffix.Length] : file;

    private void OnStatusChanged()
    {
        if (_service.Status == VtsStatus.Connected) RequestRefetch();
        else Clear();
    }

    private void OnEvent(string type, JsonElement data)
    {
        if (type is "ModelLoadedEvent" or "HotkeyTriggeredEvent") RequestRefetch();
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
                await FetchOnceAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!_cts.IsCancellationRequested)
                    _host.Logger.Warn($"VTube Studio: expression list not loaded ({ex.Message})");
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

    private async Task FetchOnceAsync()
    {
        int generation;
        lock (_lock) generation = _generation;
        if (_service.Status != VtsStatus.Connected)
        {
            Clear();
            return;
        }

        var data = await _service.RequestAsync("ExpressionStateRequest", new { details = false }, _cts.Token)
            .ConfigureAwait(false);
        var items = Parse(data);

        bool changed;
        lock (_lock)
        {
            // Dropped to not-connected while the request ran: the result is stale.
            if (generation != _generation || _disposed) return;
            changed = !_items.SequenceEqual(items);
            if (changed) _items = items;
        }
        if (changed) RaiseChanged();
    }

    private static ExpressionInfo[] Parse(JsonElement data)
    {
        var list = new List<ExpressionInfo>();
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("expressions", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in arr.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var file = e.TryGetProperty("file", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
                if (string.IsNullOrEmpty(file)) continue;
                var name = e.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                var active = e.TryGetProperty("active", out var a) && a.ValueKind == JsonValueKind.True;
                list.Add(new ExpressionInfo(string.IsNullOrEmpty(name) ? StripSuffix(file) : name, file, active));
            }
        }
        return [.. list];
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            _host.Logger.Error("VTube Studio: expression change handler failed", ex);
        }
    }
}
