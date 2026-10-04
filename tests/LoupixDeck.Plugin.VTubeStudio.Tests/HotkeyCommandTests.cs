using LoupixDeck.Plugin.VTubeStudio.Commands;
using LoupixDeck.Plugin.VTubeStudio.Vts;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

public class HotkeyCommandTests
{
    private static TriggerHotkeyCommand Hotkey(ItemRig r) => new(r.Service, r.Hotkeys, r.Expressions, r.Host);
    private static TriggerItemHotkeyCommand ItemHotkey(ItemRig r) => new(r.Service, r.Cache, r.Hotkeys, r.Host);

    private static IReadOnlyList<RecordedRequest> Triggers(ItemRig r) =>
        r.Server.Requests.Where(q => q.MessageType == "HotkeyTriggerRequest").ToList();

    [Fact]
    public void Descriptors_use_the_final_names_group_templates_and_targets()
    {
        var rig = new ItemRig();
        var hotkey = Hotkey(rig);
        var item = ItemHotkey(rig);

        Assert.Equal("VTubeStudio.TriggerHotkey", hotkey.Descriptor.CommandName);
        Assert.Equal("Trigger hotkey", hotkey.Descriptor.DisplayName);
        Assert.Equal("({Hotkey},{Icon})", hotkey.Descriptor.ParameterTemplate);
        Assert.Equal(["Hotkey", "Icon"], hotkey.Descriptor.Parameters!.Select(p => p.Name));

        Assert.Equal("VTubeStudio.TriggerItemHotkey", item.Descriptor.CommandName);
        Assert.Equal("Trigger item hotkey", item.Descriptor.DisplayName);
        Assert.Equal("({Item},{Hotkey},{Icon})", item.Descriptor.ParameterTemplate);
        Assert.Equal(["Item", "Hotkey", "Icon"], item.Descriptor.Parameters!.Select(p => p.Name));

        foreach (var command in new IPluginCommand[] { hotkey, item })
        {
            Assert.Equal("VTube Studio", command.Descriptor.Group);
            Assert.Equal(ButtonLayoutMode.None, command.Descriptor.ButtonLayout!.Mode);
            Assert.Equal(ButtonTargets.TouchButton | ButtonTargets.SimpleButton, command.SupportedTargets);
        }
    }

    [Fact]
    public async Task TriggerHotkey_sends_the_hotkey_without_an_item_id()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        await Hotkey(rig).Execute(rig.Ctx("wave"));

