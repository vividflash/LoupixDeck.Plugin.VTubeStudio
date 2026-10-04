using LoupixDeck.Plugin.VTubeStudio.Vts;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Commands;

/// <summary>
/// Common plumbing for every VTube Studio command: the service, error wrapping (a thrown
/// exception never reaches the host) and a short error text on the pressed button.
/// </summary>
internal abstract class VtsCommandBase : IPluginCommand
{
    public const string Group = "VTube Studio";
    public const string Prefix = "VTubeStudio.";
    internal static readonly TimeSpan OverlayDuration = TimeSpan.FromMilliseconds(1500);

    protected VtsCommandBase(VtsService service, IPluginHost host)
    {
        Service = service;
        Host = host;
    }

    protected VtsService Service { get; }
    protected IPluginHost Host { get; }

    public abstract CommandDescriptor Descriptor { get; }

    public virtual ButtonTargets SupportedTargets => ButtonTargets.All;

    public string Name => Descriptor.CommandName;

    public async Task Execute(CommandContext ctx)
    {
        try
        {
            await Run(ctx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Host.Logger.Error($"{Name}: failed", ex);
            ShowError(ctx, Localization.Tr("Failed"));
        }
    }

    /// <summary>The command's action. Exceptions are logged and flashed as "Failed".</summary>
    protected abstract Task Run(CommandContext ctx);

    /// <summary>Flashes a short text on the touch slot of the pressed touch button or the one next to the pressed dial. Best effort.</summary>
    protected void ShowError(CommandContext ctx, string text)
    {
        try
        {
            if (ctx.SourceIndex is not { } index) return;
            var slot = ctx.Target switch
            {
                ButtonTargets.TouchButton => index,
                ButtonTargets.RotaryEncoder => Host.GetTouchSlotForRotary(index),
                _ => -1
            };
            if (slot >= 0) Host.OverlayTouchText(slot, text, OverlayDuration);
        }
        catch
        {
            // feedback is best effort
        }
    }
}
