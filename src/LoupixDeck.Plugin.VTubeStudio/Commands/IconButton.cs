using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Commands;

/// <summary>The drawing shared by the buttons with an icon: icon on top, caption below, text-only fallback.</summary>
internal static class IconButton
{
    // Black like an empty button, so an icon does not sit on a coloured tile.
    internal static readonly PluginColor InactiveColor = new(0, 0, 0);
    internal static readonly PluginColor UnknownColor = new(0, 0, 0);
    internal static readonly PluginColor DimText = new(160, 160, 160);
    private const int CaptionHeight = 20;

    internal static readonly PluginColor ActiveColor = new(82, 144, 112);
    internal const byte UnavailableOpacity = 100; // dimming only means "unavailable"; on and off are told apart by colour

    /// <summary>
    /// With more than one parameter, a last one that is a known icon name or "none" selects the icon
    /// (an empty one means no choice). Returns how many leading parameters belong to the name.
    /// </summary>
    internal static (int Count, string? Icon) SplitIcon(string[] parameters)
    {
        if (parameters.Length > 1)
        {
            var last = parameters[^1].Trim();
            if (last.Length == 0) return (parameters.Length - 1, null);
            if (last.Equals(EmoteIcons.None, StringComparison.OrdinalIgnoreCase))
                return (parameters.Length - 1, EmoteIcons.None);
            if (EmoteIcons.IsKnown(last)) return (parameters.Length - 1, last.ToLowerInvariant());
        }

        return (parameters.Length, null);
    }

    /// <summary>The caption under the icon: no leading "EXP_", underscores as spaces.</summary>
    internal static string Caption(string name)
    {
        if (name.StartsWith("EXP_", StringComparison.OrdinalIgnoreCase)) name = name[4..];
        return name.Replace('_', ' ').Trim();
    }

    /// <summary>
    /// The "unavailable" look on black: the icon dimmed with a dim caption, or only the name when there is no
    /// icon. With a question mark (the thing does not exist while connected) a small "?" sits in the top-right
    /// corner, or below the name when there is no icon.
    /// </summary>
    internal static void DrawUnavailable(IRenderCanvas canvas, string name, string? iconChoice, bool questionMark)
    {
        canvas.Clear(InactiveColor);
        if (IconBytes(name, iconChoice) is not null)
        {
            Draw(canvas, name, iconChoice, UnavailableOpacity, DimText, bold: false);
            if (questionMark)
                canvas.DrawText("?", canvas.Width - 20, 2, 18, 18, DimText, 14f, TextHAlign.Center, TextVAlign.Middle);
            return;
        }

        if (!questionMark)
        {
            canvas.DrawText(name, 2, 2, canvas.Width - 4, canvas.Height - 4, DimText, 13f,
                TextHAlign.Center, TextVAlign.Middle, bold: true);
            return;
        }

        canvas.DrawText(name, 2, 4, canvas.Width - 4, canvas.Height / 2 - 4, DimText, 13f,
            TextHAlign.Center, TextVAlign.Middle, bold: true);
        canvas.DrawText("?", 2, canvas.Height / 2, canvas.Width - 4, canvas.Height / 2 - 4, DimText, 20f,
            TextHAlign.Center, TextVAlign.Middle);
    }

    private static byte[]? IconBytes(string name, string? iconChoice)
    {
        var icon = iconChoice ?? EmoteIcons.Match(name);
        return icon is null || icon == EmoteIcons.None ? null : EmoteIcons.Load(icon);
    }

    /// <summary>
    /// The on/off look of a toggle: green with the full icon and a bold caption when on, black with the
    /// full icon when off.
    /// </summary>
    internal static void DrawState(IRenderCanvas canvas, string name, string? iconChoice, bool active)
    {
        canvas.Clear(active ? ActiveColor : InactiveColor);
        Draw(canvas, name, iconChoice, (byte)255, PluginColor.White, bold: active);
    }

    /// <summary>
    /// Icon (chosen or matched by keyword) with the caption below it; the name as plain text when there is
    /// no icon. The caller has cleared the canvas.
    /// </summary>
    internal static void Draw(IRenderCanvas canvas, string name, string? iconChoice, byte opacity,
        PluginColor captionColor, bool bold)
    {
        var bytes = IconBytes(name, iconChoice);
        if (bytes is not null)
        {
            var size = canvas.Width * 5 / 8;
            var x = (canvas.Width - size) / 2;
            // Icon and caption are centred together.
            var y = Math.Max(0, (canvas.Height - size - CaptionHeight) / 2);
            if (opacity == 255) canvas.DrawImage(bytes, x, y, size, size);
            else canvas.DrawImage(bytes, x, y, size, size, opacity);
            var top = y + size;
            canvas.DrawText(Caption(name), 2, top, canvas.Width - 4, CaptionHeight,
                captionColor, 11f, TextHAlign.Center, TextVAlign.Middle, bold: bold);
            return;
        }

        canvas.DrawText(name, 2, 2, canvas.Width - 4, canvas.Height - 4, PluginColor.White, 15f,
            TextHAlign.Center, TextVAlign.Middle, bold: bold);
    }
}
