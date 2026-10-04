using LoupixDeck.Plugin.VTubeStudio.Commands;
using LoupixDeck.Plugin.VTubeStudio.Vts;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio;

public sealed class VTubeStudioPlugin : LoupixPlugin, IPluginSettingsPage
{
    private IPluginHost? _host;
    private VtsService? _service;
    private ExpressionCache? _cache;
    private ItemCache? _itemCache;
    private HotkeyCache? _hotkeyCache;
    private IReadOnlyList<IPluginCommand> _commands = [];
    private CancellationTokenSource? _shutdownCts;
    private string _appliedHost = "localhost";
    private long _appliedPort = VtsService.DefaultPort;

    public override PluginMetadata Metadata { get; } = new()
    {
        Id = "vtubestudio",
        Name = "VTube Studio",
        Version = new Version(1, 0, 0),
        SdkVersion = new Version(1, 28, 0),
        Author = "vividflash",
        Description = "Controls VTube Studio through its plugin API.",
    };

    public override void Initialize(IPluginHost host)
    {
        _host = host;
        Localization.SetHost(host);
        var service = new VtsService(host);
        _service = service;
        var cache = new ExpressionCache(service, host);
        _cache = cache;
        var itemCache = new ItemCache(service, host);
        _itemCache = itemCache;
        var hotkeyCache = new HotkeyCache(service, itemCache, host);
        _hotkeyCache = hotkeyCache;
        _commands =
        [
            new ConnectionStatusCommand(service, host),
            new ActivateExpressionCommand(service, cache, host),
            new DeactivateExpressionCommand(service, cache, host),
            new ToggleExpressionCommand(service, cache, host),
            new TriggerHotkeyCommand(service, hotkeyCache, cache, host),
            new TriggerItemHotkeyCommand(service, itemCache, hotkeyCache, host)
        ];
        service.StatusChanged += RequestRefresh;
        cache.Changed += RequestRefresh;
        itemCache.Changed += RequestRefresh;
        hotkeyCache.Changed += RequestHotkeyRefresh;
        _shutdownCts = new CancellationTokenSource();
        _appliedHost = VtsService.ReadHost(host.Settings);
        _appliedPort = VtsService.ReadPort(host.Settings);
        service.Start();
    }

    internal VtsService? Service => _service;

    public override void Shutdown()
    {
        if (_shutdownCts is { } cts)
        {
            _shutdownCts = null;
            try
            {
                cts.Cancel();
            }
            finally
            {
                cts.Dispose();
            }
        }

        if (_cache is { } cache)
        {
            cache.Changed -= RequestRefresh;
            cache.Dispose();
        }

        if (_hotkeyCache is { } hotkeyCache)
        {
            hotkeyCache.Changed -= RequestHotkeyRefresh;
            hotkeyCache.Dispose();
        }

        if (_itemCache is { } itemCache)
        {
            itemCache.Changed -= RequestRefresh;
            itemCache.Dispose();
        }

        if (_service is { } service)
        {
            service.StatusChanged -= RequestRefresh;
            service.Dispose();
        }

        _cache = null;
        _itemCache = null;
        _hotkeyCache = null;
        _service = null;
        _commands = [];
        Localization.SetHost(null);
        _host = null;
    }

    private void RequestRefresh()
    {
        foreach (var command in _commands) _host?.RequestButtonRefresh(command.Descriptor.CommandName);
    }

    private void RequestHotkeyRefresh()
    {
        foreach (var command in _commands)
        {
            if (command is HotkeyCommandBase) _host?.RequestButtonRefresh(command.Descriptor.CommandName);
        }
    }

    public override IEnumerable<IPluginCommand> GetCommands() => _commands;

    public override IReadOnlyList<CommandGroupDescriptor> GetCommandGroups() =>
    [
        new CommandGroupDescriptor
        {
            Group = VtsCommandBase.Group,
            Description = "VTube Studio connection and model control",
            Section = CommandGroupSection.Plugins
        }
    ];

    public IReadOnlyList<PluginSettingDescriptor> SettingsSchema =>
    [
        new PluginSettingDescriptor
        {
            Key = "__heading_status",
            Label = "Connection",
            Kind = PluginSettingKind.Heading,
            Description = StatusText(),
            DefaultValue = string.Empty
        },
        new PluginSettingDescriptor
        {
            Key = VtsService.HostKey,
            Label = "Host",
            Kind = PluginSettingKind.Text,
            Description = "Computer VTube Studio runs on. Use localhost for the same computer.",
            DefaultValue = "localhost"
        },
        new PluginSettingDescriptor
        {
            Key = VtsService.PortKey,
            Label = "Port",
            Kind = PluginSettingKind.Number,
            Description = "Port of the VTube Studio plugin API (default 8001).",
            DefaultValue = VtsService.DefaultPort
        }
    ];

    public IReadOnlyList<PluginSettingAction> SettingsActions =>
    [
        new PluginSettingAction
        {
            Label = "Authorise",
            Invoke = async () =>
            {
                var service = _service ?? throw new InvalidOperationException(Localization.Tr("VTube Studio is offline"));
                var ct = _shutdownCts?.Token ?? CancellationToken.None;
                await service.AuthoriseAsync(ct);
                return Localization.Tr("Authorised.");
            }
        },
        new PluginSettingAction
        {
            Label = "Forget token",
            Invoke = () =>
            {
                var service = _service ?? throw new InvalidOperationException(Localization.Tr("VTube Studio is offline"));
                service.ForgetToken();
                return Task.FromResult(Localization.Tr("Token removed."));
            }
        }
    ];

    public void OnSettingsSaved()
    {
        if (_host is null || _service is not { } service) return;
        var host = VtsService.ReadHost(_host.Settings);
        var port = VtsService.ReadPort(_host.Settings);
        if (host == _appliedHost && port == _appliedPort) return;
        _appliedHost = host;
        _appliedPort = port;
        service.Reconnect();
    }

    private string StatusText()
    {
        if (_service is not { } service) return Localization.Tr("VTube Studio is offline or its plugin API is disabled.");
        switch (service.Status)
        {
            case VtsStatus.Connected:
                var version = service.VTubeStudioVersion ?? "?";
                return service.CurrentModelName is { } model
                    ? string.Format(Localization.Tr("Connected to VTube Studio {0}, model: {1}"), version, model)
                    : string.Format(Localization.Tr("Connected to VTube Studio {0}, no model loaded"), version);
            case VtsStatus.NotAuthorised:
                return Localization.Tr("VTube Studio is running, not authorised. Press Authorise and allow the plugin in VTube Studio.");
            default:
                return Localization.Tr("VTube Studio is offline or its plugin API is disabled.");
        }
    }
}
