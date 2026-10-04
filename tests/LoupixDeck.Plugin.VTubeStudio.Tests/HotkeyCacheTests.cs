using LoupixDeck.Plugin.VTubeStudio.Commands;
using LoupixDeck.Plugin.VTubeStudio.Vts;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

public class HotkeyCacheTests
{
    private const string Clothes = ItemRig.Clothes;

    private static TriggerHotkeyCommand Hotkey(ItemRig r) => new(r.Service, r.Hotkeys, r.Expressions, r.Host);
    private static TriggerItemHotkeyCommand ItemHotkey(ItemRig r) => new(r.Service, r.Cache, r.Hotkeys, r.Host);

    private static Task WaitOn(ItemRig rig, string id, bool on) =>
        FakeVtsServer.WaitUntilAsync(() => rig.Hotkeys.IsItemHotkeyOn(id) == on);

    [Fact]
    public async Task Loads_the_model_and_item_lists_when_connected_and_raises_changed()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        var mic = rig.Hotkeys.FindModelHotkey("LEFT_Mic");
        Assert.Equal(new HotkeyInfo("hk-mic", "LEFT_Mic", "ToggleExpression", "mic.exp3.json"), mic);
        Assert.Equal("hk-console", rig.Hotkeys.FindItemHotkey(Clothes, "console")?.HotkeyId);
        Assert.Equal("hk-hat", rig.Hotkeys.FindItemHotkey("prism_hat", "hat")?.HotkeyId);
        Assert.True(rig.HotkeysChangedCount >= 1);

