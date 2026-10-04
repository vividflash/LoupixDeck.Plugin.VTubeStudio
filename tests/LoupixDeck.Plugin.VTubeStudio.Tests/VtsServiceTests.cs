using System.Text.Json;
using LoupixDeck.Plugin.VTubeStudio.Vts;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

public class VtsServiceTests
{
    private static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(50);

    private static CancellationToken Ct => new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

    private sealed class Rig : IAsyncDisposable
    {
        internal FakeVtsServer Server { get; } = new();
        internal FakeHost Host { get; } = new();
        internal VtsService Service { get; }
        internal List<VtsStatus> Statuses { get; } = [];

        internal Rig(string? token = null, bool answerAuth = true, bool useServerPort = true)
        {
            Host.FakeSettings.Set(VtsService.HostKey, "127.0.0.1");
            Host.FakeSettings.Set(VtsService.PortKey, (long)(useServerPort ? Server.Uri.Port : FakeVtsServer.GetFreePort()));
            if (token is not null) Host.FakeSettings.Set(VtsService.TokenKey, token);
            if (answerAuth) Server.On("AuthenticationRequest", d => new { authenticated = d.GetProperty("authenticationToken").GetString() == "good" });
            Server.On("APIStateRequest", _ => new { active = true, vTubeStudioVersion = "1.2.3", currentSessionAuthenticated = true });
            Server.On("EventSubscriptionRequest", _ => new { subscribedEventCount = 1 });
            Server.On("CurrentModelRequest", _ => new { modelLoaded = true, modelName = "Hiyori" });
            Service = new VtsService(Host, Retry);
            Service.StatusChanged += () => { lock (Statuses) Statuses.Add(Service.Status); };
        }

        internal Task WaitStatusAsync(VtsStatus status) =>
            FakeVtsServer.WaitUntilAsync(() => Service.Status == status);

        internal string? Token => Host.FakeSettings.Get<string>(VtsService.TokenKey);

        public async ValueTask DisposeAsync()
        {
            Service.Dispose();
            await Server.DisposeAsync();
        }
    }

    [Fact]
    public async Task No_server_is_offline_and_server_appearing_connects()
    {
        await using var rig = new Rig(token: "good", useServerPort: false);
        rig.Service.Start();
        await FakeVtsServer.WaitUntilAsync(() => rig.Host.FakeLog.Lines.Count > 0);
        Assert.Equal(VtsStatus.Offline, rig.Service.Status);
        // The failed attempt is logged once per offline period, not every retry.
        await Task.Delay(300);
        Assert.Single(rig.Host.FakeLog.Lines, l => l.Contains("not reachable"));

        rig.Host.FakeSettings.Set(VtsService.PortKey, (long)rig.Server.Uri.Port);
        await rig.WaitStatusAsync(VtsStatus.Connected);
    }

    [Fact]
    public async Task No_token_is_not_authorised_and_requests_no_token()
    {
        await using var rig = new Rig();
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.NotAuthorised);
        await Task.Delay(100);

