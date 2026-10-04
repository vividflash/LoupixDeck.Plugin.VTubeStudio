using LoupixDeck.Plugin.VTubeStudio.Vts;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Commands;

/// <summary>Self-drawn button (status icon and short text) showing the VTube Studio connection state; pressing it while not authorised asks for a token.</summary>
internal sealed class ConnectionStatusCommand(VtsService service, IPluginHost host) : VtsCommandBase(service, host), IDisplayImageCommand
{
    public const string CommandName = Prefix + "ConnectionStatus";

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = CommandName,
        DisplayName = "Connection status",
        Group = Group,
        Description = "Shows whether VTube Studio is connected (press to authorise when not authorised)",
        ButtonLayout = new ButtonLayoutDescriptor { Mode = ButtonLayoutMode.None }
    };

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(5);

    private static readonly PluginColor ConnectedColor = new(82, 144, 112);
    private static readonly PluginColor NotAuthorisedColor = new(110, 78, 24);

    // Only reads the cached status.
    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
    {
        try
        {
            var (text, icon, background) = Service.Status switch
            {
                VtsStatus.Connected => ("connected", "connected", ConnectedColor),
                VtsStatus.NotAuthorised => ("not authorised", "locked", NotAuthorisedColor),
                _ => ("offline", "disconnected", IconButton.UnknownColor)
            };
            canvas.Clear(background);
            IconButton.Draw(canvas, Localization.Tr(text), icon, 255, PluginColor.White, bold: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected override async Task Run(CommandContext ctx)
    {
        if (Service.Status != VtsStatus.NotAuthorised) return;
        await Service.AuthoriseAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
