# LoupixDeck VTube Studio plugin

Plugin for [LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck) that controls [VTube Studio](https://denchisoft.com/) through its plugin API: toggle expressions, trigger hotkeys of the model and of Live2D items, and see on the buttons what is on.

## Requirements

- VTube Studio with **Allow Plugin API access** switched on in its settings.
- LoupixDeck 1.37.0 or newer.

## Setup

1. Install the plugin in LoupixDeck and enable it for your device.
2. Open the plugin settings and press **Authorise**.
3. Click **Allow** in the popup in VTube Studio.

The token is stored in the plugin's settings, so the popup appears only once. **Forget token** removes it.

**Host** (default `localhost`) and **Port** (default `8001`) only need changing if VTube Studio runs elsewhere or on another port. The plugin reconnects on its own when VTube Studio starts later.

## Commands

| Command | What it does |
|---|---|
| `VTubeStudio.ConnectionStatus` | Shows `connected`, `not authorised` or `offline`. Pressing it while not authorised asks VTube Studio for access. |
| `VTubeStudio.ToggleExpression(<expression>[,<icon>])` | Toggles an expression of the model. |
| `VTubeStudio.ActivateExpression(<expression>)` | Switches an expression on. |
| `VTubeStudio.DeactivateExpression(<expression>)` | Switches an expression off. |
| `VTubeStudio.TriggerHotkey(<hotkey>[,<icon>])` | Triggers a hotkey of the model. |
| `VTubeStudio.TriggerItemHotkey(<item>,<hotkey>[,<icon>])` | Triggers a hotkey of a Live2D item in the scene. |

- `<expression>` is the expression's file name, with or without `.exp3.json`, or its name: `VTubeStudio.ToggleExpression(EXP_angry)`.
- `<hotkey>` is the hotkey's name or ID, or the file it uses (`sexy` finds the hotkey that toggles `sexy.exp3.json`): `VTubeStudio.TriggerHotkey(wave)`.
- `<item>` is the item's file name or a part of it: `VTubeStudio.TriggerItemHotkey(Clothes_Original,console)`.

Expressions of an item can only be switched through the item's hotkeys, so the item needs a hotkey for each of them in VTube Studio.

## What the buttons show

- Green: the expression is on. Black: off.
- Dimmed icon: VTube Studio is not running or not authorised.
- Dimmed icon with `?`: the loaded model has no such expression.

Hotkey buttons show on and off when the hotkey toggles an expression. For items, VTube Studio does not report the state, so the plugin counts the hotkey events instead and starts from "off" when it connects. An item expression that was already on then shows the wrong way round until it is toggled once in VTube Studio.

## Icons

A button gets its icon from keywords in the expression or hotkey name. Spaces, underscores and case do not matter, and the more specific keyword wins. Short keywords such as `eat`, `mad`, `pen` or `star` only match as a whole word (`EXP_eat`, not `great`); they are marked with * below. The optional last parameter picks an icon by name, or `none` for text only: `VTubeStudio.ToggleExpression(EXP_angry,heart)`.

![Icons](tools/emote-icons-sheet.png)

| Icon | Keywords |
|---|---|
| `cash` | cash, money, dollar, rich*, pay* |
| `heart` | heart, love* |
| `angry` | angry, mad*, rage*, anger* |
| `angryshy` | angryshy |
| `shy` | shy, blush |
| `star` | star*, stars, sparkle |
| `tears` | tears, cry*, sad*, sob* |
| `pleading` | pleading |
| `sulking` | sulking |
| `swirly` | swirly, dizzy |
| `xd` | xd, dx |
| `avoid` | avoid |
| `mouth3` | 3mouth |
| `pout` | pout |
| `sweat` | sweat, cartoonsweat |
| `sweatdrops` | realisticsweat |
| `nosebubble` | nosebubble |
| `sleepbubble` | sleepbubble |
| `sleep` | sleep, zzz, nap*, tired* |
| `loading` | loading |
| `speech` | speech, talk*, say* |
| `eating` | eating, eat*, food, nom*, snack* |
| `fish` | fish* |
| `white3` | white3 |
| `controller` | console, controller, gamepad, gaming, game* |
| `microphone` | microphone, mic* |
| `pen` | pen*, stylus, pencil, draw* |
| `outfit` | basicwhite, outfit, clothes |
| `outfitred` | redoutfit, outfitred, redskirt |
| `outfitblack` | blackoutfit, outfitblack, blackskirt |
| `heels` | sexy, heels |
| `school` | school, seifuku, uniform |
| `foxears` | kitsune, kittsune, fox, wolf |
| `bunnyears` | bunny, rabbit |
| `deviltail` | sdemon, succubus, tail |
| `horns` | demon*, devil, horn*, horns, oni* |
| `undies` | naked, underwear, undies, nude*, bra* |
| `bikini` | bikini, swim |
| `bikiniwhite` | whitebikini, bikiniwhite |
| `bikiniblack` | blackbikini, bikiniblack |
| `swimsuit` | swimsuit, onepiece |
| `swimsuitwhite` | whiteswimsuit, swimsuitwhite |
| `swimsuitblack` | blackswimsuit, swimsuitblack |
| `tshirt` | tshirt, boymode |
| `hoodie` | hoodie |
| `suit` | tuxedo, suit* |
| `trunks` | trunks |
| `halo` | halo |
| `hat` | witchhat, xmashat, santahat, hat* |
| `coffee` | coffee |
| `boba` | boba, bubbletea |
| `sunglasses` | sunglasses |
| `baseballcap` | baseballcap, cap* |
| `blanket` | blanket |
| `dragontail` | dragontail |
| `foxtail` | foxtail |
| `horsetail` | horsetail |
| `liontail` | liontail |
| `longhairtail` | longhairtail |
| `mermaidtail` | mermaidtail, mermaid |
| `ninetail` | ninetail, ninetails |
| `raccoontail` | raccoontail |
| `squirreltail` | squirreltail |
| `thicktail` | thicktail |
| `sharktail` | sharktail, whaleshark |
| `cattail` | cattail |
| `nightcap` | nightcap |
| `fairywings` | fairywing |
| `featherwings` | featherwing |
| `impwings` | impwing |
| `littlewings` | littlewing |
| `membranedwings` | membranedwing |
| `petitwings` | petitwing |
| `can` | soda, cola, energydrink, can*, beverage |
| `water` | water* |
| `wine` | wine* |
| `juice` | juice |
| `cocoa` | cocoa, hotchocolate |
| `bread` | bread |
| `egg` | egg* |
| `pudding` | pudding |
| `candycane` | candycane |
| `bed` | bed* |
| `couch` | couch, sofa |
| `gamerchair` | gamerchair, gamingchair |
| `table` | table* |
| `tablet` | tablet |
| `hammer` | hammer, mallet |
| `keys` | keys*, key* |
| `xmaslights` | xmaslights, lights* |
| `headpat` | headpat, pat* |
| `steam` | steam* |
| `swimring` | swimmingtube, innertube, swimring |
| `heartglasses` | heartglasses |
| `catears` | catear |
| `moustache` | moustache, mustache |
| `eyepatch` | eyepatch |
| `headband` | headband |
| `helmet` | helmet |
| `clown` | clown |
| `pacifier` | pacifier |
| `bandaid` | bandaid |
| `crown` | crown |
| `earring` | earring |
| `bow` | ribbon, bow* |
| `miku` | miku |

`connected`, `disconnected` and `locked` belong to the connection button and have no keywords.

## Build

```
.\build.ps1
```

Builds the plugin and packs `dist\vtubestudio-<version>-any.zip`.
