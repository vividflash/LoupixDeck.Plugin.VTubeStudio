using LoupixDeck.Plugin.VTubeStudio.Vts;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Commands;

/// <summary>Shared by the hotkey commands: descriptor, parameter rejoin, the request and error texts.</summary>
internal abstract class HotkeyCommandBase : VtsCommandBase, IDisplayImageCommand
{
    private const int HotkeyNotFoundId = 202;

    protected HotkeyCommandBase(VtsService service, IPluginHost host) : base(service, host)
    {
    }

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton | ButtonTargets.SimpleButton;

    protected static CommandDescriptor Describe(string name, string displayName, string description,
        string template, params CommandParameter[] parameters) => new()
    {
        CommandName = Prefix + name,
        DisplayName = displayName,
        Group = Group,
        Description = description,
        ButtonLayout = new ButtonLayoutDescriptor { Mode = ButtonLayoutMode.None },
        ParameterTemplate = template,
        Parameters = [.. parameters]
    };

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(5);

    /// <summary>The hotkey name (without the optional icon parameter) and the chosen icon.</summary>
    protected abstract (string Hotkey, string? Icon) Parse(string[] parameters);

    /// <summary>On/off of the hotkey from the caches; null when it has no state (other type, not cached yet).</summary>
    protected abstract bool? ReadState(string[] parameters);

