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
    [InlineData("Swimsuit", "swimsuit")]
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
    [InlineData("Swimsuit", "swimsuit")]
    [InlineData("swim", "bikini")]
    [InlineData("EXP_white.3.", "white3")]
    public void Outfit_and_look_names_match_their_icons(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("")]
    [InlineData("EXP_smile")]
    [InlineData("hello")]
    public void No_keyword_means_no_icon(string name) => Assert.Null(EmoteIcons.Match(name));

    [Theory]
    [InlineData("EXP_eat_2", "eating")]
    [InlineData("eat2", "eating")]
    [InlineData("Eat cake", "eating")]
    [InlineData("MadFace", "angry")]
    [InlineData("EXP_MAD", "angry")]
    [InlineData("cry.exp3.json", "tears")]
    public void Eat_mad_and_cry_match_as_a_whole_word(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("great")]
    [InlineData("theatre")]
    [InlineData("made")]
    [InlineData("nomad")]
    [InlineData("crystal")]
    [InlineData("EXP_crystal_eyes")]
    public void Eat_mad_and_cry_do_not_match_inside_another_word(string name) => Assert.Null(EmoteIcons.Match(name));

    [Theory]
    [InlineData("Halo [@7MC_Official]", "halo")]
    [InlineData("Flower Hat [@7MC_Official]", "hat")]
    [InlineData("Flowery Hat (@MoshieStudio)", "hat")]
    [InlineData("Prism_Acc_witchhat_2K", "hat")]
    [InlineData("Prism_Acc_Xmashat_2K", "hat")]
    [InlineData("xmas_santa_hat (@catboymech)", "hat")]
    [InlineData("coffee", "coffee")]
    [InlineData("Green Boba", "boba")]
    [InlineData("beverage_Boba (@7MDigital)", "boba")]
    [InlineData("Black Sunglasses", "sunglasses")]
    [InlineData("sunglasses_deal_with_it (@catboymech)", "sunglasses")]
    [InlineData("BaseBall Cap [@7MC_Official]", "baseballcap")]
    [InlineData("cap", "baseballcap")]
    [InlineData("Akari Blanket (@MoshieStudio)", "blanket")]
    [InlineData("blanket_cute (@catboymech)", "blanket")]
    public void Accessory_names_match_their_icons(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("Prism_Tail_Dragon_2k", "dragontail")]
    [InlineData("Prism_Tail_Fox_2k", "foxtail")]
    [InlineData("Prism_Tail_Horse_2k", "horsetail")]
    [InlineData("Prism_Tail_Lion_2k", "liontail")]
    [InlineData("Prism_Tail_Longhair_2k", "longhairtail")]
    [InlineData("Prism_Tail_Mermaid_2k", "mermaidtail")]
    [InlineData("Prism_Tail_Ninetail_2k", "ninetail")]
    [InlineData("Prism_Tail_Raccoon_2k", "raccoontail")]
    [InlineData("Prism_Tail_Squirrel_2k", "squirreltail")]
    [InlineData("Prism_Tail_Thick_2k", "thicktail")]
    [InlineData("Prism_Tail_WhaleShark_2k", "sharktail")]
    [InlineData("Prism_Tail_cat_2k", "cattail")]
    [InlineData("cat_tail_black (@catboymech)", "cattail")]
    [InlineData("Prism_Tail_Imp_2k", "deviltail")]
    [InlineData("fox tail", "foxtail")]
    public void Tail_names_match_their_icons(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("White Bikini", "bikiniwhite")]
    [InlineData("bikini_black", "bikiniblack")]
    [InlineData("bikini blue", "bikini")]
    [InlineData("Black Swimsuit", "swimsuitblack")]
    [InlineData("swimsuit_white", "swimsuitwhite")]
    [InlineData("one piece", "swimsuit")]
    [InlineData("Red Outfit", "outfitred")]
    [InlineData("red skirt", "outfitred")]
    [InlineData("outfit black", "outfitblack")]
    [InlineData("BlackSkirt", "outfitblack")]
    [InlineData("T-Shirt", "tshirt")]
    [InlineData("Boymode", "tshirt")]
    [InlineData("hoodie on", "hoodie")]
    [InlineData("Suit", "suit")]
    [InlineData("tuxedo", "suit")]
    [InlineData("swim trunks", "trunks")]
    [InlineData("bunnysuit", "bunnyears")]
    public void Outfit_variant_names_match_their_icons(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("beverage_7Bull (@7MDigital)", "can")]
    [InlineData("beverage_Soda_Cola (@7MDigital)", "can")]
    [InlineData("beverage_Creature (@7MDigital)", "can")]
    [InlineData("beverage_water (@7MDigital)", "water")]
    [InlineData("Wine", "wine")]
    [InlineData("Dream Fizz Juice (Little Mewn)", "juice")]
    [InlineData("xmas_hot_cocoa (@7MDigital)", "cocoa")]
    [InlineData("xmas_candy_cocoa (@7MDigital)", "cocoa")]
    [InlineData("bread (@denchisoft)", "bread")]
    [InlineData("egg (by @denchisoft)", "egg")]
    [InlineData("dancing_pudding (@denchisoft)", "pudding")]
    [InlineData("xmas_candy_cane_red (@7MDigital)", "candycane")]
    public void Drink_and_food_names_match_their_icons(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("watermelon")]
    [InlineData("candle")]
    [InlineData("swine")]
    [InlineData("beggar")]
    public void Drink_and_food_keywords_do_not_match_inside_another_word(string name) => Assert.Null(EmoteIcons.Match(name));

    [Theory]
    [InlineData("bed_pink (@catboymech)", "bed")]
    [InlineData("bed_white_blanket (@catboymech)", "bed")]
    [InlineData("couch_brown (@catboymech)", "couch")]
    [InlineData("xmas_couch (@catboymech)", "couch")]
    [InlineData("gamerchair_pink_2 (@catboymech)", "gamerchair")]
    [InlineData("table (@catboymech)", "table")]
    [InlineData("tablet_blue (@7MDigital)", "tablet")]
    [InlineData("hammer (@denchisoft)", "hammer")]
    [InlineData("Charmy Keys (Little Mewn)", "keys")]
    [InlineData("xmas_lights (@7MDigital)", "xmaslights")]
    [InlineData("ANIM_xmas_lights (@7MDigital)", "xmaslights")]
    [InlineData("ANIM_headpat", "headpat")]
    [InlineData("steam (@denchisoft)", "steam")]
    [InlineData("cat_blanket_b_back (@catboymech)", "blanket")]
    public void Furniture_and_prop_names_match_their_icons(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("stable")]
    [InlineData("steamy")]
    [InlineData("bedroom")]
    [InlineData("monkey")]
    [InlineData("pattern")]
    public void Furniture_and_prop_keywords_do_not_match_inside_another_word(string name) => Assert.Null(EmoteIcons.Match(name));

    [Theory]
    [InlineData("Swimming Tube [@7MC_Official]", "swimring")]
    [InlineData("inner_tube (@catboymech)", "swimring")]
    [InlineData("Pink Heart Glasses", "heartglasses")]
    [InlineData("Black Cat Ear", "catears")]
    [InlineData("cat_ear_yellow (@catboymech)", "catears")]
    [InlineData("moustache_rainbow (@catboymech)", "moustache")]
    [InlineData("eye_patch (@denchisoft)", "eyepatch")]
    [InlineData("head_band (@denchisoft)", "headband")]
    [InlineData("helmet (@denchisoft)", "helmet")]
    [InlineData("clown_nose (@catboymech)", "clown")]
    [InlineData("pacifier (@catboymech)", "pacifier")]
    [InlineData("bandaid (@denchisoft)", "bandaid")]
    [InlineData("Prism_Acc_elfCrown_2K", "crown")]
    [InlineData("Prism_Acc_elfearring_2K", "earring")]
    [InlineData("Prism_Acc_XmasBackBow_2K", "bow")]
    [InlineData("Ribbon Bell (Little Mewn)", "bow")]
    [InlineData("Miku", "miku")]
    [InlineData("swim", "bikini")]
    [InlineData("heart", "heart")]
    public void Wearable_item_names_match_their_icons(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("rainbow")]
    [InlineData("elbow")]
    public void Wearable_item_keywords_do_not_match_inside_another_word(string name) => Assert.Null(EmoteIcons.Match(name));

    [Theory]
    [InlineData("nightcap_blue (@catboymech)", "nightcap")]
    [InlineData("nightcap_white (@catboymech)", "nightcap")]
    public void Nightcap_names_match_their_icon(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("Prism_Wing_Fairy_2k", "fairywings")]
    [InlineData("Prism_Wing_Feather_2k", "featherwings")]
    [InlineData("Prism_Wing_Imp_2k", "impwings")]
    [InlineData("Prism_Wing_Littlewings_2k", "littlewings")]
    [InlineData("Prism_Wing_Membraned_2k", "membranedwings")]
    [InlineData("Prism_Wing_Petit_2k", "petitwings")]
    [InlineData("fairy wings", "fairywings")]
    [InlineData("prism_wings_halo_2k", "halo")]
    public void Wing_names_match_their_icons(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("EXP_pen", "pen")]
    [InlineData("Mic on", "microphone")]
    [InlineData("StarEyes", "star")]
    [InlineData("stars", "star")]
    [InlineData("love", "heart")]
    [InlineData("horn", "horns")]
    [InlineData("Horns_red", "horns")]
    [InlineData("fish", "fish")]
    [InlineData("talk", "speech")]
    [InlineData("demon", "horns")]
    [InlineData("rage", "angry")]
    [InlineData("anger", "angry")]
    [InlineData("sad", "tears")]
    [InlineData("sob", "tears")]
    [InlineData("nap", "sleep")]
    [InlineData("tired", "sleep")]
    [InlineData("nom", "eating")]
    [InlineData("snack", "eating")]
    [InlineData("say", "speech")]
    [InlineData("game", "controller")]
    [InlineData("draw", "pen")]
    [InlineData("rich", "cash")]
    [InlineData("pay", "cash")]
    [InlineData("nude", "undies")]
    [InlineData("bra", "undies")]
    [InlineData("oni", "horns")]
    public void Short_synonyms_match_as_a_whole_word(string name, string icon) => Assert.Equal(icon, EmoteIcons.Match(name));

    [Theory]
    [InlineData("open")]
    [InlineData("comic")]
    [InlineData("start")]
    [InlineData("glove")]
    [InlineData("thorn")]
    [InlineData("selfish")]
    [InlineData("stalk")]
    [InlineData("demonstrate")]
    [InlineData("garage")]
    [InlineData("danger")]
    [InlineData("saddle")]
    [InlineData("snapshot")]
    [InlineData("essay")]
    [InlineData("drawer")]
    [InlineData("payload")]
    [InlineData("bracelet")]
    [InlineData("onion")]
    public void Short_synonyms_do_not_match_inside_another_word(string name) => Assert.Null(EmoteIcons.Match(name));

    [Theory]
    [InlineData("chat")]
    [InlineData("escape")]
    public void Hat_and_cap_do_not_match_inside_another_word(string name) => Assert.Null(EmoteIcons.Match(name));

    [Fact]
    public void Known_names_are_the_listed_icons()
    {
        Assert.Equal(109,EmoteIcons.Names.Count);
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
