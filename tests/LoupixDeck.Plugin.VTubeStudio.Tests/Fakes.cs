using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

internal sealed class FakeLogger : IPluginLogger
{
    private readonly object _lock = new();
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get { lock (_lock) return _lines.ToArray(); }
    }

    public void Info(string message) => Add("I " + message);
    public void Warn(string message) => Add("W " + message);
    public void Error(string message, Exception? exception = null) => Add("E " + message + " " + exception?.Message);

    private void Add(string line)
    {
        lock (_lock) _lines.Add(line);
    }
}

internal sealed class FakeSettings : IPluginSettings
{
    private readonly object _lock = new();
    private readonly Dictionary<string, object?> _values = [];

    public int SaveCount { get; private set; }

    public T? Get<T>(string key, T? defaultValue = default)
    {
        lock (_lock) return _values.TryGetValue(key, out var v) && v is T t ? t : defaultValue;
    }

    public void Set<T>(string key, T value)
    {
        lock (_lock) _values[key] = value;
    }

    public bool Contains(string key)
    {
        lock (_lock) return _values.ContainsKey(key);
    }

    public void Remove(string key)
    {
        lock (_lock) _values.Remove(key);
    }

    public IEnumerable<string> Keys
    {
        get { lock (_lock) return _values.Keys.ToArray(); }
    }

    public void Save()
    {
        lock (_lock) SaveCount++;
    }
}

internal sealed class FakeHost : IPluginHost
{
    public FakeLogger FakeLog { get; } = new();
    public FakeSettings FakeSettings { get; } = new();
    public IPluginLogger Logger => FakeLog;
    public IPluginSettings Settings => FakeSettings;
    public string CurrentLanguage => "en";
    public Func<string, string> Translate { get; set; } = key => key;
    public string Tr(string key) => Translate(key);
    public FolderGridInfo FolderGrid => new(5, 3, 0);
    public DeviceInfo? ActiveDevice => null;
    public bool IsInExclusiveMode => false;
    public List<string> Refreshes { get; } = [];
    public List<(int Slot, string Text)> Overlays { get; } = [];
    public void RequestButtonRefresh(string commandName) { lock (Refreshes) Refreshes.Add(commandName); }
    public void ExecuteCommand(string command) { }
    public void OpenFolder(IFolderProvider provider) { }
    public bool OpenBrowser(string url) => false;
    public void OverlayTouchText(int slot, string text, TimeSpan duration) { lock (Overlays) Overlays.Add((slot, text)); }
    public int GetTouchSlotForRotary(int rotaryIndex) => rotaryIndex + 10;
    public bool RequestExclusiveMode(IExclusiveModeProvider provider) => false;
    public void ReleaseExclusiveMode(IExclusiveModeProvider provider) { }
    public IFullDisplayRenderSession? RequestFullDisplayRenderer(IFullDisplayRenderer renderer) => null;
    public IReadOnlyList<string> GetButtonStates(string commandName) => [];
    public string? GetActiveButtonState(string commandName) => null;
    public bool SetActiveButtonState(string commandName, string stateNameOrId) => false;
}