        Assert.DoesNotContain(rig.Server.Requests, r => r.MessageType is "AuthenticationTokenRequest" or "AuthenticationRequest");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.RequestAsync("X", null, Ct));
        Assert.Contains("Not authorised", ex.Message);
    }

    [Fact]
    public async Task AuthoriseAsync_stores_token_and_connects()
    {
        await using var rig = new Rig();
        rig.Server.On("AuthenticationTokenRequest", _ => new { authenticationToken = "good" });
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.NotAuthorised);

        await rig.Service.AuthoriseAsync(Ct);

        Assert.Equal(VtsStatus.Connected, rig.Service.Status);
        Assert.Equal("good", rig.Token);
        Assert.True(rig.Host.FakeSettings.SaveCount >= 1);
        var tokenRequest = rig.Server.Requests.Single(r => r.MessageType == "AuthenticationTokenRequest");
        Assert.Equal("LoupixDeck", tokenRequest.Data.GetProperty("pluginName").GetString());
        Assert.Equal("vividflash", tokenRequest.Data.GetProperty("pluginDeveloper").GetString());
        var icon = tokenRequest.Data.GetProperty("pluginIcon").GetString();
        Assert.False(string.IsNullOrEmpty(icon));
        using var embedded = typeof(VtsService).Assembly.GetManifestResourceStream(VtsService.IconResource)!;
        using var ms = new MemoryStream();
        embedded.CopyTo(ms);
        Assert.Equal(ms.ToArray(), Convert.FromBase64String(icon!));
        var auth = rig.Server.Requests.Single(r => r.MessageType == "AuthenticationRequest");
        Assert.Equal("good", auth.Data.GetProperty("authenticationToken").GetString());
    }

    [Fact]
    public async Task AuthoriseAsync_throws_when_denied_or_offline()
    {
        await using var rig = new Rig();
        rig.Server.OnError("AuthenticationTokenRequest", 50, "denied");
        var offline = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.AuthoriseAsync(Ct));
        Assert.Contains("offline", offline.Message);

        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.NotAuthorised);
        var denied = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.AuthoriseAsync(Ct));
        Assert.Contains("denied", denied.Message);
        Assert.Null(rig.Token);
        Assert.Equal(VtsStatus.NotAuthorised, rig.Service.Status);
    }

    [Fact]
    public async Task Accepted_token_connects_and_reads_version_and_model()
    {
        await using var rig = new Rig(token: "good");
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.Connected);

        Assert.Equal("1.2.3", rig.Service.VTubeStudioVersion);
        Assert.Equal("Hiyori", rig.Service.CurrentModelName);
        var subs = rig.Server.Requests.Where(r => r.MessageType == "EventSubscriptionRequest").ToArray();
        Assert.Equal(["ModelLoadedEvent", "HotkeyTriggeredEvent", "ItemEvent", "ModelConfigChangedEvent"], subs.Select(s => s.Data.GetProperty("eventName").GetString()));
        Assert.All(subs, s => Assert.True(s.Data.GetProperty("subscribe").GetBoolean()));

        var answer = await rig.Service.RequestAsync("APIStateRequest", null, Ct);
        Assert.True(answer.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task Rejected_token_is_removed()
    {
        await using var rig = new Rig(token: "bad");
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.NotAuthorised);

        Assert.Null(rig.Token);
        Assert.True(rig.Host.FakeSettings.SaveCount >= 1);
        // The loop does not retry the rejected token or ask for a new one.
        await Task.Delay(150);
        Assert.Single(rig.Server.Requests, r => r.MessageType == "AuthenticationRequest");
        Assert.DoesNotContain(rig.Server.Requests, r => r.MessageType == "AuthenticationTokenRequest");
    }

    [Fact]
    public async Task Model_events_update_name_and_pass_through()
    {
        await using var rig = new Rig(token: "good");
        var passed = new List<string>();
        rig.Service.EventReceived += (type, _) => { lock (passed) passed.Add(type); };
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.Connected);

        var changes = 0;
        rig.Service.StatusChanged += () => Interlocked.Increment(ref changes);
        await rig.Server.PushEventAsync("ModelLoadedEvent", new { modelLoaded = true, modelName = "Akari", modelID = "x" });
        await FakeVtsServer.WaitUntilAsync(() => rig.Service.CurrentModelName == "Akari");
        await rig.Server.PushEventAsync("ModelLoadedEvent", new { modelLoaded = false, modelName = "", modelID = "" });
        await FakeVtsServer.WaitUntilAsync(() => rig.Service.CurrentModelName is null);

        Assert.Equal(2, changes);
        Assert.Equal(2, passed.Count(t => t == "ModelLoadedEvent"));
    }

    [Fact]
    public async Task Drop_goes_offline_then_reconnects_with_stored_token()
    {
        await using var rig = new Rig(token: "good");
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.Connected);

        rig.Server.Stop();
        await rig.WaitStatusAsync(VtsStatus.Offline);
        Assert.Null(rig.Service.CurrentModelName);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.RequestAsync("X", null, Ct));
        Assert.Contains("offline", ex.Message);

        rig.Server.Start();
        await rig.WaitStatusAsync(VtsStatus.Connected);
        Assert.Equal(2, rig.Server.Requests.Count(r => r.MessageType == "AuthenticationRequest"));
        Assert.All(rig.Server.Requests.Where(r => r.MessageType == "AuthenticationRequest"),
            r => Assert.Equal("good", r.Data.GetProperty("authenticationToken").GetString()));
        Assert.Equal("Hiyori", rig.Service.CurrentModelName);
    }

    [Fact]
    public async Task Reconnect_drops_connection_and_connects_again()
    {
        await using var rig = new Rig(token: "good");
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.Connected);

        rig.Service.Reconnect();
        await FakeVtsServer.WaitUntilAsync(() => rig.Server.Requests.Count(r => r.MessageType == "AuthenticationRequest") == 2);
        await rig.WaitStatusAsync(VtsStatus.Connected);
    }

    [Fact]
    public async Task ForgetToken_removes_token_and_drops_to_not_authorised()
    {
        await using var rig = new Rig(token: "good");
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.Connected);

        rig.Service.ForgetToken();

        Assert.Null(rig.Token);
        Assert.Equal(VtsStatus.NotAuthorised, rig.Service.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.RequestAsync("X", null, Ct));
    }

    [Fact]
    public async Task StatusChanged_fires_for_each_change_and_not_under_lock()
    {
        await using var rig = new Rig(token: "good");
        // Reading state from the handler would deadlock if the service raised it while locked.
        var seen = new List<(VtsStatus, string?)>();
        rig.Service.StatusChanged += () => { lock (seen) seen.Add((rig.Service.Status, rig.Service.CurrentModelName)); };
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.Connected);
        rig.Server.DropConnections();
        await FakeVtsServer.WaitUntilAsync(() => { lock (seen) return seen.Any(s => s.Item1 == VtsStatus.Offline); });
        await rig.WaitStatusAsync(VtsStatus.Connected);

        lock (seen)
        {
            Assert.Contains(seen, s => s.Item1 == VtsStatus.Connected);
            Assert.Contains(seen, s => s.Item1 == VtsStatus.Offline);
        }
    }

    [Fact]
    public async Task Dispose_ends_the_loop_and_closes_the_connection()
    {
        await using var rig = new Rig(token: "good");
        rig.Service.Start();
        await rig.WaitStatusAsync(VtsStatus.Connected);
        await rig.Server.WaitForConnectionsAsync(1);

        rig.Service.Dispose();

        Assert.Equal(VtsStatus.Offline, rig.Service.Status);
        await FakeVtsServer.WaitUntilAsync(() => rig.Server.ConnectionCount == 0);
        await Task.Delay(250);
        Assert.Equal(0, rig.Server.ConnectionCount);
        Assert.Equal(1, rig.Server.Requests.Count(r => r.MessageType == "AuthenticationRequest"));
    }
}
