using System.Text.Json;
using LoupixDeck.Plugin.VTubeStudio.Vts;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

public class VtsClientTests
{
    private static CancellationToken Ct => new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

    private static async Task<VtsClient> ConnectedAsync(FakeVtsServer server, TimeSpan? timeout = null)
    {
        var client = new VtsClient(timeout);
        await client.ConnectAsync(server.Uri, Ct);
        return client;
    }

    [Fact]
    public async Task Request_sends_envelope_and_unique_ids()
    {
        await using var server = new FakeVtsServer();
        server.On("APIStateRequest", _ => new { active = true });
        server.On("NoDataRequest", _ => null);
        await using var client = await ConnectedAsync(server);

        Assert.True(client.IsConnected);
        var first = await client.RequestAsync("APIStateRequest", new { name = "x", n = 3 }, Ct);
        await client.RequestAsync("APIStateRequest", null, Ct);
        await client.RequestAsync("NoDataRequest", null, Ct);

        Assert.True(first.GetProperty("active").GetBoolean());
        var requests = server.Requests;
        Assert.Equal(3, requests.Count);
        var env = requests[0].Envelope;
        Assert.Equal("VTubeStudioPublicAPI", env.GetProperty("apiName").GetString());
        Assert.Equal("1.0", env.GetProperty("apiVersion").GetString());
        Assert.Equal("APIStateRequest", env.GetProperty("messageType").GetString());
        Assert.Equal("x", requests[0].Data.GetProperty("name").GetString());
        Assert.Equal(3, requests[0].Data.GetProperty("n").GetInt32());
        Assert.False(requests[1].Envelope.TryGetProperty("data", out _));
        Assert.Equal(3, requests.Select(r => r.RequestId).Distinct().Count());
        Assert.All(requests, r => Assert.InRange(r.RequestId.Length, 1, 64));
    }

    [Fact]
    public async Task Responses_are_matched_by_request_id_when_answered_in_reverse()
    {
        await using var server = new FakeVtsServer();
        var releaseFirst = new TaskCompletionSource();
        server.OnAsync("SlowRequest", async _ =>
        {
            await releaseFirst.Task;
            return ServerReply.Ok(new { who = "slow" });
        });
        server.On("FastRequest", _ => new { who = "fast" });
        await using var client = await ConnectedAsync(server);

        var slow = client.RequestAsync("SlowRequest", null, Ct);
        await server.WaitForRequestsAsync(1);
        var fast = await client.RequestAsync("FastRequest", null, Ct);
        Assert.False(slow.IsCompleted);
        releaseFirst.SetResult();
        var slowData = await slow;

        Assert.Equal("fast", fast.GetProperty("who").GetString());
        Assert.Equal("slow", slowData.GetProperty("who").GetString());
    }

    [Fact]
    public async Task ApiError_becomes_VtsApiException()
    {
        await using var server = new FakeVtsServer();
        server.OnError("AuthenticationTokenRequest", 50, "User has denied API access for your plugin.");
        await using var client = await ConnectedAsync(server);

        var ex = await Assert.ThrowsAsync<VtsApiException>(() =>
            client.RequestAsync("AuthenticationTokenRequest", null, Ct));

        Assert.Equal(50, ex.ErrorId);
        Assert.Equal("User has denied API access for your plugin.", ex.Message);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Unanswered_request_times_out()
    {
        await using var server = new FakeVtsServer();
        await using var client = await ConnectedAsync(server, TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<TimeoutException>(() => client.RequestAsync("SilentRequest", null, Ct));
        await Assert.ThrowsAsync<TimeoutException>(() =>
            client.RequestAsync("SilentRequest", null, Ct, TimeSpan.FromMilliseconds(50)));
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Event_message_reaches_EventReceived()
    {
        await using var server = new FakeVtsServer();
        await using var client = await ConnectedAsync(server);
        var received = new TaskCompletionSource<(string Type, JsonElement Data)>();
        client.EventReceived += (type, data) => received.TrySetResult((type, data));
        await server.WaitForConnectionsAsync(1);

        await server.PushEventAsync("ModelLoadedEvent", new { modelLoaded = true, modelName = "Hiyori" });

        var (eventType, eventData) = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("ModelLoadedEvent", eventType);
        Assert.Equal("Hiyori", eventData.GetProperty("modelName").GetString());
    }

    [Fact]
    public async Task Server_drop_fails_pending_requests_and_fires_Disconnected_once()
    {
        await using var server = new FakeVtsServer();
        server.OnAsync("HangRequest", _ => new TaskCompletionSource<ServerReply>().Task);
        await using var client = await ConnectedAsync(server);
        var disconnects = 0;
        var fired = new TaskCompletionSource();
        client.Disconnected += () =>
        {
            Interlocked.Increment(ref disconnects);
            fired.TrySetResult();
        };

        var pending = client.RequestAsync("HangRequest", null, Ct);
        await server.WaitForRequestsAsync(1);
        server.DropConnections();

        await Assert.ThrowsAnyAsync<Exception>(() => pending);
        await fired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(client.IsConnected);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.RequestAsync("HangRequest", null, Ct));
        await client.DisposeAsync();
        Assert.Equal(1, disconnects);
    }

    [Fact]
    public async Task Large_multi_frame_response_is_received_intact()
    {
        await using var server = new FakeVtsServer();
        var payload = new string('a', 200_000) + "END";
        server.On("BigRequest", _ => new { blob = payload, extra = 1 });
        await using var client = await ConnectedAsync(server);

        var data = await client.RequestAsync("BigRequest", null, Ct);

        Assert.Equal(payload, data.GetProperty("blob").GetString());
    }

    [Fact]
    public async Task Client_can_reconnect_after_server_restart()
    {
        await using var server = new FakeVtsServer();
        server.On("PingRequest", _ => new { ok = true });
        await using var client = await ConnectedAsync(server);
        var fired = new TaskCompletionSource();
        client.Disconnected += () => fired.TrySetResult();

        server.Stop();
        await fired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        server.Start();
        await client.ConnectAsync(server.Uri, Ct);

        var data = await client.RequestAsync("PingRequest", null, Ct);
        Assert.True(data.GetProperty("ok").GetBoolean());
    }
}
