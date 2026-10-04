using System.Text.Json;
using LoupixDeck.PluginSdk;
using LoupixDeck.Plugin.VTubeStudio.Vts;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

/// <summary>Fake VTube Studio with an item list and a hotkey trigger, a service and an item cache wired to it.</summary>
internal sealed class ItemRig : IAsyncDisposable
{
    private static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(50);
    private readonly object _lock = new();
    private readonly List<(string File, string Id, string Type)> _items =
    [
        ("Prism_Clothes_Original_2k", "id-clothes", "Live2D"),
        ("Prism_Hat", "id-hat", "Live2D"),
        ("balloon.png", "id-balloon", "PNG")
    ];

    internal const string Clothes = "Prism_Clothes_Original_2k";

    private readonly List<(string Id, string Name, string Type, string File)> _modelHotkeys =
    [
        ("hk-wave", "wave", "TriggerAnimation", "wave.motion3.json"),
        ("hk-mic", "LEFT_Mic", "ToggleExpression", "mic.exp3.json")
    ];

    private readonly Dictionary<string, List<(string Id, string Name, string Type, string File)>> _itemHotkeys = new()
    {
        [Clothes] =
        [
            ("hk-console", "console", "ToggleExpression", "console.exp3.json"),
            ("hk-pen", "LEFT_PEN", "ToggleExpression", "pen.exp3.json"),
            ("hk-reset", "reset", "RemoveAllExpressions", ""),
            ("hk-anim", "anim", "TriggerAnimation", "a.motion3.json")
        ],
        ["Prism_Hat"] = [("hk-hat", "hat", "ToggleExpression", "hat.exp3.json")]
    };

    private readonly List<(string File, bool Active)> _expressions = [("mic.exp3.json", false)];

    internal FakeVtsServer Server { get; } = new();
    internal FakeHost Host { get; } = new();
    internal VtsService Service { get; }
    internal ItemCache Cache { get; }
    internal ExpressionCache Expressions { get; }
    internal HotkeyCache Hotkeys { get; }
    internal int ChangedCount;
    internal int HotkeysChangedCount;

    internal ItemRig(string? token = "good")
    {
        Host.FakeSettings.Set(VtsService.HostKey, "127.0.0.1");
        Host.FakeSettings.Set(VtsService.PortKey, (long)Server.Uri.Port);
        if (token is not null) Host.FakeSettings.Set(VtsService.TokenKey, token);
        Server.On("AuthenticationRequest", d => new { authenticated = d.GetProperty("authenticationToken").GetString() == "good" });
        Server.On("APIStateRequest", _ => new { active = true, vTubeStudioVersion = "1.2.3", currentSessionAuthenticated = true });
        Server.On("EventSubscriptionRequest", _ => new { subscribedEventCount = 1 });
        Server.On("CurrentModelRequest", _ => new { modelLoaded = true, modelName = "Hiyori" });
        Server.On("ItemListRequest", _ => ListResponse());
        Server.On("HotkeyTriggerRequest", d => new { hotkeyID = d.GetProperty("hotkeyID").GetString() });
        Server.On("HotkeysInCurrentModelRequest", HotkeyList);
        Server.On("ExpressionStateRequest", _ =>
        {
            lock (_lock)
                return new
                {
                    modelLoaded = true,
                    expressions = _expressions.Select(e => new { name = e.File, file = e.File, active = e.Active }).ToArray()
                };
        });
        Service = new VtsService(Host, Retry);
        Cache = new ItemCache(Service, Host);
        Cache.Changed += () => Interlocked.Increment(ref ChangedCount);
        Expressions = new ExpressionCache(Service, Host);
        Hotkeys = new HotkeyCache(Service, Cache, Host);
        Hotkeys.Changed += () => Interlocked.Increment(ref HotkeysChangedCount);
    }

    private object HotkeyList(JsonElement data)
    {
        lock (_lock)
        {
            var list = data.TryGetProperty("live2DItemFileName", out var f)
                ? _itemHotkeys.GetValueOrDefault(f.GetString() ?? "") ?? []
                : _modelHotkeys;
            return new
            {
                modelLoaded = true,
                availableHotkeys = list.Select(h => new { name = h.Name, type = h.Type, file = h.File, hotkeyID = h.Id }).ToArray()
            };
        }
    }

    internal void AddModelHotkey(string id, string name, string type, string file)
    {
        lock (_lock) _modelHotkeys.Add((id, name, type, file));
    }

