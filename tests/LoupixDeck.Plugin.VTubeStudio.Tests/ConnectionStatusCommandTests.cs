using LoupixDeck.Plugin.VTubeStudio.Commands;
using LoupixDeck.Plugin.VTubeStudio.Vts;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

public class ConnectionStatusCommandTests
{
    private static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(50);

    private sealed class Rig : IAsyncDisposable
    {
        internal FakeVtsServer Server { get; } = new();
        internal FakeHost Host { get; } = new();
        internal VtsService Service { get; }
        internal ConnectionStatusCommand Command { get; }

        internal Rig(string? token = null, bool serverReachable = true)
        {
            Host.FakeSettings.Set(VtsService.HostKey, "127.0.0.1");
            Host.FakeSettings.Set(VtsService.PortKey, (long)(serverReachable ? Server.Uri.Port : FakeVtsServer.GetFreePort()));
            if (token is not null) Host.FakeSettings.Set(VtsService.TokenKey, token);
            Server.On("AuthenticationRequest", d => new { authenticated = d.GetProperty("authenticationToken").GetString() == "good" });
            Server.On("APIStateRequest", _ => new { active = true, vTubeStudioVersion = "1.2.3", currentSessionAuthenticated = true });
            Server.On("EventSubscriptionRequest", _ => new { subscribedEventCount = 1 });
            Server.On("CurrentModelRequest", _ => new { modelLoaded = true, modelName = "Hiyori" });
            Service = new VtsService(Host, Retry);
            Command = new ConnectionStatusCommand(Service, Host);
        }

        internal Task WaitStatusAsync(VtsStatus status) => FakeVtsServer.WaitUntilAsync(() => Service.Status == status);

        internal CommandContext Ctx(ButtonTargets target = ButtonTargets.TouchButton, int? index = 2) => new()
        {
            Parameters = [],
            Target = target,
            SourceIndex = index,
            Host = Host
        };

        public async ValueTask DisposeAsync()
        {
            Service.Dispose();
            await Server.DisposeAsync();
        }
    }

    private static (PluginColor? Background, string Icon, string Caption) Draw(Rig rig)
    {
        var canvas = new FakeCanvas();
        Assert.True(rig.Command.RenderImage(rig.Ctx(), canvas));
        var image = Assert.Single(canvas.Images);
        Assert.Equal(255, image.Opacity);
        var icon = new[] { "connected", "disconnected", "locked" }.Single(n => EmoteIcons.Load(n)!.SequenceEqual(image.Bytes));
        return (canvas.Background, icon, Assert.Single(canvas.Texts));
    }

    [Fact]
    public async Task Image_follows_the_status()
    {
        await using var rig = new Rig(serverReachable: false);
        var offline = Draw(rig);
        Assert.Equal(("disconnected", "offline"), (offline.Icon, offline.Caption));
        rig.Service.Start();
        await FakeVtsServer.WaitUntilAsync(() => rig.Host.FakeLog.Lines.Count > 0);
        Assert.Equal(("disconnected", "offline"), (Draw(rig).Icon, Draw(rig).Caption));

        await using var noToken = new Rig();
        noToken.Service.Start();
        await noToken.WaitStatusAsync(VtsStatus.NotAuthorised);
        var locked = Draw(noToken);
        Assert.Equal(("locked", "not authorised"), (locked.Icon, locked.Caption));

        await using var good = new Rig(token: "good");
        good.Service.Start();
        await good.WaitStatusAsync(VtsStatus.Connected);
        var connected = Draw(good);
        Assert.Equal(("connected", "connected"), (connected.Icon, connected.Caption));

        Assert.NotEqual(offline.Background, locked.Background);
        Assert.NotEqual(locked.Background, connected.Background);
        Assert.NotEqual(offline.Background, connected.Background);
    }

    [Fact]
    public void Status_icons_are_known_names_but_never_matched_by_keyword()
    {
        foreach (var name in new[] { "connected", "disconnected", "locked" })
        {
            Assert.True(EmoteIcons.IsKnown(name));
            Assert.Null(EmoteIcons.Match(name));
        }
    }

