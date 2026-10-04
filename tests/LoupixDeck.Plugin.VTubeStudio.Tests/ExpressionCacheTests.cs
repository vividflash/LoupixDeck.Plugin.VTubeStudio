using System.Text.Json;
using LoupixDeck.Plugin.VTubeStudio.Commands;
using LoupixDeck.Plugin.VTubeStudio.Vts;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

/// <summary>Records draw calls; background and texts are what tests assert on.</summary>
internal sealed class FakeCanvas : IRenderCanvas
{
    public int Width => 90;
    public int Height => 90;
    public PluginColor? Background { get; private set; }
    public List<string> Texts { get; } = [];
    public List<(byte[] Bytes, int Width, int Height, byte Opacity)> Images { get; } = [];

    public void Clear(PluginColor color)
    {
        Background = color;
        Texts.Clear();
        Images.Clear();
    }

    public void FillRectangle(int x, int y, int width, int height, PluginColor color) { }
    public void DrawRectangle(int x, int y, int width, int height, int strokeWidth, PluginColor color) { }
    public void FillRoundedRectangle(int x, int y, int width, int height, int radius, PluginColor color) { }
    public void DrawRoundedRectangle(int x, int y, int width, int height, int radius, int strokeWidth, PluginColor color) { }
    public void FillCircle(int centerX, int centerY, int radius, PluginColor color) { }
    public void DrawCircle(int centerX, int centerY, int radius, int strokeWidth, PluginColor color) { }
    public void FillEllipse(int x, int y, int width, int height, PluginColor color) { }
    public void DrawEllipse(int x, int y, int width, int height, int strokeWidth, PluginColor color) { }
    public void DrawArc(int x, int y, int width, int height, float startAngle, float sweepAngle, int strokeWidth, PluginColor color) { }
    public void FillArc(int x, int y, int width, int height, float startAngle, float sweepAngle, PluginColor color) { }
    public void DrawLine(int x1, int y1, int x2, int y2, int strokeWidth, PluginColor color) { }

    public void DrawText(string text, int x, int y, int width, int height, PluginColor color, float fontSize,
        bool bold = false, bool italic = false, bool centered = true, bool outlined = false, PluginColor outlineColor = default) => Texts.Add(text);

    public void DrawText(string text, int x, int y, int width, int height, PluginColor color, float fontSize,
        TextHAlign hAlign, TextVAlign vAlign, bool bold = false, bool italic = false, bool outlined = false, PluginColor outlineColor = default) => Texts.Add(text);

    public float MeasureText(string text, float fontSize, bool bold = false, bool italic = false) => text.Length * fontSize / 2;
    public void DrawSymbol(string symbolId, int x, int y, int width, int height, PluginColor tint) { }
    public void DrawSymbol(string symbolId, int x, int y, int width, int height, SymbolStyle style) { }
    public void DrawImage(byte[] imageBytes, int x, int y, int width, int height) => Images.Add((imageBytes, width, height, 255));
    public void DrawImage(byte[] imageBytes, int x, int y, int width, int height, byte opacity, PluginColor tint = default) => Images.Add((imageBytes, width, height, opacity));
    public void PushTransform() { }
    public void PopTransform() { }
    public void Translate(float dx, float dy) { }
    public void Rotate(float degrees) { }
    public void Scale(float sx, float sy) { }
}

/// <summary>Fake VTube Studio with an expression list, a service and a cache wired to it.</summary>
internal sealed class ExpressionRig : IAsyncDisposable
{
    private static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(50);
    private readonly object _lock = new();
    private readonly List<(string Name, string File, bool Active)> _state =
    [
        ("Smile", "smile.exp3.json", false),
        ("Angry, very", "angry.exp3.json", true),
        ("Blush", "Blush.exp3.json", false)
    ];

    internal FakeVtsServer Server { get; } = new();
    internal FakeHost Host { get; } = new();
    internal VtsService Service { get; }
    internal ExpressionCache Cache { get; }
    internal int ChangedCount;

