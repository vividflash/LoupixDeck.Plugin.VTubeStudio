using LoupixDeck.Plugin.VTubeStudio.Commands;
using LoupixDeck.Plugin.VTubeStudio.Vts;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

public class ExpressionCommandTests
{
    private static ActivateExpressionCommand Activate(ExpressionRig r) => new(r.Service, r.Cache, r.Host);
    private static DeactivateExpressionCommand Deactivate(ExpressionRig r) => new(r.Service, r.Cache, r.Host);
    private static ToggleExpressionCommand Toggle(ExpressionRig r) => new(r.Service, r.Cache, r.Host);

    private static RecordedRequest LastActivation(ExpressionRig r) =>
        r.Server.Requests.Last(q => q.MessageType == "ExpressionActivationRequest");

    [Fact]
    public void Descriptors_use_the_final_names_group_template_and_targets()
    {
        var rig = new ExpressionRig();
        var expected = new (IPluginCommand Command, string Name, string Display)[]
        {
            (Activate(rig), "VTubeStudio.ActivateExpression", "Activate expression"),
            (Deactivate(rig), "VTubeStudio.DeactivateExpression", "Deactivate expression"),
            (Toggle(rig), "VTubeStudio.ToggleExpression", "Toggle expression")
        };
        foreach (var (command, name, display) in expected)
        {
            Assert.Equal(name, command.Descriptor.CommandName);
            Assert.Equal(display, command.Descriptor.DisplayName);
            Assert.Equal("VTube Studio", command.Descriptor.Group);
            if (command is ToggleExpressionCommand)
            {
                Assert.Equal("({Expression},{Icon})", command.Descriptor.ParameterTemplate);
                Assert.Equal(["Expression", "Icon"], command.Descriptor.Parameters!.Select(p => p.Name));
            }
            else
            {
                Assert.Equal("({Expression})", command.Descriptor.ParameterTemplate);
                Assert.Equal("Expression", Assert.Single(command.Descriptor.Parameters!).Name);
            }
            Assert.Equal(ButtonTargets.TouchButton | ButtonTargets.SimpleButton, command.SupportedTargets);
        }
    }

    [Fact]
    public async Task Activate_sends_active_true_by_file_name()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();

        await Activate(rig).Execute(rig.Ctx("smile"));