        // the item request names the item file, the model request has no data
        var requests = rig.Server.Requests.Where(r => r.MessageType == "HotkeysInCurrentModelRequest").ToList();
        Assert.Contains(requests, r => !r.Data.TryGetProperty("live2DItemFileName", out _));
        Assert.Contains(requests, r => r.Data.TryGetProperty("live2DItemFileName", out var f) && f.GetString() == Clothes);
        // the PNG item has no hotkeys to ask for
        Assert.DoesNotContain(requests, r => r.Data.TryGetProperty("live2DItemFileName", out var f) && f.GetString() == "balloon.png");
    }

    [Fact]
    public async Task Lookup_is_by_name_or_id_case_insensitive_and_the_first_match_wins()
    {
        await using var rig = new ItemRig();
        rig.AddModelHotkey("hk-mic2", "left_mic", "TriggerAnimation", "x");
        await rig.StartConnectedAsync();
        await FakeVtsServer.WaitUntilAsync(() => rig.Hotkeys.FindModelHotkey("hk-mic2") is not null);

        Assert.Equal("hk-mic", rig.Hotkeys.FindModelHotkey("LEFT_MIC")?.HotkeyId);
        Assert.Equal("hk-mic", rig.Hotkeys.FindModelHotkey("HK-MIC")?.HotkeyId);
        Assert.Equal("hk-mic2", rig.Hotkeys.FindModelHotkey("hk-mic2")?.HotkeyId);
        Assert.Null(rig.Hotkeys.FindModelHotkey("nope"));
        Assert.Null(rig.Hotkeys.FindModelHotkey("  "));
        Assert.Null(rig.Hotkeys.FindItemHotkey("unknown item", "console"));
        Assert.Null(rig.Hotkeys.FindItemHotkey(Clothes, "wave"));
    }

    [Fact]
    public async Task Model_expression_hotkey_follows_the_expression_cache_and_other_types_have_no_state()
    {
        await using var rig = new ItemRig();
        var canvas = new FakeCanvas();
        await rig.StartConnectedAsync();

        Hotkey(rig).RenderImage(rig.Ctx("LEFT_Mic"), canvas);
        Assert.Equal(IconButton.InactiveColor, canvas.Background);
        Assert.Equal(255, Assert.Single(canvas.Images).Opacity);

        rig.SetExpression("mic.exp3.json", true);
        rig.Expressions.RequestRefetch();
        await FakeVtsServer.WaitUntilAsync(() => rig.Expressions.Find("mic")!.Active);

        Hotkey(rig).RenderImage(rig.Ctx("LEFT_Mic"), canvas);
        Assert.Equal(IconButton.ActiveColor, canvas.Background);
        Assert.Equal(255, Assert.Single(canvas.Images).Opacity);

        // by ID too; an animation hotkey has no state: black, full opacity
        Hotkey(rig).RenderImage(rig.Ctx("hk-mic"), canvas);
        Assert.Equal(IconButton.ActiveColor, canvas.Background);
        Hotkey(rig).RenderImage(rig.Ctx("wave", "star"), canvas);
        Assert.Equal(IconButton.InactiveColor, canvas.Background);
        Assert.Equal(255, Assert.Single(canvas.Images).Opacity);
    }

    [Fact]
    public async Task Hotkey_that_is_not_cached_yet_keeps_the_stateless_look()
    {
        await using var rig = new ItemRig();
        var canvas = new FakeCanvas();

        Assert.True(Hotkey(rig).RenderImage(rig.Ctx("LEFT_Mic"), canvas));
        Assert.Equal(IconButton.UnavailableOpacity, Assert.Single(canvas.Images).Opacity);
        await rig.StartConnectedAsync();

        Hotkey(rig).RenderImage(rig.Ctx("not a hotkey", "star"), canvas);
        Assert.Equal(IconButton.InactiveColor, canvas.Background);
        Assert.Equal(255, Assert.Single(canvas.Images).Opacity);
        ItemHotkey(rig).RenderImage(rig.Ctx("no such item", "console", "star"), canvas);
        Assert.Equal(255, Assert.Single(canvas.Images).Opacity);
        ItemHotkey(rig).RenderImage(rig.Ctx(Clothes, "no such hotkey", "star"), canvas);
        Assert.Equal(255, Assert.Single(canvas.Images).Opacity);
    }

    [Fact]
    public async Task Item_hotkey_is_off_at_start_and_flips_on_every_event_by_api_or_not()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        var canvas = new FakeCanvas();

        Assert.False(rig.Hotkeys.IsItemHotkeyOn("hk-console"));
        ItemHotkey(rig).RenderImage(rig.Ctx(Clothes, "console", "star"), canvas);
        Assert.Equal(IconButton.InactiveColor, canvas.Background);
        Assert.Equal(255, Assert.Single(canvas.Images).Opacity);

        await rig.PushHotkeyEventAsync("hk-console", byApi: true);
        await WaitOn(rig, "hk-console", true);
        ItemHotkey(rig).RenderImage(rig.Ctx(Clothes, "console", "star"), canvas);
        Assert.Equal(IconButton.ActiveColor, canvas.Background);
        Assert.Equal(255, Assert.Single(canvas.Images).Opacity);

        await rig.PushHotkeyEventAsync("hk-console", byApi: false);
        await WaitOn(rig, "hk-console", false);
        ItemHotkey(rig).RenderImage(rig.Ctx(Clothes, "console", "star"), canvas);
        Assert.Equal(IconButton.InactiveColor, canvas.Background);
    }

    [Fact]
    public async Task Item_events_of_other_hotkeys_and_model_events_do_not_flip_it()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        await rig.PushHotkeyEventAsync("hk-pen");
        await rig.PushHotkeyEventAsync("hk-unknown");
        await rig.PushHotkeyEventAsync("hk-anim", "TriggerAnimation");
        await rig.PushHotkeyEventAsync("hk-console", isItem: false);
        await rig.PushHotkeyEventAsync("hk-hat");
        await WaitOn(rig, "hk-hat", true);

        Assert.True(rig.Hotkeys.IsItemHotkeyOn("hk-pen"));
        Assert.False(rig.Hotkeys.IsItemHotkeyOn("hk-console"));
        Assert.False(rig.Hotkeys.IsItemHotkeyOn("hk-unknown"));
        Assert.False(rig.Hotkeys.IsItemHotkeyOn("hk-anim"));
    }

    [Fact]
    public async Task Remove_all_expressions_event_sets_the_states_of_that_item_to_off()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        await rig.PushHotkeyEventAsync("hk-console");
        await rig.PushHotkeyEventAsync("hk-pen");
        await rig.PushHotkeyEventAsync("hk-hat");
        await WaitOn(rig, "hk-hat", true);
        Assert.True(rig.Hotkeys.IsItemHotkeyOn("hk-console"));

        await rig.PushHotkeyEventAsync("hk-reset", "RemoveAllExpressions");

        await WaitOn(rig, "hk-console", false);
        Assert.False(rig.Hotkeys.IsItemHotkeyOn("hk-pen"));
        Assert.True(rig.Hotkeys.IsItemHotkeyOn("hk-hat"));
    }

    [Fact]
    public async Task Item_states_are_cleared_when_the_item_leaves_the_scene()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        await rig.PushHotkeyEventAsync("hk-hat");
        await rig.PushHotkeyEventAsync("hk-console");
        await WaitOn(rig, "hk-hat", true);
        await WaitOn(rig, "hk-console", true);

        rig.RemoveItem("id-hat");
        await rig.Server.PushEventAsync("ItemEvent", new { itemEventType = "Removed" });

        await WaitOn(rig, "hk-hat", false);
        Assert.True(rig.Hotkeys.IsItemHotkeyOn("hk-console"));
        Assert.Null(rig.Hotkeys.FindItemHotkey("Prism_Hat", "hat"));
    }

    [Fact]
    public async Task An_item_that_comes_back_as_a_new_instance_starts_off_again()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        await rig.PushHotkeyEventAsync("hk-hat");
        await WaitOn(rig, "hk-hat", true);

        rig.RemoveItem("id-hat");
        rig.AddItem("Prism_Hat", "id-hat-2");
        await rig.Server.PushEventAsync("ItemEvent", new { itemEventType = "Added" });

        await WaitOn(rig, "hk-hat", false);
        Assert.NotNull(rig.Hotkeys.FindItemHotkey("Prism_Hat", "hat"));
    }

    [Fact]
    public async Task Everything_is_cleared_when_the_connection_is_lost()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        await rig.PushHotkeyEventAsync("hk-console");
        await WaitOn(rig, "hk-console", true);

        rig.Server.Stop();

        await FakeVtsServer.WaitUntilAsync(() => rig.Hotkeys.FindModelHotkey("wave") is null
            && rig.Hotkeys.FindItemHotkey(Clothes, "console") is null);
        Assert.False(rig.Hotkeys.IsItemHotkeyOn("hk-console"));
        var canvas = new FakeCanvas();
        Hotkey(rig).RenderImage(rig.Ctx("LEFT_Mic"), canvas);
        Assert.Equal(IconButton.UnavailableOpacity, Assert.Single(canvas.Images).Opacity);
        Assert.Equal(["LEFT Mic"], canvas.Texts);
    }

    [Fact]
    public async Task First_item_hotkey_event_is_logged_once_with_model_id_and_name()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        await rig.PushHotkeyEventAsync("hk-console");
        await rig.PushHotkeyEventAsync("hk-pen");
        await WaitOn(rig, "hk-pen", true);

        var lines = rig.Host.FakeLog.Lines.Where(l => l.StartsWith("I ") && l.Contains("item hotkey event")).ToList();
        var line = Assert.Single(lines);
        Assert.Contains("modelID 'm-1'", line);
        Assert.Contains("modelName 'Hiyori'", line);
    }

    [Fact]
    public async Task Pressing_the_button_sends_one_request_and_does_not_flip_the_state()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        await ItemHotkey(rig).Execute(rig.Ctx(Clothes, "console"));
        await Task.Delay(200);

        var trigger = Assert.Single(rig.Server.Requests, r => r.MessageType == "HotkeyTriggerRequest");
        Assert.Equal("hk-console", trigger.Data.GetProperty("hotkeyID").GetString());
        Assert.False(rig.Hotkeys.IsItemHotkeyOn("hk-console"));

        await rig.PushHotkeyEventAsync("hk-console", byApi: true);
        await WaitOn(rig, "hk-console", true);
    }

    [Theory]
    [InlineData("ModelLoadedEvent")]
    [InlineData("ModelConfigChangedEvent")]
    public async Task Lists_are_refetched_on_model_events(string eventName)
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        rig.AddModelHotkey("hk-new", "fresh", "ToggleExpression", "fresh.exp3.json");

        await rig.Server.PushEventAsync(eventName, new { modelLoaded = true, modelName = "Hiyori", modelID = "m-1" });

        await FakeVtsServer.WaitUntilAsync(() => rig.Hotkeys.FindModelHotkey("fresh") is not null);
    }

    [Fact]
    public async Task Lists_are_refetched_when_the_item_list_changes()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        rig.AddItemHotkey("Cape", "hk-cape", "flap", "ToggleExpression", "flap.exp3.json");
        rig.AddItem("Cape", "id-cape");

        await rig.Server.PushEventAsync("ItemEvent", new { itemEventType = "Added" });

        await FakeVtsServer.WaitUntilAsync(() => rig.Hotkeys.FindItemHotkey("Cape", "flap") is not null);
    }

    [Fact]
    public async Task Subscribes_to_the_model_config_event()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        Assert.Contains(rig.Server.Requests, r => r.MessageType == "EventSubscriptionRequest"
            && r.Data.GetProperty("eventName").GetString() == "ModelConfigChangedEvent"
            && r.Data.GetProperty("subscribe").GetBoolean());
    }

    [Fact]
    public async Task Changed_is_raised_only_when_something_changed()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        await Task.Delay(100);
        var before = rig.HotkeysChangedCount;

        // same lists again, an event of an untracked hotkey: nothing changed
        await rig.Server.PushEventAsync("ModelConfigChangedEvent", new { hotkeyConfigChanged = true });
        await rig.PushHotkeyEventAsync("hk-unknown");
        var requests = rig.Server.Requests.Count(r => r.MessageType == "HotkeysInCurrentModelRequest");
        await FakeVtsServer.WaitUntilAsync(() =>
            rig.Server.Requests.Count(r => r.MessageType == "HotkeysInCurrentModelRequest") > requests);
        await Task.Delay(100);
        Assert.Equal(before, rig.HotkeysChangedCount);

        await rig.PushHotkeyEventAsync("hk-console");
        await WaitOn(rig, "hk-console", true);
        Assert.Equal(before + 1, rig.HotkeysChangedCount);
    }

    [Fact]
    public async Task Requests_during_a_running_fetch_collapse_into_one_more_fetch()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        await Task.Delay(100);
        int Model() => rig.Server.Requests.Count(r => r.MessageType == "HotkeysInCurrentModelRequest"
            && !r.Data.TryGetProperty("live2DItemFileName", out _));
        var initial = Model();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Server.OnAsync("HotkeysInCurrentModelRequest", async _ =>
        {
            await gate.Task;
            return ServerReply.Ok(new { availableHotkeys = Array.Empty<object>() });
        });

        rig.Hotkeys.RequestRefetch();
        await FakeVtsServer.WaitUntilAsync(() => Model() == initial + 1);
        for (var i = 0; i < 5; i++) rig.Hotkeys.RequestRefetch();
        gate.SetResult();

        await FakeVtsServer.WaitUntilAsync(() => Model() == initial + 2);
        await Task.Delay(100);
        Assert.Equal(initial + 2, Model());
    }

    [Fact]
    public async Task An_api_error_for_one_target_leaves_an_empty_list_and_a_warning_and_does_not_throw()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        rig.Server.OnError("HotkeysInCurrentModelRequest", 201, "no model");

        rig.Hotkeys.RequestRefetch();

        await FakeVtsServer.WaitUntilAsync(() => rig.Hotkeys.FindModelHotkey("wave") is null);
        Assert.Null(rig.Hotkeys.FindItemHotkey(Clothes, "console"));
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("W ") && l.Contains("hotkeys of"));
    }

    [Fact]
    public void Off_look_is_the_full_icon_on_black_and_on_look_is_active_color_with_the_full_icon()
    {
        var canvas = new FakeCanvas();

        IconButton.DrawState(canvas, "LEFT_Mic", null, active: false);
        Assert.Equal(IconButton.InactiveColor, canvas.Background);
        Assert.Equal(255, Assert.Single(canvas.Images).Opacity);

        IconButton.DrawState(canvas, "LEFT_Mic", null, active: true);
        Assert.Equal(IconButton.ActiveColor, canvas.Background);
        Assert.Equal(255, Assert.Single(canvas.Images).Opacity);
        Assert.NotEqual(IconButton.InactiveColor, IconButton.ActiveColor);
    }

    [Fact]
    public async Task Lookup_by_file_with_or_without_suffix_after_id_and_name()
    {
        await using var rig = new ItemRig();
        rig.AddItemHotkey(Clothes, "hk-sexy", "Sexy Outfit", "ToggleExpression", "sexy.exp3.json");
        rig.AddItemHotkey(Clothes, "hk-dance", "dance", "TriggerAnimation", "Dance.motion3.json");
        rig.AddItemHotkey(Clothes, "hk-other", "sexy", "ToggleExpression", "other.exp3.json");
        await rig.StartConnectedAsync();
        await FakeVtsServer.WaitUntilAsync(() => rig.Hotkeys.FindItemHotkey(Clothes, "hk-other") is not null);

        Assert.Equal("hk-other", rig.Hotkeys.FindItemHotkey(Clothes, "sexy")?.HotkeyId); // the name wins over the file
        Assert.Equal("hk-sexy", rig.Hotkeys.FindItemHotkey(Clothes, "sexy.exp3.json")?.HotkeyId);
        Assert.Equal("hk-sexy", rig.Hotkeys.FindItemHotkey(Clothes, "SEXY.EXP3.JSON")?.HotkeyId);
        Assert.Equal("hk-dance", rig.Hotkeys.FindItemHotkey(Clothes, "dance")?.HotkeyId);
        Assert.Equal("hk-dance", rig.Hotkeys.FindItemHotkey(Clothes, "Dance.motion3.json")?.HotkeyId);
        Assert.Equal("hk-hat", rig.Hotkeys.FindItemHotkey("Prism_Hat", "hat.exp3.json")?.HotkeyId);
        Assert.Equal("hk-mic", rig.Hotkeys.FindModelHotkey("mic")?.HotkeyId);
        Assert.Equal("hk-wave", rig.Hotkeys.FindModelHotkey("wave.motion3.json")?.HotkeyId);
        Assert.Null(rig.Hotkeys.FindItemHotkey(Clothes, "exp3.json"));
        Assert.Null(rig.Hotkeys.FindItemHotkey(Clothes, "hat"));
    }

    [Fact]
    public async Task RefreshAsync_completes_with_the_new_list()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        rig.AddItemHotkey(Clothes, "hk-late", "late", "ToggleExpression", "late.exp3.json");
        Assert.Null(rig.Hotkeys.FindItemHotkey(Clothes, "late"));

        await rig.Hotkeys.RefreshAsync();

        Assert.Equal("hk-late", rig.Hotkeys.FindItemHotkey(Clothes, "late")?.HotkeyId);
    }
}