    // Only reads the caches. Without a state: neutral background, icon at full opacity.
    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
    {
        try
        {
            var (hotkey, iconChoice) = Parse(ctx.Parameters);
            if (Service.Status != VtsStatus.Connected)
            {
                IconButton.DrawUnavailable(canvas, hotkey, iconChoice, questionMark: false);
                return true;
            }

            if (ReadState(ctx.Parameters) is { } active)
            {
                IconButton.DrawState(canvas, hotkey, iconChoice, active);
                return true;
            }

            canvas.Clear(IconButton.InactiveColor);
            IconButton.Draw(canvas, hotkey, iconChoice, 255, PluginColor.White, bold: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected static CommandParameter Text(string name) => new(name, typeof(string)) { DefaultValue = "" };

    /// <summary>The host splits parameters on commas and trims them: join them back.</summary>
    internal static string Rejoin(IEnumerable<string> parameters) => string.Join(", ", parameters).Trim();

    /// <summary>
    /// Sends the trigger request with the hotkeyID of the cached hotkey, or the text as it is when none is cached;
    /// an API error 202 flashes "no hotkey", others are left to the base class.
    /// </summary>
    protected async Task Trigger(CommandContext ctx, string hotkey, string? itemInstanceId, HotkeyInfo? found = null)
    {
        var id = found?.HotkeyId ?? hotkey;
        object data = itemInstanceId is null
            ? new { hotkeyID = id }
            : new { hotkeyID = id, itemInstanceID = itemInstanceId };
        try
        {
            await Service.RequestAsync("HotkeyTriggerRequest", data, CancellationToken.None).ConfigureAwait(false);
        }
        catch (VtsApiException ex) when (ex.ErrorId == HotkeyNotFoundId)
        {
            Fail(ctx, "no hotkey", $"hotkey '{hotkey}' not found ({ex.Message})");
        }
    }

    /// <summary>False (after flashing the reason) when the service is offline or not authorised.</summary>
    protected bool CheckStatus(CommandContext ctx)
    {
        switch (Service.Status)
        {
            case VtsStatus.Offline:
                Fail(ctx, "offline");
                return false;
            case VtsStatus.NotAuthorised:
                Fail(ctx, "not authorised");
                return false;
            default:
                return true;
        }
    }

    protected void Fail(CommandContext ctx, string text, string? log = null)
    {
        Host.Logger.Error($"{Name}: {log ?? text}");
        ShowError(ctx, Localization.Tr(text));
    }
}

internal sealed class TriggerHotkeyCommand(VtsService service, HotkeyCache hotkeys, ExpressionCache expressions,
    IPluginHost host) : HotkeyCommandBase(service, host)
{
    public override CommandDescriptor Descriptor { get; } = Describe("TriggerHotkey", "Trigger hotkey",
        "Triggers a hotkey of the loaded model by name or ID", "({Hotkey},{Icon})", Text("Hotkey"), Text("Icon"));

    protected override (string Hotkey, string? Icon) Parse(string[] parameters) => ParseParameters(parameters);

    // An expression hotkey shows the state of its expression.
    protected override bool? ReadState(string[] parameters)
    {
        var hotkey = hotkeys.FindModelHotkey(ParseParameters(parameters).Hotkey);
        if (hotkey is not { Type: HotkeyCache.ToggleExpressionType }) return null;
        return expressions.Find(hotkey.File)?.Active;
    }

    /// <summary>A last parameter that is a known icon name or "none" selects the icon, the rest is the hotkey.</summary>
    internal static (string Hotkey, string? Icon) ParseParameters(string[] parameters)
    {
        var (count, icon) = IconButton.SplitIcon(parameters);
        return (Rejoin(parameters[..count]), icon);
    }

    protected override async Task Run(CommandContext ctx)
    {
        if (!CheckStatus(ctx)) return;
        var hotkey = ParseParameters(ctx.Parameters).Hotkey;
        if (hotkey.Length == 0)
        {
            Fail(ctx, "no hotkey", "no hotkey given");
            return;
        }

        var found = hotkeys.FindModelHotkey(hotkey);
        if (found is null)
        {
            // Hotkeys can be added while we run: look at the list once more before sending the text as it is.
            await hotkeys.RefreshAsync().ConfigureAwait(false);
            found = hotkeys.FindModelHotkey(hotkey);
        }
        await Trigger(ctx, hotkey, null, found).ConfigureAwait(false);
    }
}

internal sealed class TriggerItemHotkeyCommand(VtsService service, ItemCache cache, HotkeyCache hotkeys,
    IPluginHost host) : HotkeyCommandBase(service, host)
{
    public override CommandDescriptor Descriptor { get; } = Describe("TriggerItemHotkey", "Trigger item hotkey",
        "Triggers a hotkey of an item in the scene, the item is found by its file name or a part of it",
        "({Item},{Hotkey},{Icon})", Text("Item"), Text("Hotkey"), Text("Icon"));

    protected override (string Hotkey, string? Icon) Parse(string[] parameters)
    {
        var (_, hotkey, icon) = ParseParameters(parameters);
        return (hotkey, icon);
    }

    // An expression hotkey shows the tracked state, which starts off and flips with the hotkey events.
    protected override bool? ReadState(string[] parameters)
    {
        var (item, key, _) = ParseParameters(parameters);
        var found = cache.Find(item);
        if (found is null) return null;
        var hotkey = hotkeys.FindItemHotkey(found.FileName, key);
        if (hotkey is not { Type: HotkeyCache.ToggleExpressionType }) return null;
        return hotkeys.IsItemHotkeyOn(hotkey.HotkeyId);
    }

    /// <summary>
    /// The first parameter is the item; a last one (after at least a hotkey) that is a known icon name or
    /// "none" selects the icon; what is in between is the hotkey.
    /// </summary>
    internal static (string Item, string Hotkey, string? Icon) ParseParameters(string[] parameters)
    {
        if (parameters.Length == 0) return ("", "", null);
        var rest = parameters[1..];
        var (count, icon) = IconButton.SplitIcon(rest);
        return (parameters[0].Trim(), Rejoin(rest[..count]), icon);
    }

    protected override async Task Run(CommandContext ctx)
    {
        if (!CheckStatus(ctx)) return;
        var (item, hotkey, _) = ParseParameters(ctx.Parameters);
        if (hotkey.Length == 0)
        {
            Fail(ctx, "no hotkey", "no hotkey given");
            return;
        }

        var found = cache.Find(item);
        if (found is null)
        {
            // Item events can be missed: look at the scene once more before giving up.
            await cache.RefreshAsync().ConfigureAwait(false);
            found = cache.Find(item);
        }
        if (found is null)
        {
            Fail(ctx, "no item", $"item '{item}' not found");
            return;
        }

        var itemHotkey = hotkeys.FindItemHotkey(found.FileName, hotkey);
        if (itemHotkey is null)
        {
            await hotkeys.RefreshAsync().ConfigureAwait(false);
            itemHotkey = hotkeys.FindItemHotkey(found.FileName, hotkey);
        }
        await Trigger(ctx, hotkey, found.InstanceId, itemHotkey).ConfigureAwait(false);
    }
}
