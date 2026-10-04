using System.Collections.Concurrent;

namespace LoupixDeck.Plugin.VTubeStudio.Commands;

/// <summary>The built-in icons of the expression buttons, picked by keyword in the expression name.</summary>
internal static class EmoteIcons
{
    public const string None = "none";

    /// <summary>Most specific first: the first keyword contained in the normalised name wins.</summary>
    private static readonly (string Keyword, string Icon)[] Keywords =
    [
        ("angryshy", "angryshy"),
        ("nosebubble", "nosebubble"),
        ("sleepbubble", "sleepbubble"),
        ("cartoonsweat", "sweat"),
        ("realisticsweat", "sweatdrops"),
        ("fish", "fish"),
        ("white3", "white3"),
        ("3mouth", "mouth3"),
        ("pleading", "pleading"),
        ("sulking", "sulking"),
        ("cash", "cash"),
        ("money", "cash"),
        ("dollar", "cash"),
        ("heart", "heart"),
        ("love", "heart"),
        ("angry", "angry"),
        ("mad", "angry"),
        ("blush", "shy"),
        ("shy", "shy"),
        ("star", "star"),
        ("sparkle", "star"),
        ("tears", "tears"),
        ("cry", "tears"),
        ("swirly", "swirly"),
        ("dizzy", "swirly"),
        ("xd", "xd"),
        ("dx", "xd"),
        ("avoid", "avoid"),
        ("pout", "pout"),
        ("sweat", "sweat"),
        ("sleep", "sleep"),
        ("zzz", "sleep"),
        ("loading", "loading"),
        ("speech", "speech"),
        ("talk", "speech"),
        ("eating", "eating"),
        ("eat", "eating"),
        ("food", "eating"),
        ("console", "controller"),
        ("controller", "controller"),
        ("gamepad", "controller"),
        ("gaming", "controller"),
        ("microphone", "microphone"),
        ("mic", "microphone"),
        ("pen", "pen"),
        ("stylus", "pen"),
        ("pencil", "pen"),
        ("basicwhite", "outfit"),
        ("outfit", "outfit"),
        ("clothes", "outfit"),
        ("sexy", "heels"),
        ("heels", "heels"),
        ("school", "school"),
        ("seifuku", "school"),
        ("uniform", "school"),
        ("sdemon", "deviltail"),
        ("succubus", "deviltail"),
        ("tail", "deviltail"),
        ("demon", "horns"),
        ("devil", "horns"),
        ("horn", "horns"),
        ("kitsune", "foxears"),
        ("kittsune", "foxears"),
        ("fox", "foxears"),
        ("wolf", "foxears"),
        ("bunny", "bunnyears"),
        ("rabbit", "bunnyears"),
        ("naked", "undies"),
        ("underwear", "undies"),
        ("undies", "undies"),
        ("bikini", "bikini"),
        ("swimsuit", "bikini"),
        ("swim", "bikini")
    ];

    public static IReadOnlyList<string> Names { get; } =
    [
        "cash", "heart", "angry", "angryshy", "shy", "star", "tears", "pleading", "sulking", "swirly", "xd", "avoid",
        "mouth3", "pout", "sweat", "sweatdrops", "nosebubble", "sleepbubble", "sleep", "loading", "speech", "eating",
        "fish", "white3", "controller", "microphone", "pen",
        "outfit", "heels", "school", "foxears", "bunnyears", "deviltail", "horns", "undies", "bikini",
        // status icons of the connection button: no keywords, only reachable through the icon parameter
        "connected", "disconnected", "locked"
    ];

    private static readonly ConcurrentDictionary<string, byte[]?> Cache = new();

    /// <summary>The icon for an expression name, or null when no keyword matches.</summary>
    public static string? Match(string expressionName)
    {
        var name = new string(expressionName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        foreach (var (keyword, icon) in Keywords)
            if (name.Contains(keyword, StringComparison.Ordinal)) return icon;
        return null;
    }

    public static bool IsKnown(string iconName) =>
        Names.Contains(iconName.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>The PNG bytes of an icon, read once from the embedded resources; null when missing.</summary>
    public static byte[]? Load(string iconName) =>
        Cache.GetOrAdd(iconName.Trim().ToLowerInvariant(), static name =>
        {
            using var stream = typeof(EmoteIcons).Assembly.GetManifestResourceStream($"Icons/{name}.png");
            if (stream is null) return null;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        });
}
