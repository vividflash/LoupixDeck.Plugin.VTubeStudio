using System.Buffers.Binary;
using LoupixDeck.Plugin.VTubeStudio.Commands;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

public class EmoteIconTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Theory]
    [InlineData("EXP_CASH", "cash")]
    [InlineData("EXP_Heart", "heart")]
    [InlineData("EXP_angry", "angry")]
    [InlineData("EXP_angryshy", "angryshy")]
    [InlineData("EXP_SHY", "shy")]
    [InlineData("EXP_STAR", "star")]
    [InlineData("EXP_tears", "tears")]
    [InlineData("EXP_pleading", "pleading")]
    [InlineData("EXP_sulking", "sulking")]
    [InlineData("EXP_swirly", "swirly")]
    [InlineData("EXP_XD", "xd")]
    [InlineData("EXP_avoid", "avoid")]
    [InlineData("EXP_3mouth", "mouth3")]
    [InlineData("EXP_pout_mouth", "pout")]
    [InlineData("EXP_cartoonsweat", "sweat")]
    [InlineData("EXP_realisticsweat", "sweatdrops")]
    [InlineData("EXP_nose_bubble", "nosebubble")]
    [InlineData("EXP_Sleepbubble", "sleepbubble")]
    [InlineData("EXP_sleep_-w-", "sleep")]
    [InlineData("EXP_Loading..", "loading")]
    [InlineData("EXP_speechmesh", "speech")]
    [InlineData("EXP_eating_motion", "eating")]
    [InlineData("EXP_eating_fishPHY", "fish")]
    [InlineData("bikini", "bikini")]
    [InlineData("Swimsuit", "bikini")]
    [InlineData("swim", "bikini")]
    [InlineData("EXP_white.3.", "white3")]
    public void Matches_the_owners_expression_names(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("angry_shy", "angryshy")]
    [InlineData("Angry Shy", "angryshy")]
    [InlineData("fish_eating", "fish")]
    [InlineData("nose bubble sleep", "nosebubble")]
    [InlineData("sleep_bubble", "sleepbubble")]
    [InlineData("cartoon_sweat", "sweat")]
    [InlineData("realistic-sweat", "sweatdrops")]
    public void More_specific_keywords_win(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("love", "heart")]
    [InlineData("money", "cash")]
    [InlineData("dollar", "cash")]
    [InlineData("cry", "tears")]
    [InlineData("blush", "shy")]
    [InlineData("sparkle", "star")]
    [InlineData("sweat", "sweat")]
    [InlineData("zzz", "sleep")]
    [InlineData("mad", "angry")]
    [InlineData("dizzy", "swirly")]
    [InlineData("eat", "eating")]
    [InlineData("food", "eating")]
    [InlineData("talk", "speech")]
    public void Synonyms_match(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("console", "controller")]
    [InlineData("console_L1", "controller")]
    [InlineData("gamepad", "controller")]
    [InlineData("Gaming", "controller")]
    [InlineData("controller", "controller")]
    [InlineData("LEFT_Mic", "microphone")]
    [InlineData("microphone", "microphone")]
    [InlineData("LEFT_PEN_ASMR", "pen")]
    [InlineData("PEN_Click", "pen")]
    [InlineData("stylus", "pen")]
    [InlineData("pencil", "pen")]
    [InlineData("EXP_DX", "xd")]
    public void Hotkey_names_match_the_new_icons(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("sexy", "heels")]
    [InlineData("basic school girl", "school")]
    [InlineData("basic white", "outfit")]
    [InlineData("basicwhite", "outfit")]
    [InlineData("Outfit_2", "outfit")]
    [InlineData("clothes", "outfit")]
    [InlineData("heels", "heels")]
    [InlineData("seifuku", "school")]
    [InlineData("uniform", "school")]
    [InlineData("001kittsune", "foxears")]
    [InlineData("001_kitsune", "foxears")]
    [InlineData("white wolf", "foxears")]
    [InlineData("fox", "foxears")]
    [InlineData("001_demon", "horns")]
    [InlineData("devil", "horns")]
    [InlineData("horn", "horns")]
    [InlineData("001_s_demon", "deviltail")]
    [InlineData("succubus", "deviltail")]
    [InlineData("tail", "deviltail")]
    [InlineData("Pink Bunny Ear", "bunnyears")]
    [InlineData("rabbit", "bunnyears")]
    [InlineData("naked", "undies")]
    [InlineData("underwear", "undies")]
    [InlineData("undies", "undies")]
    [InlineData("bikini", "bikini")]
    [InlineData("Swimsuit", "bikini")]
    [InlineData("swim", "bikini")]
    [InlineData("EXP_white.3.", "white3")]
    public void Outfit_and_look_names_match_their_icons(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("")]
    [InlineData("EXP_smile")]
    [InlineData("hello")]
    public void No_keyword_means_no_icon(string name) => Assert.Null(EmoteIcons.Match(name));

    [Fact]
    public void Known_names_are_the_listed_icons()
    {
        Assert.Equal(39, EmoteIcons.Names.Count);
        Assert.Contains("pen", EmoteIcons.Names);
        Assert.Contains("controller", EmoteIcons.Names);
        Assert.Contains("microphone", EmoteIcons.Names);
        Assert.True(EmoteIcons.IsKnown("heart"));
        Assert.True(EmoteIcons.IsKnown("Heart"));
        Assert.False(EmoteIcons.IsKnown("none"));
        Assert.False(EmoteIcons.IsKnown("smile"));
    }

    [Fact]
    public void Every_icon_resource_exists_and_is_a_144x144_png()
    {
        foreach (var name in EmoteIcons.Names)
        {
            var png = EmoteIcons.Load(name);
            Assert.True(png is not null, $"missing icon {name}");
            Assert.True(png!.AsSpan(0, 8).SequenceEqual(PngSignature), name);
            Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
            Assert.Equal(144, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
            Assert.Equal(144, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        }
    }

    [Fact]
    public void Load_is_cached_and_null_for_a_missing_icon()
    {
        Assert.Null(EmoteIcons.Load("nope"));
        Assert.Same(EmoteIcons.Load("cash"), EmoteIcons.Load("cash"));
    }
}