    internal void AddItemHotkey(string itemFile, string id, string name, string type, string file)
    {
        lock (_lock)
        {
            if (!_itemHotkeys.TryGetValue(itemFile, out var list)) _itemHotkeys[itemFile] = list = [];
            list.Add((id, name, type, file));
        }
    }

    internal void SetExpression(string file, bool active)
    {
        lock (_lock)
        {
            _expressions.RemoveAll(e => e.File == file);
            _expressions.Add((file, active));
        }
    }

    /// <summary>Pushes an item (or model) hotkey event the way VTube Studio sends it.</summary>
    internal Task PushHotkeyEventAsync(string hotkeyId, string action = "ToggleExpression", bool isItem = true,
        bool byApi = false) => Server.PushEventAsync("HotkeyTriggeredEvent", new
        {
            hotkeyID = hotkeyId,
            hotkeyName = hotkeyId,
            hotkeyAction = action,
            hotkeyFile = "",
            hotkeyTriggeredByAPI = byApi,
            modelID = "m-1",
            modelName = "Hiyori",
            isLive2DItem = isItem
        });

    internal object ListResponse()
    {
        lock (_lock)
            return new
            {
                itemInstancesInScene = _items.Select(i => new { fileName = i.File, instanceID = i.Id, type = i.Type }).ToArray()
            };
    }

    internal void AddItem(string file, string id, string type = "Live2D")
    {
        lock (_lock) _items.Add((file, id, type));
    }

    internal void RemoveItem(string id)
    {
        lock (_lock) _items.RemoveAll(i => i.Id == id);
    }

    internal int Count(string messageType) => Server.Requests.Count(r => r.MessageType == messageType);

    internal async Task StartConnectedAsync()
    {
        Service.Start();
        await FakeVtsServer.WaitUntilAsync(() => Service.Status == VtsStatus.Connected);
        await FakeVtsServer.WaitUntilAsync(() => Cache.Items.Count == 3);
        await FakeVtsServer.WaitUntilAsync(() => Hotkeys.FindModelHotkey("wave") is not null
            && Hotkeys.FindItemHotkey(Clothes, "console") is not null
            && Hotkeys.FindItemHotkey("Prism_Hat", "hat") is not null);
        await FakeVtsServer.WaitUntilAsync(() => Expressions.Find("mic") is not null);
    }

    internal CommandContext Ctx(params string[] parameters) => new()
    {
        Parameters = parameters,
        Target = ButtonTargets.TouchButton,
        SourceIndex = 2,
        Host = Host
    };

    public async ValueTask DisposeAsync()
    {
        Hotkeys.Dispose();
        Expressions.Dispose();
        Cache.Dispose();
        Service.Dispose();
        await Server.DisposeAsync();
    }
}