    internal ExpressionRig(string? token = "good")
    {
        Host.FakeSettings.Set(VtsService.HostKey, "127.0.0.1");
        Host.FakeSettings.Set(VtsService.PortKey, (long)Server.Uri.Port);
        if (token is not null) Host.FakeSettings.Set(VtsService.TokenKey, token);
        Server.On("AuthenticationRequest", d => new { authenticated = d.GetProperty("authenticationToken").GetString() == "good" });
        Server.On("APIStateRequest", _ => new { active = true, vTubeStudioVersion = "1.2.3", currentSessionAuthenticated = true });
        Server.On("EventSubscriptionRequest", _ => new { subscribedEventCount = 1 });
        Server.On("CurrentModelRequest", _ => new { modelLoaded = true, modelName = "Hiyori" });
        Server.On("ExpressionStateRequest", _ => StateResponse());
        Server.On("ItemListRequest", _ => new { itemInstancesInScene = Array.Empty<object>() });
        Server.On("ExpressionActivationRequest", d =>
        {
            SetState(d.GetProperty("expressionFile").GetString()!, d.GetProperty("active").GetBoolean());
            return new { };
        });
        Service = new VtsService(Host, Retry);
        Cache = new ExpressionCache(Service, Host);
        Cache.Changed += () => Interlocked.Increment(ref ChangedCount);
    }

    internal object StateResponse()
    {
        lock (_lock)
            return new
            {
                modelLoaded = true,
                expressions = _state.Select(e => new { name = e.Name, file = e.File, active = e.Active }).ToArray()
            };
    }

    internal void SetState(string file, bool active)
    {
        lock (_lock)
        {
            var i = _state.FindIndex(e => e.File == file);
            _state[i] = _state[i] with { Active = active };
        }
    }

    internal int Count(string messageType) => Server.Requests.Count(r => r.MessageType == messageType);

    internal async Task StartConnectedAsync()
    {
        Service.Start();
        await FakeVtsServer.WaitUntilAsync(() => Service.Status == VtsStatus.Connected);
        await FakeVtsServer.WaitUntilAsync(() => Cache.Expressions.Count == 3);
    }

    internal bool IsActive(string file) => Cache.Expressions.Single(e => e.File == file).Active;

    internal CommandContext Ctx(params string[] parameters) => new()
    {
        Parameters = parameters,
        Target = ButtonTargets.TouchButton,
        SourceIndex = 2,
        Host = Host
    };

    public async ValueTask DisposeAsync()
    {
        Cache.Dispose();
        Service.Dispose();
        await Server.DisposeAsync();
    }
}

public class ExpressionCacheTests
{
    [Fact]
    public async Task Loads_when_connected_and_raises_changed()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();

