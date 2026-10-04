using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace LoupixDeck.Plugin.VTubeStudio.Commands;

/// <summary>The built-in icons of the expression buttons, picked by keyword in the expression name.</summary>
internal static class EmoteIcons
{
    public const string None = "none";

    /// <summary>Most specific first: the first keyword contained in the normalised name wins.</summary>
    private static readonly (string Keyword, string Icon)[] Keywords =
    [
        ("swimmingtube", "swimring"),
        ("innertube", "swimring"),
        ("swimring", "swimring"),
        ("heartglasses", "heartglasses"),
        ("bed", "bed"),
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
        ("redoutfit", "outfitred"),
        ("outfitred", "outfitred"),
        ("redskirt", "outfitred"),
        ("blackoutfit", "outfitblack"),
        ("outfitblack", "outfitblack"),
        ("blackskirt", "outfitblack"),
        ("basicwhite", "outfit"),
        ("outfit", "outfit"),
        ("clothes", "outfit"),
        ("sexy", "heels"),
        ("heels", "heels"),
        ("school", "school"),
        ("seifuku", "school"),
        ("uniform", "school"),
        ("taildragon", "dragontail"),
        ("dragontail", "dragontail"),
        ("tailfox", "foxtail"),
        ("foxtail", "foxtail"),
        ("tailhorse", "horsetail"),
        ("horsetail", "horsetail"),
        ("taillion", "liontail"),
        ("liontail", "liontail"),
        ("taillonghair", "longhairtail"),
        ("longhairtail", "longhairtail"),
        ("tailmermaid", "mermaidtail"),
        ("mermaidtail", "mermaidtail"),
        ("mermaid", "mermaidtail"),
        ("tailninetail", "ninetail"),
        ("ninetail", "ninetail"),
        ("ninetails", "ninetail"),
        ("tailraccoon", "raccoontail"),
        ("raccoontail", "raccoontail"),
        ("tailsquirrel", "squirreltail"),
        ("squirreltail", "squirreltail"),
        ("tailthick", "thicktail"),
        ("thicktail", "thicktail"),
        ("tailwhaleshark", "sharktail"),
        ("whaleshark", "sharktail"),
        ("sharktail", "sharktail"),
        ("tailcat", "cattail"),
        ("cattail", "cattail"),
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
        ("whitebikini", "bikiniwhite"),
        ("bikiniwhite", "bikiniwhite"),
        ("blackbikini", "bikiniblack"),
        ("bikiniblack", "bikiniblack"),
        ("bikini", "bikini"),
        ("whiteswimsuit", "swimsuitwhite"),
        ("swimsuitwhite", "swimsuitwhite"),
        ("blackswimsuit", "swimsuitblack"),
        ("swimsuitblack", "swimsuitblack"),
        ("swimsuit", "swimsuit"),
        ("onepiece", "swimsuit"),
        ("trunks", "trunks"),
        ("swim", "bikini"),
        ("halo", "halo"),
        ("witchhat", "hat"),
        ("xmashat", "hat"),
        ("santahat", "hat"),
        ("hat", "hat"),
        ("coffee", "coffee"),
        ("boba", "boba"),
        ("bubbletea", "boba"),
        ("sunglasses", "sunglasses"),
        ("baseballcap", "baseballcap"),
        ("cap", "baseballcap"),
        ("blanket", "blanket"),
        ("nightcap", "nightcap"),
        ("wingfairy", "fairywings"),
        ("fairywing", "fairywings"),
        ("wingfeather", "featherwings"),
        ("featherwing", "featherwings"),
        ("wingimp", "impwings"),
        ("impwing", "impwings"),
        ("littlewing", "littlewings"),
        ("wingmembraned", "membranedwings"),
        ("membranedwing", "membranedwings"),
        ("wingpetit", "petitwings"),
        ("petitwing", "petitwings"),
        ("stars", "star"),
        ("horns", "horns"),
        ("rage", "angry"),
        ("anger", "angry"),
        ("sad", "tears"),
        ("sob", "tears"),
        ("nap", "sleep"),
        ("tired", "sleep"),
        ("nom", "eating"),
        ("snack", "eating"),
        ("say", "speech"),
        ("game", "controller"),
        ("draw", "pen"),
        ("rich", "cash"),
        ("pay", "cash"),
        ("nude", "undies"),
        ("bra", "undies"),
        ("oni", "horns"),
        ("tshirt", "tshirt"),
        ("boymode", "tshirt"),
        ("hoodie", "hoodie"),
        ("tuxedo", "suit"),
        ("suit", "suit"),
        ("cocoa", "cocoa"),
        ("hotchocolate", "cocoa"),
        ("candycane", "candycane"),
        ("water", "water"),
        ("wine", "wine"),
        ("juice", "juice"),
        ("bread", "bread"),
        ("pudding", "pudding"),
        ("egg", "egg"),
        ("soda", "can"),
        ("cola", "can"),
        ("energydrink", "can"),
        ("can", "can"),
        ("beverage", "can"),
        ("couch", "couch"),
        ("sofa", "couch"),
        ("gamerchair", "gamerchair"),
        ("gamingchair", "gamerchair"),
        ("tablet", "tablet"),
        ("table", "table"),
        ("hammer", "hammer"),
        ("mallet", "hammer"),
        ("keys", "keys"),
        ("key", "keys"),
        ("xmaslights", "xmaslights"),
        ("lights", "xmaslights"),
        ("headpat", "headpat"),
        ("pat", "headpat"),
        ("steam", "steam"),
        ("catear", "catears"),
        ("moustache", "moustache"),
        ("mustache", "moustache"),
        ("eyepatch", "eyepatch"),
        ("headband", "headband"),
        ("helmet", "helmet"),
        ("clown", "clown"),
        ("pacifier", "pacifier"),
        ("bandaid", "bandaid"),
        ("crown", "crown"),
        ("earring", "earring"),
        ("ribbon", "bow"),
        ("bow", "bow"),
        ("miku", "miku")
    ];

