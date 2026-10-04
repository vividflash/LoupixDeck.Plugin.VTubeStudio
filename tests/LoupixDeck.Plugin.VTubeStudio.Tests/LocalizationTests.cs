using System.Text.Json;
using System.Text.RegularExpressions;
using LoupixDeck.Plugin.VTubeStudio.Vts;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

public class LocalizationTests : IDisposable
{
    private static readonly Regex TrCall = new(
        @"Localization\.Tr\(\s*""((?:[^""\\]|\\.)*)""\s*\)", RegexOptions.Compiled);

    private static readonly Regex Placeholder = new(@"\{\d+\}", RegexOptions.Compiled);

    public LocalizationTests() => Localization.ResetForTests();
    public void Dispose() => Localization.ResetForTests();

    private static string SrcDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "LoupixDeck.Plugin.VTubeStudio.slnx"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "src", "LoupixDeck.Plugin.VTubeStudio");
        }
    }

    private static Dictionary<string, string> Load(string code) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(SrcDir, $"strings.{code}.json")))!;

    private static void AssertTranslated(IEnumerable<string?> texts, string what)
    {
        var de = Load("de");
        var es = Load("es");
        foreach (var text in texts.Where(t => !string.IsNullOrEmpty(t)))
        {
            Assert.True(de.ContainsKey(text!), $"strings.de.json is missing ({what}): {text}");
            Assert.True(es.ContainsKey(text!), $"strings.es.json is missing ({what}): {text}");
        }
    }

    private static string[] Placeholders(string s) => Placeholder.Matches(s).Select(m => m.Value).OrderBy(v => v).ToArray();

    [Fact]
    public void German_and_Spanish_have_identical_keys_and_no_empty_values()
    {
        var de = Load("de");
        var es = Load("es");
        Assert.NotEmpty(de);
        Assert.Empty(de.Keys.Except(es.Keys));
        Assert.Empty(es.Keys.Except(de.Keys));
        Assert.All(de.Values.Concat(es.Values), v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("es")]
    public void Placeholders_match_between_key_and_value(string code)
    {
        foreach (var (key, value) in Load(code))
            Assert.True(Placeholders(key).SequenceEqual(Placeholders(value)), $"strings.{code}.json placeholders differ: {key}");
    }

    [Fact]
    public async Task Every_descriptor_string_has_a_key()
    {
        var host = new FakeHost();
        host.FakeSettings.Set(VtsService.HostKey, "127.0.0.1");
        host.FakeSettings.Set(VtsService.PortKey, (long)FakeVtsServer.GetFreePort());
        var plugin = new VTubeStudioPlugin();
        try
        {
            plugin.Initialize(host);
            var texts = new List<string?> { plugin.Metadata.Description };
            Assert.NotEmpty(plugin.GetCommands());
            foreach (var c in plugin.GetCommands()) texts.AddRange([c.Descriptor.DisplayName, c.Descriptor.Description, c.Descriptor.Group]);
            texts.AddRange(plugin.GetCommandGroups().SelectMany(g => new[] { g.Group, g.Description }));
            // The first entry is the heading whose description is the live status text.
            texts.AddRange(plugin.SettingsSchema.SelectMany((s, i) => i == 0 ? new[] { s.Label } : [s.Label, s.Description]));
            texts.AddRange(plugin.SettingsActions.Select(a => a.Label));
            AssertTranslated(texts, "descriptor");
        }
        finally
        {
            plugin.Shutdown();
        }

        await Task.CompletedTask;
    }

    [Fact]
    public void Manifest_description_has_a_key()
    {
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(SrcDir, "plugin.json")));
        AssertTranslated([manifest.RootElement.GetProperty("description").GetString()], "plugin.json");
    }

    [Fact]
    public void Every_literal_Tr_call_in_source_has_a_key()
    {
        var keys = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(SrcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            foreach (Match m in TrCall.Matches(File.ReadAllText(file))) keys.Add(m.Groups[1].Value);
        }

        Assert.NotEmpty(keys);
        AssertTranslated(keys, "Localization.Tr call");
    }

    [Fact]
    public void Indirectly_translated_texts_have_keys() =>
        // ExpressionCommandBase.Fail and HotkeyCommandBase.Fail pass these to Localization.Tr as a variable.
        AssertTranslated(["offline", "not authorised", "no expression", "no item", "no hotkey"], "indirect Tr");

    [Fact]
    public void No_host_returns_english_unchanged() => Assert.Equal("offline", Localization.Tr("offline"));

    [Fact]
    public void Tr_returns_the_translation_from_the_host()
    {
        var de = Load("de");
        var host = new FakeHost { Translate = s => de.GetValueOrDefault(s, s) };
        Localization.SetHost(host);
        Assert.Equal("verbunden", Localization.Tr("connected"));
        Assert.Equal("nicht autorisiert", Localization.Tr("not authorised"));
    }
}