        Assert.Equal(["Smile", "Angry, very", "Blush"], rig.Cache.Expressions.Select(e => e.Name));
        Assert.True(rig.IsActive("angry.exp3.json"));
        Assert.False(rig.IsActive("smile.exp3.json"));
        Assert.True(rig.ChangedCount >= 1);
    }

    [Theory]
    [InlineData("smile.exp3.json", "smile.exp3.json")]
    [InlineData("SMILE.EXP3.JSON", "smile.exp3.json")]
    [InlineData("smile", "smile.exp3.json")]
    [InlineData("SmIlE", "smile.exp3.json")]
    [InlineData("blush", "Blush.exp3.json")]
    [InlineData("Angry, very", "angry.exp3.json")]
    [InlineData("  angry, VERY ", "angry.exp3.json")]
    public async Task Find_matches_file_stem_and_name_case_insensitively(string key, string expectedFile)
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();

        Assert.Equal(expectedFile, rig.Cache.Find(key)?.File);
    }

    [Fact]
    public async Task Find_returns_null_for_unknown_or_empty()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();

        Assert.Null(rig.Cache.Find("nope"));
        Assert.Null(rig.Cache.Find("  "));
    }

    [Fact]
    public async Task Refetches_on_model_loaded_event()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();
        rig.SetState("smile.exp3.json", true);

        await rig.Server.PushEventAsync("ModelLoadedEvent", new { modelLoaded = true, modelName = "Hiyori" });

        await FakeVtsServer.WaitUntilAsync(() => rig.IsActive("smile.exp3.json"));
    }

    [Fact]
    public async Task Refetches_on_hotkey_triggered_event()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();
        rig.SetState("Blush.exp3.json", true);

        await rig.Server.PushEventAsync("HotkeyTriggeredEvent", new { hotkeyAction = "ToggleExpression" });

        await FakeVtsServer.WaitUntilAsync(() => rig.IsActive("Blush.exp3.json"));
    }

    [Fact]
    public async Task Subscribes_to_hotkey_triggered_event()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();

        Assert.Contains(rig.Server.Requests, r => r.MessageType == "EventSubscriptionRequest"
            && r.Data.GetProperty("eventName").GetString() == "HotkeyTriggeredEvent"
            && r.Data.GetProperty("subscribe").GetBoolean());
    }

    [Fact]
    public async Task Changed_is_raised_only_when_the_content_changed()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();
        var before = rig.ChangedCount;

        // Same content: refetch happens, no Changed. Then a real change: exactly one Changed.
        await rig.Server.PushEventAsync("HotkeyTriggeredEvent", new { });
        rig.SetState("smile.exp3.json", true);
        await rig.Server.PushEventAsync("HotkeyTriggeredEvent", new { });

        await FakeVtsServer.WaitUntilAsync(() => rig.IsActive("smile.exp3.json"));
        Assert.Equal(before + 1, rig.ChangedCount);
    }

    [Fact]
    public async Task Cleared_when_the_connection_is_lost()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();

        rig.Server.Stop();

        await FakeVtsServer.WaitUntilAsync(() => rig.Cache.Expressions.Count == 0);
    }

    [Fact]
    public async Task Cleared_when_not_authorised_any_more()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();

        rig.Service.ForgetToken();

        await FakeVtsServer.WaitUntilAsync(() => rig.Cache.Expressions.Count == 0);
    }

    [Fact]
    public async Task Requests_during_a_running_fetch_collapse_into_one_more_fetch()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();
        var initial = rig.Count("ExpressionStateRequest");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Server.OnAsync("ExpressionStateRequest", async _ =>
        {
            await gate.Task;
            return ServerReply.Ok(rig.StateResponse());
        });

        rig.Cache.RequestRefetch();
        await FakeVtsServer.WaitUntilAsync(() => rig.Count("ExpressionStateRequest") == initial + 1);
        for (var i = 0; i < 5; i++) rig.Cache.RequestRefetch();
        gate.SetResult();
        await FakeVtsServer.WaitUntilAsync(() => rig.Count("ExpressionStateRequest") == initial + 2);

        // The cache is idle again: a new request starts a new fetch right away.
        rig.Cache.RequestRefetch();
        await FakeVtsServer.WaitUntilAsync(() => rig.Count("ExpressionStateRequest") == initial + 3);
        Assert.Equal(initial + 3, rig.Count("ExpressionStateRequest"));
    }

    [Fact]
    public async Task Api_error_on_refetch_is_swallowed_and_logged()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();
        rig.Server.OnError("ExpressionStateRequest", 600, "bad");

        rig.Cache.RequestRefetch();

        await FakeVtsServer.WaitUntilAsync(() => rig.Host.FakeLog.Lines.Any(l => l.StartsWith("W ") && l.Contains("expression list")));
        Assert.Equal(3, rig.Cache.Expressions.Count);
    }

    [Fact]
    public void Parameters_are_rejoined_with_comma_and_space()
    {
        Assert.Equal("Angry, very", ExpressionCommandBase.ExpressionFrom(["Angry", "very"]));
        Assert.Equal("smile", ExpressionCommandBase.ExpressionFrom([" smile "]));
    }
}