    public static IReadOnlyList<string> Names { get; } =
    [
        "cash", "heart", "angry", "angryshy", "shy", "star", "tears", "pleading", "sulking", "swirly", "xd", "avoid",
        "mouth3", "pout", "sweat", "sweatdrops", "nosebubble", "sleepbubble", "sleep", "loading", "speech", "eating",
        "fish", "white3", "controller", "microphone", "pen",
        "outfit", "heels", "school", "foxears", "bunnyears", "deviltail", "horns", "undies", "bikini",
        "bikiniwhite", "bikiniblack", "swimsuit", "swimsuitwhite", "swimsuitblack", "outfitred", "outfitblack",
        "tshirt", "hoodie", "suit", "trunks",
        "halo", "hat", "coffee", "boba", "sunglasses", "baseballcap", "blanket",
        "dragontail", "foxtail", "horsetail", "liontail", "longhairtail", "mermaidtail", "ninetail", "raccoontail",
        "squirreltail", "thicktail", "sharktail", "cattail", "nightcap",
        "fairywings", "featherwings", "impwings", "littlewings", "membranedwings", "petitwings",
        "can", "water", "wine", "juice", "cocoa", "bread", "egg", "pudding", "candycane",
        "bed", "couch", "gamerchair", "table", "tablet", "hammer", "keys", "xmaslights", "headpat", "steam",
        "swimring", "heartglasses", "catears", "moustache", "eyepatch", "headband", "helmet", "clown", "pacifier", "bandaid", "crown", "earring", "bow", "miku",
        // status icons of the connection button: no keywords, only reachable through the icon parameter
        "connected", "disconnected", "locked"
    ];

    /// <summary>Keywords that are part of too many other words: they only match a whole word of the name.</summary>
    private static readonly HashSet<string> WholeWordOnly =
    [
        "eat", "mad", "cry", "hat", "cap", "pen", "mic", "star", "love", "horn", "fish", "talk", "demon",
        "rage", "anger", "sad", "sob", "nap", "tired", "nom", "snack", "say", "game", "draw", "rich", "pay", "nude", "bra", "oni", "suit",
        "water", "wine", "egg", "can",
        "bed", "table", "keys", "key", "lights", "pat", "steam",
        "bow"
    ];

    /// <summary>A word of a name: letters up to the next non-letter or the next lower-to-upper case change.</summary>
    private static readonly Regex Word = new("[A-Z]?[a-z]+|[A-Z]+(?![a-z])", RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<string, byte[]?> Cache = new();

    /// <summary>The icon for an expression name, or null when no keyword matches.</summary>
    public static string? Match(string expressionName)
    {
        var name = new string(expressionName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        HashSet<string>? words = null;
        foreach (var (keyword, icon) in Keywords)
        {
            if (WholeWordOnly.Contains(keyword))
            {
                words ??= Word.Matches(expressionName).Select(m => m.Value.ToLowerInvariant()).ToHashSet();
                if (words.Contains(keyword)) return icon;
            }
            else if (name.Contains(keyword, StringComparison.Ordinal)) return icon;
        }
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
