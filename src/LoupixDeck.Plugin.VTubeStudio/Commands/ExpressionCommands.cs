using LoupixDeck.Plugin.VTubeStudio.Vts;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Commands;

internal enum ExpressionAction
{
    Activate,
    Deactivate,
    Toggle
}

/// <summary>Shared by the three expression commands: parameter matching, the request and error texts.</summary>
internal abstract class ExpressionCommandBase : VtsCommandBase
{
    private readonly ExpressionAction _action;

    protected ExpressionCommandBase(VtsService service, ExpressionCache cache, IPluginHost host, ExpressionAction action)
        : base(service, host)
    {
        Cache = cache;
        _action = action;
    }

    protected ExpressionCache Cache { get; }

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton | ButtonTargets.SimpleButton;

    protected static CommandDescriptor Describe(string name, string displayName, string description,
        ButtonLayoutDescriptor? layout = null, bool withIcon = false) => new()
    {
        CommandName = Prefix + name,
        DisplayName = displayName,
        Group = Group,
        Description = description,
        ButtonLayout = layout,
        ParameterTemplate = withIcon ? "({Expression},{Icon})" : "({Expression})",
        Parameters = withIcon
            ? [new CommandParameter("Expression", typeof(string)) { DefaultValue = "" },
               new CommandParameter("Icon", typeof(string)) { DefaultValue = "" }]
            : [new CommandParameter("Expression", typeof(string)) { DefaultValue = "" }]
    };

    /// <summary>The host splits parameters on commas and trims them: join them back.</summary>
    internal static string ExpressionFrom(string[] parameters) => string.Join(", ", parameters).Trim();

    /// <summary>The expression name out of the command parameters.</summary>
    protected virtual string KeyFrom(string[] parameters) => ExpressionFrom(parameters);

    protected override async Task Run(CommandContext ctx)
    {
        var key = KeyFrom(ctx.Parameters);
        switch (Service.Status)
        {
            case VtsStatus.Offline:
                Fail(ctx, "offline");
                return;
            case VtsStatus.NotAuthorised:
                Fail(ctx, "not authorised");
                return;
        }

        var expression = Cache.Find(key);
        if (expression is null)
        {
            Cache.RequestRefetch();
            Fail(ctx, "no expression", $"expression '{key}' not found");
            return;
        }

        var active = _action switch
        {
            ExpressionAction.Activate => true,
            ExpressionAction.Deactivate => false,
            _ => !expression.Active
        };
        await Service.RequestAsync("ExpressionActivationRequest",
            new { expressionFile = expression.File, active }, CancellationToken.None).ConfigureAwait(false);
        Cache.SetActive(expression.File, active);
        Cache.RequestRefetch();
    }

    private void Fail(CommandContext ctx, string text, string? log = null)
    {
        Host.Logger.Error($"{Name}: {log ?? text}");
        ShowError(ctx, Localization.Tr(text));
    }
}

internal sealed class ActivateExpressionCommand(VtsService service, ExpressionCache cache, IPluginHost host)
    : ExpressionCommandBase(service, cache, host, ExpressionAction.Activate)
{
    public override CommandDescriptor Descriptor { get; } =
        Describe("ActivateExpression", "Activate expression", "Turns a model expression on");
}

internal sealed class DeactivateExpressionCommand(VtsService service, ExpressionCache cache, IPluginHost host)
    : ExpressionCommandBase(service, cache, host, ExpressionAction.Deactivate)
{
    public override CommandDescriptor Descriptor { get; } =
        Describe("DeactivateExpression", "Deactivate expression", "Turns a model expression off");
}

/// <summary>Flips an expression; the button draws itself highlighted while the expression is active.</summary>
internal sealed class ToggleExpressionCommand(VtsService service, ExpressionCache cache, IPluginHost host)
    : ExpressionCommandBase(service, cache, host, ExpressionAction.Toggle), IDisplayImageCommand
{
    public override CommandDescriptor Descriptor { get; } = Describe("ToggleExpression", "Toggle expression",
        "Turns a model expression on or off and shows whether it is on",
        new ButtonLayoutDescriptor { Mode = ButtonLayoutMode.None }, withIcon: true);

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    protected override string KeyFrom(string[] parameters) => ParseParameters(parameters).Expression;

    /// <summary>
    /// With more than one parameter, a last one that is a known icon name or "none" selects the icon
    /// (an empty one means no choice); otherwise all parameters are the expression name.
    /// </summary>
    internal static (string Expression, string? Icon) ParseParameters(string[] parameters)
    {
        var (count, icon) = IconButton.SplitIcon(parameters);
        return (ExpressionFrom(parameters[..count]), icon);
    }

    internal static string Caption(string name) => IconButton.Caption(name);

    // Only reads the caches.
    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
    {
        try
        {
            var (key, iconChoice) = ParseParameters(ctx.Parameters);
            var connected = Service.Status == VtsStatus.Connected;
            var expression = connected ? Cache.Find(key) : null;
            if (expression is null)
            {
                // Not connected: unavailable; connected without such an expression: unavailable plus "?".
                IconButton.DrawUnavailable(canvas, key, iconChoice, questionMark: connected);
                return true;
            }

            IconButton.DrawState(canvas, expression.Name, iconChoice, expression.Active);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