public class ItemCacheTests
{
    [Fact]
    public async Task Loads_when_connected_and_raises_changed()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        Assert.Equal(["Prism_Clothes_Original_2k", "Prism_Hat", "balloon.png"], rig.Cache.Items.Select(i => i.FileName));
        Assert.Equal("id-clothes", rig.Cache.Items[0].InstanceId);
        Assert.Equal("Live2D", rig.Cache.Items[0].Type);
        Assert.True(rig.ChangedCount >= 1);
        var request = rig.Server.Requests.First(r => r.MessageType == "ItemListRequest").Data;
        Assert.False(request.GetProperty("includeAvailableSpots").GetBoolean());
        Assert.True(request.GetProperty("includeItemInstancesInScene").GetBoolean());
        Assert.False(request.GetProperty("includeAvailableItemFiles").GetBoolean());
    }

    [Theory]
    [InlineData("Prism_Clothes_Original_2k", "id-clothes")]
    [InlineData("prism_hat", "id-hat")]
    [InlineData("BALLOON.PNG", "id-balloon")]
    [InlineData("clothes", "id-clothes")]
    [InlineData("  ORIGINAL ", "id-clothes")]
    [InlineData("prism", "id-clothes")]
    [InlineData("prism original", "id-clothes")]
    [InlineData("hat PRISM", "id-hat")]
    public async Task Find_matches_exact_then_part_then_words_case_insensitively(string key, string expectedId)
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        Assert.Equal(expectedId, rig.Cache.Find(key)?.InstanceId);
    }

    [Fact]
    public async Task Find_prefers_an_exact_name_over_a_part_and_the_first_in_scene_order()
    {
        await using var rig = new ItemRig();
        rig.AddItem("Prism", "id-prism");
        rig.Service.Start();
        await FakeVtsServer.WaitUntilAsync(() => rig.Cache.Items.Count == 4);

        Assert.Equal("id-prism", rig.Cache.Find("prism")?.InstanceId);
        Assert.Equal("id-clothes", rig.Cache.Find("prism_")?.InstanceId);
    }

    [Fact]
    public async Task Find_returns_null_for_unknown_or_empty()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        Assert.Null(rig.Cache.Find("nope"));
        Assert.Null(rig.Cache.Find("prism nope"));
        Assert.Null(rig.Cache.Find("  "));
    }

    [Fact]
    public async Task Subscribes_to_item_event()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        Assert.Contains(rig.Server.Requests, r => r.MessageType == "EventSubscriptionRequest"
            && r.Data.GetProperty("eventName").GetString() == "ItemEvent"
            && r.Data.GetProperty("subscribe").GetBoolean());
    }

    [Theory]
    [InlineData("ItemEvent")]
    [InlineData("ModelLoadedEvent")]
    public async Task Refetches_on_events(string eventName)
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        rig.AddItem("Cape", "id-cape");

        await rig.Server.PushEventAsync(eventName, new { itemEventType = "Added" });

        await FakeVtsServer.WaitUntilAsync(() => rig.Cache.Find("cape") is not null);
    }

    [Fact]
    public async Task Changed_is_raised_only_when_the_content_changed()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        var before = rig.ChangedCount;

        await rig.Server.PushEventAsync("ItemEvent", new { });
        rig.AddItem("Cape", "id-cape");
        await rig.Server.PushEventAsync("ItemEvent", new { });

        await FakeVtsServer.WaitUntilAsync(() => rig.Cache.Items.Count == 4);
        Assert.Equal(before + 1, rig.ChangedCount);
    }

    [Fact]
    public async Task Cleared_when_the_connection_is_lost()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        rig.Server.Stop();

        await FakeVtsServer.WaitUntilAsync(() => rig.Cache.Items.Count == 0);
    }

    [Fact]
    public async Task Cleared_when_not_authorised_any_more()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();

        rig.Service.ForgetToken();

        await FakeVtsServer.WaitUntilAsync(() => rig.Cache.Items.Count == 0);
    }

    [Fact]
    public async Task Requests_during_a_running_fetch_collapse_into_one_more_fetch()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        var initial = rig.Count("ItemListRequest");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Server.OnAsync("ItemListRequest", async _ =>
        {
            await gate.Task;
            return ServerReply.Ok(rig.ListResponse());
        });

        rig.Cache.RequestRefetch();
        await FakeVtsServer.WaitUntilAsync(() => rig.Count("ItemListRequest") == initial + 1);
        for (var i = 0; i < 5; i++) rig.Cache.RequestRefetch();
        gate.SetResult();
        await FakeVtsServer.WaitUntilAsync(() => rig.Count("ItemListRequest") == initial + 2);

        rig.Cache.RequestRefetch();
        await FakeVtsServer.WaitUntilAsync(() => rig.Count("ItemListRequest") == initial + 3);
        Assert.Equal(initial + 3, rig.Count("ItemListRequest"));
    }

    [Fact]
    public async Task RefreshAsync_completes_with_the_new_list_and_leaves_the_background_fetch_alone()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        var initial = rig.Count("ItemListRequest");
        rig.AddItem("Cape", "id-cape");

        await rig.Cache.RefreshAsync();

        Assert.NotNull(rig.Cache.Find("cape"));
        Assert.Equal(initial + 1, rig.Count("ItemListRequest"));
        rig.Cache.RequestRefetch();
        await FakeVtsServer.WaitUntilAsync(() => rig.Count("ItemListRequest") == initial + 2);
    }

    [Fact]
    public async Task RefreshAsync_never_throws_on_an_api_error()
    {
        await using var rig = new ItemRig();
        await rig.StartConnectedAsync();
        rig.Server.OnError("ItemListRequest", 600, "bad");

        await rig.Cache.RefreshAsync();

        Assert.Contains(rig.Host.FakeLog.Lines, l => l.StartsWith("W ") && l.Contains("item list"));
        Assert.Equal(3, rig.Cache.Items.Count);
    }

    [Fact]
    public async Task RefreshAsync_while_offline_does_nothing()
    {
        await using var rig = new ItemRig();

        await rig.Cache.RefreshAsync();

        Assert.Empty(rig.Cache.Items);
        Assert.Equal(0, rig.Count("ItemListRequest"));
    }
}