        var data = Assert.Single(Triggers(rig)).Data;
        Assert.Equal("hk-wave", data.GetProperty("hotkeyID").GetString());
        Assert.False(data.TryGetProperty("itemInstanceID", out _));
        lock (rig.Host.Overlays) Assert.Empty(rig.Host.Overlays);
    }

    [Fact]
    public async Task TriggerHotkey_rejoins_a_name_with_a_comma()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        await Hotkey(rig).Execute(rig.Ctx("Hello", "world"));

        Assert.Equal("Hello, world", Assert.Single(Triggers(rig)).Data.GetProperty("hotkeyID").GetString());
    }

    [Fact]
    public async Task TriggerHotkey_with_an_empty_hotkey_flashes_no_hotkey()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        await Hotkey(rig).Execute(rig.Ctx("  "));

        Assert.Empty(Triggers(rig));
        lock (rig.Host.Overlays) Assert.Equal([(2, "no hotkey")], rig.Host.Overlays);
    }

    [Theory]
    [InlineData("Prism_Clothes_Original_2k", "id-clothes", "hk-console")]
    [InlineData("PRISM_HAT", "id-hat", "console")]
    [InlineData("balloon", "id-balloon", "console")]
    [InlineData("prism original", "id-clothes", "hk-console")]
    public async Task TriggerItemHotkey_sends_the_instance_id_of_the_matched_item(string item, string expectedId, string expectedHotkey)
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        await ItemHotkey(rig).Execute(rig.Ctx(item, "console"));

        var data = Assert.Single(Triggers(rig)).Data;
        Assert.Equal(expectedHotkey, data.GetProperty("hotkeyID").GetString());
        Assert.Equal(expectedId, data.GetProperty("itemInstanceID").GetString());
    }

    [Fact]
    public async Task TriggerItemHotkey_rejoins_the_remaining_parameters_as_the_hotkey()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        await ItemHotkey(rig).Execute(rig.Ctx("hat", "Hello", "world"));

        Assert.Equal("Hello, world", Assert.Single(Triggers(rig)).Data.GetProperty("hotkeyID").GetString());
    }

    [Fact]
    public async Task TriggerItemHotkey_refetches_once_when_the_item_is_not_cached_yet()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        var lists = rig.Count("ItemListRequest");
        rig.AddItem("Cape", "id-cape");

        await ItemHotkey(rig).Execute(rig.Ctx("cape", "flap"));

        Assert.Equal(lists + 1, rig.Count("ItemListRequest"));
        Assert.Equal("id-cape", Assert.Single(Triggers(rig)).Data.GetProperty("itemInstanceID").GetString());
    }

    [Fact]
    public async Task TriggerItemHotkey_with_a_missing_item_flashes_no_item()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        var lists = rig.Count("ItemListRequest");

        await ItemHotkey(rig).Execute(rig.Ctx("nope", "console"));

        Assert.Empty(Triggers(rig));
        Assert.Equal(lists + 1, rig.Count("ItemListRequest"));
        lock (rig.Host.Overlays) Assert.Equal([(2, "no item")], rig.Host.Overlays);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("E "));
    }

    [Fact]
    public async Task TriggerItemHotkey_with_an_empty_hotkey_flashes_no_hotkey()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        await ItemHotkey(rig).Execute(rig.Ctx("hat"));

        Assert.Empty(Triggers(rig));
        lock (rig.Host.Overlays) Assert.Equal([(2, "no hotkey")], rig.Host.Overlays);
    }

    [Fact]
    public async Task Api_error_202_flashes_no_hotkey()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        rig.Server.OnError("HotkeyTriggerRequest", 202, "HotkeyIDNotFoundInModel");

        await Hotkey(rig).Execute(rig.Ctx("nope"));
        await ItemHotkey(rig).Execute(rig.Ctx("hat", "nope"));

        lock (rig.Host.Overlays) Assert.Equal([(2, "no hotkey"), (2, "no hotkey")], rig.Host.Overlays);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("E "));
    }

    [Fact]
    public async Task Other_api_errors_flash_failed()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        rig.Server.OnError("HotkeyTriggerRequest", 201, "NoModelLoaded");

        await Hotkey(rig).Execute(rig.Ctx("wave"));

        lock (rig.Host.Overlays) Assert.Equal([(2, "Failed")], rig.Host.Overlays);
        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("E ") && l.Contains("VTubeStudio.TriggerHotkey"));
    }

    [Fact]
    public async Task Offline_sends_nothing_and_flashes_offline()
    {
        await using var rig = new ItemRig();

        await Hotkey(rig).Execute(rig.Ctx("wave"));
        await ItemHotkey(rig).Execute(rig.Ctx("hat", "wave"));

        lock (rig.Host.Overlays) Assert.Equal([(2, "offline"), (2, "offline")], rig.Host.Overlays);
        Assert.Equal(0, rig.Count("HotkeyTriggerRequest"));
        Assert.Equal(0, rig.Count("ItemListRequest"));
    }

    [Fact]
    public async Task Not_authorised_sends_nothing_and_flashes_not_authorised()
    {
        await using var rig = new ItemRig(token: null);
        rig.Service.Start();
        await FakeVtsServer.WaitUntilAsync(() => rig.Service.Status == VtsStatus.NotAuthorised);

        await Hotkey(rig).Execute(rig.Ctx("wave"));
        await ItemHotkey(rig).Execute(rig.Ctx("hat", "wave"));

        lock (rig.Host.Overlays) Assert.Equal([(2, "not authorised"), (2, "not authorised")], rig.Host.Overlays);
        Assert.Equal(0, rig.Count("HotkeyTriggerRequest"));
    }

    [Fact]
    public void Icon_parameter_is_split_off_the_hotkey_for_both_commands()
    {
        Assert.Equal(("wave", null), TriggerHotkeyCommand.ParseParameters(["wave"]));
        Assert.Equal(("wave", "heart"), TriggerHotkeyCommand.ParseParameters(["wave", "HEART"]));
        Assert.Equal(("wave", "none"), TriggerHotkeyCommand.ParseParameters(["wave", "none"]));
        Assert.Equal(("Hello, world", null), TriggerHotkeyCommand.ParseParameters(["Hello", "world"]));
        Assert.Equal(("a, b", "pen"), TriggerHotkeyCommand.ParseParameters(["a", "b", "pen"]));

        Assert.Equal(("item", "console", null), TriggerItemHotkeyCommand.ParseParameters(["item", "console"]));
        Assert.Equal(("item", "console", "star"), TriggerItemHotkeyCommand.ParseParameters(["item", "console", "star"]));
        Assert.Equal(("item", "a, b", "none"), TriggerItemHotkeyCommand.ParseParameters(["item", "a", "b", "none"]));
        Assert.Equal(("item", "pen", null), TriggerItemHotkeyCommand.ParseParameters(["item", "pen"]));
    }

    [Fact]
    public async Task The_icon_parameter_is_not_sent_to_vts()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        await Hotkey(rig).Execute(rig.Ctx("wave", "heart"));
        await ItemHotkey(rig).Execute(rig.Ctx("PRISM_HAT", "console", "none"));
        await ItemHotkey(rig).Execute(rig.Ctx("PRISM_HAT", "a", "b", "star"));

        Assert.Equal(["hk-wave", "console", "a, b"],
            Triggers(rig).Select(t => t.Data.GetProperty("hotkeyID").GetString()));
    }

    [Fact]
    public async Task Hotkey_buttons_draw_icon_or_text_and_go_dark_when_not_connected()
    {
        await using var rig = new ItemRig();
        var canvas = new FakeCanvas();
        foreach (var command in new HotkeyCommandBase[] { Hotkey(rig), ItemHotkey(rig) })
        {
            Assert.Equal(TimeSpan.FromSeconds(5), command.UpdateInterval);
            Assert.Equal(ButtonLayoutMode.None, command.Descriptor.ButtonLayout!.Mode);
        }

        // not connected: the icon dimmed with a caption, no question mark
        Assert.True(Hotkey(rig).RenderImage(rig.Ctx("LEFT_Mic"), canvas));
        var image0 = Assert.Single(canvas.Images);
        Assert.Equal(100, image0.Opacity);
        Assert.Equal(EmoteIcons.Load("microphone"), image0.Bytes);
        Assert.Equal(["LEFT Mic"], canvas.Texts);

        ItemHotkey(rig).RenderImage(rig.Ctx("PRISM_HAT", "console_L1"), canvas);
        Assert.Equal(100, Assert.Single(canvas.Images).Opacity);
        Assert.Equal(["console L1"], canvas.Texts);

        // without an icon: only the name
        Hotkey(rig).RenderImage(rig.Ctx("wave"), canvas);
        Assert.Empty(canvas.Images);
        Assert.Equal(["wave"], canvas.Texts);

        await rig.StartConnectedAsync();

        Hotkey(rig).RenderImage(rig.Ctx("MIC_extra"), canvas);
        var image = Assert.Single(canvas.Images);
        Assert.Equal(255, image.Opacity);
        Assert.Equal(EmoteIcons.Load("microphone"), image.Bytes);
        Assert.Equal(["MIC extra"], canvas.Texts);

        ItemHotkey(rig).RenderImage(rig.Ctx("PRISM_HAT", "console_L1"), canvas);
        Assert.Equal(EmoteIcons.Load("controller"), Assert.Single(canvas.Images).Bytes);
        Assert.Equal(["console L1"], canvas.Texts);

        Hotkey(rig).RenderImage(rig.Ctx("wave"), canvas);
        Assert.Empty(canvas.Images);
        Assert.Equal(["wave"], canvas.Texts);

        Hotkey(rig).RenderImage(rig.Ctx("wave", "star"), canvas);
        Assert.Equal(EmoteIcons.Load("star"), Assert.Single(canvas.Images).Bytes);

        Hotkey(rig).RenderImage(rig.Ctx("LEFT_Mic", "none"), canvas);
        Assert.Empty(canvas.Images);
        Assert.Equal(["LEFT_Mic"], canvas.Texts);

        ItemHotkey(rig).RenderImage(rig.Ctx("PRISM_HAT", "wave", "heart"), canvas);
        Assert.Equal(EmoteIcons.Load("heart"), Assert.Single(canvas.Images).Bytes);
        Assert.Equal(["wave"], canvas.Texts);
    }

    [Fact]
    public async Task TriggerItemHotkey_sends_the_cached_hotkey_id_when_found_by_file_or_name()
    {
        await using var rig = new ItemRig();
        rig.AddItemHotkey(ItemRig.Clothes, "hk-sexy", "Outfit 1", "ToggleExpression", "sexy.exp3.json");
        await rig.StartConnectedAsync();
        await FakeVtsServer.WaitUntilAsync(() => rig.Hotkeys.FindItemHotkey(ItemRig.Clothes, "sexy") is not null);

        await ItemHotkey(rig).Execute(rig.Ctx(ItemRig.Clothes, "sexy"));
        await ItemHotkey(rig).Execute(rig.Ctx(ItemRig.Clothes, "console"));

        var triggers = Triggers(rig);
        Assert.Equal(2, triggers.Count);
        Assert.Equal("hk-sexy", triggers[0].Data.GetProperty("hotkeyID").GetString());
        Assert.Equal("id-clothes", triggers[0].Data.GetProperty("itemInstanceID").GetString());
        Assert.Equal("hk-console", triggers[1].Data.GetProperty("hotkeyID").GetString());
    }

    [Fact]
    public async Task TriggerHotkey_sends_the_cached_hotkey_id_when_found_by_file()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        await Hotkey(rig).Execute(rig.Ctx("wave.motion3.json"));

        Assert.Equal("hk-wave", Assert.Single(Triggers(rig)).Data.GetProperty("hotkeyID").GetString());
    }

    [Fact]
    public async Task A_hotkey_that_is_not_cached_is_fetched_once_and_then_sent_as_the_cached_id()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        rig.AddItemHotkey(ItemRig.Clothes, "hk-school", "x", "ToggleExpression", "basic school girl.exp3.json");
        var before = rig.Count("HotkeysInCurrentModelRequest");

        await ItemHotkey(rig).Execute(rig.Ctx(ItemRig.Clothes, "basic school girl"));

        Assert.Equal("hk-school", Assert.Single(Triggers(rig)).Data.GetProperty("hotkeyID").GetString());
        Assert.Equal(before + 3, rig.Count("HotkeysInCurrentModelRequest")); // model + two Live2D items, once
    }

    [Fact]
    public async Task A_hotkey_that_stays_unknown_is_refetched_once_and_sent_as_it_is()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        var before = rig.Count("HotkeysInCurrentModelRequest");

        await ItemHotkey(rig).Execute(rig.Ctx(ItemRig.Clothes, "nothing"));
        await Hotkey(rig).Execute(rig.Ctx("nothing"));

        var triggers = Triggers(rig);
        Assert.Equal(2, triggers.Count);
        Assert.All(triggers, t => Assert.Equal("nothing", t.Data.GetProperty("hotkeyID").GetString()));
        Assert.Equal(before + 6, rig.Count("HotkeysInCurrentModelRequest"));
    }
}