    [Fact]
    public void Descriptor_is_a_self_drawn_touch_button_in_the_vts_group()
    {
        var rig = new Rig();
        Assert.Equal("VTubeStudio.ConnectionStatus", rig.Command.Descriptor.CommandName);
        Assert.Equal("Connection status", rig.Command.Descriptor.DisplayName);
        Assert.Equal("VTube Studio", rig.Command.Descriptor.Group);
        Assert.Equal(ButtonTargets.TouchButton, rig.Command.SupportedTargets);
        Assert.Equal(ButtonLayoutMode.None, rig.Command.Descriptor.ButtonLayout!.Mode);
        Assert.Equal(TimeSpan.FromSeconds(5), rig.Command.UpdateInterval);
    }

    [Fact]
    public async Task Plugin_requests_a_refresh_on_status_change_and_lists_the_command()
    {
        var server = new FakeVtsServer();
        await using var _ = server;
        var host = new FakeHost();
        host.FakeSettings.Set(VtsService.HostKey, "127.0.0.1");
        host.FakeSettings.Set(VtsService.PortKey, (long)server.Uri.Port);
        host.FakeSettings.Set(VtsService.TokenKey, "good");
        server.On("AuthenticationRequest", _ => new { authenticated = true });
        server.On("APIStateRequest", _ => new { active = true, vTubeStudioVersion = "1.2.3", currentSessionAuthenticated = true });
        server.On("EventSubscriptionRequest", _ => new { subscribedEventCount = 1 });
        server.On("CurrentModelRequest", _ => new { modelLoaded = true, modelName = "Hiyori" });
        server.On("ExpressionStateRequest", _ => new { modelLoaded = true, expressions = Array.Empty<object>() });
        server.On("ItemListRequest", _ => new { itemInstancesInScene = Array.Empty<object>() });

        var plugin = new VTubeStudioPlugin();
        try
        {
            plugin.Initialize(host);
            Assert.Equal(["VTubeStudio.ConnectionStatus", "VTubeStudio.ActivateExpression", "VTubeStudio.DeactivateExpression", "VTubeStudio.ToggleExpression", "VTubeStudio.TriggerHotkey", "VTubeStudio.TriggerItemHotkey"], plugin.GetCommands().Select(c => c.Descriptor.CommandName));
            var group = Assert.Single(plugin.GetCommandGroups());
            Assert.Equal("VTube Studio", group.Group);
            Assert.Equal(CommandGroupSection.Plugins, group.Section);

            await FakeVtsServer.WaitUntilAsync(() => { lock (host.Refreshes) return host.Refreshes.Count > 0; });
            lock (host.Refreshes) Assert.Contains("VTubeStudio.ConnectionStatus", host.Refreshes);
        }
        finally
        {
            plugin.Shutdown();
            Localization.ResetForTests();
        }
    }

    [Fact]
    public async Task Press_while_not_authorised_authorises()
    {
        await using var rig = new Rig();
        rig.Server.On("AuthenticationTokenRequest", _ => new { authenticationToken = "good" });
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.NotAuthorised);

        await rig.Command.Execute(rig.Ctx());

        await rig.WaitStatusAsync(VtsStatus.Connected);
        Assert.Equal("good", rig.Host.FakeSettings.Get<string>(VtsService.TokenKey));
        Assert.Contains(rig.Server.Requests, r => r.MessageType == "AuthenticationTokenRequest");
    }

    [Fact]
    public async Task Press_while_connected_does_nothing()
    {
        await using var rig = new Rig(token: "good");
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.Connected);

        await rig.Command.Execute(rig.Ctx());

        Assert.DoesNotContain(rig.Server.Requests, r => r.MessageType == "AuthenticationTokenRequest");
    }

    [Fact]
    public async Task Exception_in_execute_is_logged_and_flashed_not_thrown()
    {
        await using var rig = new Rig();
        rig.Server.OnError("AuthenticationTokenRequest", 50, "denied");
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.NotAuthorised);

        await rig.Command.Execute(rig.Ctx(ButtonTargets.TouchButton, 3));

        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("E ") && l.Contains("VTubeStudio.ConnectionStatus"));
        lock (rig.Host.Overlays) Assert.Equal([(3, "Failed")], rig.Host.Overlays);
    }
}
