using LoupixDeck.Plugin.VTubeStudio.Vts;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

public class SettingsPageTests
{
    private sealed class Rig : IAsyncDisposable
    {
        internal FakeVtsServer Server { get; } = new();
        internal FakeHost Host { get; } = new();
        internal VTubeStudioPlugin Plugin { get; } = new();

        internal Rig(string? token = null, int? port = null, string model = "Hiyori")
        {
            Host.FakeSettings.Set(VtsService.HostKey, "127.0.0.1");
            Host.FakeSettings.Set(VtsService.PortKey, (long)(port ?? Server.Uri.Port));
            if (token is not null) Host.FakeSettings.Set(VtsService.TokenKey, token);
            Server.On("AuthenticationRequest", d => new { authenticated = d.GetProperty("authenticationToken").GetString() == "good" });
            Server.On("APIStateRequest", _ => new { active = true, vTubeStudioVersion = "1.2.3", currentSessionAuthenticated = true });
            Server.On("EventSubscriptionRequest", _ => new { subscribedEventCount = 1 });
            Server.On("CurrentModelRequest", _ => model.Length > 0
                ? new { modelLoaded = true, modelName = model }
                : new { modelLoaded = false, modelName = "" });
            Server.On("ExpressionStateRequest", _ => new { modelLoaded = true, expressions = Array.Empty<object>() });
            Server.On("AuthenticationTokenRequest", _ => new { authenticationToken = "good" });
            Plugin.Initialize(Host);
        }

        internal string Heading => Plugin.SettingsSchema[0].Description!;

        internal Task WaitStatusAsync(VtsStatus status) => FakeVtsServer.WaitUntilAsync(() => Plugin.Service!.Status == status);

        public async ValueTask DisposeAsync()
        {
            Plugin.Shutdown();
            Localization.ResetForTests();
            await Server.DisposeAsync();
        }
    }

    private const string Offline = "VTube Studio is offline or its plugin API is disabled.";

    [Fact]
    public void Schema_has_heading_host_and_port()
    {
        var schema = new VTubeStudioPlugin().SettingsSchema;
        Assert.Equal(3, schema.Count);
        Assert.Equal(PluginSettingKind.Heading, schema[0].Kind);
        Assert.Equal(Offline, schema[0].Description);
        Assert.Equal("host", schema[1].Key);
        Assert.Equal(PluginSettingKind.Text, schema[1].Kind);
        Assert.Equal("localhost", schema[1].DefaultValue);
        Assert.Equal("port", schema[2].Key);
        Assert.Equal(PluginSettingKind.Number, schema[2].Kind);
        Assert.Equal(8001L, Convert.ToInt64(schema[2].DefaultValue));
    }

    [Fact]
    public async Task Heading_follows_the_status()
    {
        await using var off = new Rig(port: FakeVtsServer.GetFreePort());
        Assert.Equal(Offline, off.Heading);

        await using var noToken = new Rig();
        await noToken.WaitStatusAsync(VtsStatus.NotAuthorised);
        Assert.Equal("VTube Studio is running, not authorised. Press Authorise and allow the plugin in VTube Studio.", noToken.Heading);

        await using var good = new Rig(token: "good");
        await good.WaitStatusAsync(VtsStatus.Connected);
        await FakeVtsServer.WaitUntilAsync(() => good.Plugin.Service!.CurrentModelName is not null);
        Assert.Equal("Connected to VTube Studio 1.2.3, model: Hiyori", good.Heading);

        await using var bare = new Rig(token: "good", model: "");
        await bare.WaitStatusAsync(VtsStatus.Connected);
        Assert.Equal("Connected to VTube Studio 1.2.3, no model loaded", bare.Heading);
    }

    [Fact]
    public async Task Actions_throw_before_initialize_and_after_shutdown()
    {
        var plugin = new VTubeStudioPlugin();
        Assert.Equal(["Authorise", "Forget token"], plugin.SettingsActions.Select(a => a.Label));
        foreach (var action in plugin.SettingsActions)
            await Assert.ThrowsAsync<InvalidOperationException>(() => action.Invoke());
        plugin.OnSettingsSaved();

        await using var rig = new Rig(port: FakeVtsServer.GetFreePort());
        var actions = rig.Plugin.SettingsActions;
        rig.Plugin.Shutdown();
        foreach (var action in actions)
            await Assert.ThrowsAsync<InvalidOperationException>(() => action.Invoke());
        Assert.Equal(Offline, rig.Heading);
    }

    [Fact]
    public async Task Authorise_succeeds_and_stores_the_token()
    {
        await using var rig = new Rig();
        await rig.WaitStatusAsync(VtsStatus.NotAuthorised);

        var result = await rig.Plugin.SettingsActions[0].Invoke();

        Assert.Equal("Authorised.", result);
        Assert.Equal("good", rig.Host.FakeSettings.Get<string>(VtsService.TokenKey));
        Assert.Equal(VtsStatus.Connected, rig.Plugin.Service!.Status);
    }

    [Fact]
    public async Task Authorise_while_offline_throws_invalid_operation()
    {
        await using var rig = new Rig(port: FakeVtsServer.GetFreePort());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Plugin.SettingsActions[0].Invoke());
        Assert.Equal("VTube Studio is offline", ex.Message);
    }

    [Fact]
    public async Task Forget_token_removes_it()
    {
        await using var rig = new Rig(token: "good");
        await rig.WaitStatusAsync(VtsStatus.Connected);

        var result = await rig.Plugin.SettingsActions[1].Invoke();

        Assert.Equal("Token removed.", result);
        Assert.False(rig.Host.FakeSettings.Contains(VtsService.TokenKey));
        Assert.Equal(VtsStatus.NotAuthorised, rig.Plugin.Service!.Status);
    }

    [Fact]
    public async Task Changed_port_reconnects_unchanged_does_not()
    {
        await using var second = new FakeVtsServer();
        second.On("APIStateRequest", _ => new { active = true, vTubeStudioVersion = "9.9.9", currentSessionAuthenticated = true });
        await using var rig = new Rig();
        await rig.WaitStatusAsync(VtsStatus.NotAuthorised);
        Assert.Equal(1, rig.Server.ConnectionCount);

        rig.Plugin.OnSettingsSaved();
        await Task.Delay(300);
        Assert.Equal(1, rig.Server.ConnectionCount);
        Assert.Equal(VtsStatus.NotAuthorised, rig.Plugin.Service!.Status);

        rig.Host.FakeSettings.Set(VtsService.PortKey, (long)second.Uri.Port);
        rig.Plugin.OnSettingsSaved();
        await second.WaitForConnectionsAsync(1);

        var count = second.ConnectionCount;
        rig.Plugin.OnSettingsSaved();
        await Task.Delay(300);
        Assert.Equal(count, second.ConnectionCount);
    }

    [Fact]
    public void Nonsense_port_falls_back_to_8001()
    {
        var settings = new FakeSettings();
        foreach (var bad in new long[] { 0, -5, 70000 })
        {
            settings.Set(VtsService.PortKey, bad);
            Assert.Equal(8001L, VtsService.ReadPort(settings));
        }
        settings.Set(VtsService.PortKey, 9000L);
        Assert.Equal(9000L, VtsService.ReadPort(settings));
    }
}
