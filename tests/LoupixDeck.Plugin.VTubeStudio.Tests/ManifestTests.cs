using System.Text.Json;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

public class ManifestTests
{
    [Fact]
    public void Manifest_versions_match_the_plugin_metadata_and_the_icon_file_exists()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "LoupixDeck.Plugin.VTubeStudio.slnx"))) root = root.Parent;
        Assert.NotNull(root);

        var src = Path.Combine(root!.FullName, "src", "LoupixDeck.Plugin.VTubeStudio");
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(src, "plugin.json"))).RootElement;
        var meta = new VTubeStudioPlugin().Metadata;

        Assert.Equal(meta.Version.ToString(), manifest.GetProperty("version").GetString());
        Assert.Equal(meta.SdkVersion.ToString(), manifest.GetProperty("sdkVersion").GetString());
        Assert.True(File.Exists(Path.Combine(root.FullName, manifest.GetProperty("iconFile").GetString()!)));
    }
}