        var data = LastActivation(rig).Data;
        Assert.Equal("smile.exp3.json", data.GetProperty("expressionFile").GetString());
        Assert.True(data.GetProperty("active").GetBoolean());
        Assert.False(data.TryGetProperty("fadeTime", out _));
        Assert.True(rig.IsActive("smile.exp3.json"));
    }

    [Fact]
    public async Task Deactivate_sends_active_false_and_rejoins_a_name_with_a_comma()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();

        await Deactivate(rig).Execute(rig.Ctx("Angry", "very"));

        var data = LastActivation(rig).Data;
        Assert.Equal("angry.exp3.json", data.GetProperty("expressionFile").GetString());
        Assert.False(data.GetProperty("active").GetBoolean());
        Assert.False(rig.IsActive("angry.exp3.json"));
    }

    [Fact]
    public async Task Toggle_sends_the_inverse_of_the_cached_state()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();
        var toggle = Toggle(rig);

        await toggle.Execute(rig.Ctx("SMILE.exp3.json"));
        Assert.True(LastActivation(rig).Data.GetProperty("active").GetBoolean());

        await toggle.Execute(rig.Ctx("Smile"));
        Assert.False(LastActivation(rig).Data.GetProperty("active").GetBoolean());

        await toggle.Execute(rig.Ctx("angry, very"));
        Assert.False(LastActivation(rig).Data.GetProperty("active").GetBoolean());
        Assert.Equal(3, rig.Count("ExpressionActivationRequest"));
    }

    [Fact]
    public async Task Cache_is_updated_before_the_refetch_answers_then_refetched()
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

        await Activate(rig).Execute(rig.Ctx("smile"));
        await FakeVtsServer.WaitUntilAsync(() => rig.Count("ExpressionStateRequest") == initial + 1);

        Assert.True(rig.IsActive("smile.exp3.json"));
        gate.SetResult();
    }

    [Fact]
    public async Task Plugin_registers_the_commands_and_requests_refreshes_for_them()
    {
        await using var rig = new ExpressionRig();
        var plugin = new VTubeStudioPlugin();
        try
        {
            plugin.Initialize(rig.Host);
            await FakeVtsServer.WaitUntilAsync(() => plugin.Service?.Status == VtsStatus.Connected);
            await FakeVtsServer.WaitUntilAsync(() => { lock (rig.Host.Refreshes) return rig.Host.Refreshes.Contains("VTubeStudio.ToggleExpression"); });

            string[] names = ["VTubeStudio.ConnectionStatus", "VTubeStudio.ActivateExpression",
                "VTubeStudio.DeactivateExpression", "VTubeStudio.ToggleExpression", "VTubeStudio.TriggerHotkey", "VTubeStudio.TriggerItemHotkey"];
            Assert.Equal(names, plugin.GetCommands().Select(c => c.Descriptor.CommandName));
            lock (rig.Host.Refreshes) Assert.All(names, n => Assert.Contains(n, rig.Host.Refreshes));
        }
        finally
        {
            plugin.Shutdown();
            Localization.ResetForTests();
        }
    }

    [Fact]
    public async Task Unknown_expression_sends_nothing_and_flashes_no_expression()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();

        await Toggle(rig).Execute(rig.Ctx("nope"));

        Assert.Equal(0, rig.Count("ExpressionActivationRequest"));
        lock (rig.Host.Overlays) Assert.Equal([(2, "no expression")], rig.Host.Overlays);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("E "));
    }

    [Fact]
    public async Task Offline_flashes_offline()
    {
        await using var rig = new ExpressionRig();

        await Activate(rig).Execute(rig.Ctx("smile"));

        lock (rig.Host.Overlays) Assert.Equal([(2, "offline")], rig.Host.Overlays);
        Assert.Equal(0, rig.Count("ExpressionActivationRequest"));
    }

    [Fact]
    public async Task Not_authorised_flashes_not_authorised()
    {
        await using var rig = new ExpressionRig(token: null);
        rig.Service.Start();
        await FakeVtsServer.WaitUntilAsync(() => rig.Service.Status == VtsStatus.NotAuthorised);

        await Activate(rig).Execute(rig.Ctx("smile"));

        lock (rig.Host.Overlays) Assert.Equal([(2, "not authorised")], rig.Host.Overlays);
    }

    [Fact]
    public async Task Api_error_is_logged_and_flashed_as_failed()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();
        rig.Server.OnError("ExpressionActivationRequest", 652, "no model");

        await Activate(rig).Execute(rig.Ctx("smile"));

        lock (rig.Host.Overlays) Assert.Equal([(2, "Failed")], rig.Host.Overlays);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("E ") && l.Contains("VTubeStudio.ActivateExpression"));
        Assert.False(rig.IsActive("smile.exp3.json"));
    }

    [Fact]
    public async Task Toggle_draws_active_inactive_and_unknown_from_the_cache()
    {
        await using var rig = new ExpressionRig();
        var toggle = Toggle(rig);
        var canvas = new FakeCanvas();

        Assert.True(toggle.RenderImage(rig.Ctx("smile"), canvas));
        var unknown = canvas.Background;
        Assert.DoesNotContain("?", canvas.Texts);
        Assert.Equal(0, rig.Count("ExpressionStateRequest"));

        await rig.StartConnectedAsync();

        toggle.RenderImage(rig.Ctx("smile"), canvas);
        var inactive = canvas.Background;
        Assert.Equal(["Smile"], canvas.Texts);

        toggle.RenderImage(rig.Ctx("angry", "very"), canvas);
        var active = canvas.Background;
        Assert.Equal(["Angry, very"], canvas.Texts);

        Assert.NotEqual(active, inactive);

        toggle.RenderImage(rig.Ctx("nope"), canvas);
        Assert.Equal(unknown, canvas.Background);
        Assert.Contains("?", canvas.Texts);
    }

    [Fact]
    public void Toggle_override_parameter_selects_icon_none_or_stays_part_of_the_name()
    {
        Assert.Equal(("angry", "heart"), ToggleExpressionCommand.ParseParameters(["angry", "heart"]));
        Assert.Equal(("angry", "heart"), ToggleExpressionCommand.ParseParameters(["angry", "HEART"]));
        Assert.Equal(("angry", "none"), ToggleExpressionCommand.ParseParameters(["angry", "none"]));
        Assert.Equal(("angry", null), ToggleExpressionCommand.ParseParameters(["angry", ""]));
        Assert.Equal(("angry", null), ToggleExpressionCommand.ParseParameters(["angry"]));
        Assert.Equal(("angry, very", null), ToggleExpressionCommand.ParseParameters(["angry", "very"]));
        Assert.Equal(("a, b", "star"), ToggleExpressionCommand.ParseParameters(["a", "b", "star"]));
    }

    [Fact]
    public async Task Toggle_with_an_icon_parameter_still_finds_the_expression()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();

        await Toggle(rig).Execute(rig.Ctx("smile", "heart"));

        Assert.Equal("smile.exp3.json", LastActivation(rig).Data.GetProperty("expressionFile").GetString());
    }

    [Fact]
    public async Task Toggle_draws_the_matching_icon_and_falls_back_to_text()
    {
        await using var rig = new ExpressionRig();
        await rig.StartConnectedAsync();
        var toggle = Toggle(rig);
        var canvas = new FakeCanvas();

        // "angry, very" is active in the rig, "smile" is not
        toggle.RenderImage(rig.Ctx("smile", "star"), canvas);
        var off = Assert.Single(canvas.Images);
        Assert.Equal(255, off.Opacity);
        Assert.Equal(EmoteIcons.Load("star"), off.Bytes);
        Assert.Equal(["Smile"], canvas.Texts);

        toggle.RenderImage(rig.Ctx("angry", "very", "heart"), canvas);
        var on = Assert.Single(canvas.Images);
        Assert.Equal(255, on.Opacity);
        Assert.Equal(EmoteIcons.Load("heart"), on.Bytes);

        toggle.RenderImage(rig.Ctx("angry", "very"), canvas);
        Assert.Equal(EmoteIcons.Load("angry"), Assert.Single(canvas.Images).Bytes);

        toggle.RenderImage(rig.Ctx("smile", "none"), canvas);
        Assert.Empty(canvas.Images);
        Assert.Equal(["Smile"], canvas.Texts);

        toggle.RenderImage(rig.Ctx("smile"), canvas);
        Assert.Empty(canvas.Images);

        // connected but no such expression: dimmed icon, dim caption and a small question mark
        toggle.RenderImage(rig.Ctx("nope", "star"), canvas);
        var missing = Assert.Single(canvas.Images);
        Assert.Equal(IconButton.UnavailableOpacity, missing.Opacity);
        Assert.Equal(EmoteIcons.Load("star"), missing.Bytes);
        Assert.Equal(["nope", "?"], canvas.Texts);

        // no icon: the name with the question mark
        toggle.RenderImage(rig.Ctx("nope", "none"), canvas);
        Assert.Empty(canvas.Images);
        Assert.Equal(["nope", "?"], canvas.Texts);
    }

    [Fact]
    public void Toggle_not_connected_draws_the_dimmed_icon_without_a_question_mark()
    {
        var rig = new ExpressionRig();
        var toggle = Toggle(rig);
        var canvas = new FakeCanvas();

        toggle.RenderImage(rig.Ctx("EXP_angry"), canvas);
        var image = Assert.Single(canvas.Images);
        Assert.Equal(100, image.Opacity);
        Assert.Equal(EmoteIcons.Load("angry"), image.Bytes);
        Assert.Equal(["angry"], canvas.Texts);

        toggle.RenderImage(rig.Ctx("smile", "star"), canvas);
        Assert.Equal(EmoteIcons.Load("star"), Assert.Single(canvas.Images).Bytes);
        Assert.Equal(100, canvas.Images[0].Opacity);
        Assert.DoesNotContain("?", canvas.Texts);

        toggle.RenderImage(rig.Ctx("smile"), canvas);
        Assert.Empty(canvas.Images);
        Assert.Equal(["smile"], canvas.Texts);
    }

    [Theory]
    [InlineData("EXP_angry", "angry")]
    [InlineData("exp_pout_mouth", "pout mouth")]
    [InlineData("Smile", "Smile")]
    public void Caption_drops_the_exp_prefix_and_underscores(string name, string expected) =>
        Assert.Equal(expected, ToggleExpressionCommand.Caption(name));
}
