# Larder — Technical Plan

**Idea:** a food planner that lives in the inventory screen. It looks at the food you actually
have (bag, chests you can open nearby, placed feasts), picks the best three for your goal, tells
you which of those you can eat right now, and names the **one dish to cook next** that would
improve the combo, with its ingredients as have / need.

**Target build:** Valheim 1.0.16 (Unity 6), network version 40
(`Version.CurrentVersion = new GameVersion(1, 0, 16)`, `c_networkVersion = 40u`). Findings below
come from the rig's `valheim_Data/Managed/assembly_valheim.dll` (copied 2026-09-28) decompiled
with `ilspycmd`. `lib/` gets a fresh copy from the rig before building, per the sibling repos'
rule.

**Scope:** client-side only. No RPCs of our own, no ZDO writes, nothing on the server; works on
vanilla servers. Thunderstore namespace `Jumpingmushroom`, package `Larder`, GUID
`com.jumpingmushroom.larder`, repo `github.com/jumpingmushroom/Larder`. BepInEx only, no Jotunn.
Repo layout, build scripts, publicizer setup and release steps are the same as Milestones'.

**Prior art (checked 2026-09-28):**
- **FeastPlanner** (LongHouseListings, 2026-06, needs Jotunn): hotkey window (RightAlt+P) with
  role/biome meal templates, hidden undiscovered recipes, inventory + nearby-container
  craftability counts, saved custom plans. Advisory only.
- **Glutton Food Manager** (2021), **HearthPantry** (2026-09): auto-eaters.
- Food-stat editors.

Larder is different by design: it sits in the inventory/chest screen (no hotkey window), plans
from **what you own**, not templates, suggests the **one dish to cook next** with its ingredient
chain, shows **slot timing** ("refresh in 6m"), and offers an Eat button. It does not auto-eat
and has no templates or saved plans. The README and Thunderstore description lead with these
points.

**Decisions agreed (2026-09-28 design review):**
- Main use: at base, before heading out. The planner is a **side panel on the inventory screen**,
  opened by a *Larder* button; it remembers whether it was open. Works with or without a chest
  open.
- Goals: **Health**, **Stamina**, **Eitr**, **Balanced** (= health + stamina, eitr ignored).
  Selected goal is remembered per character.
- Rank by full stats. Duration is a tie-breaker and is displayed, not modelled (§1.2).
- Active foods: plan the ideal combo, then annotate each line: *active, 18m left* / *can refresh
  now* / *eat now*, and for an active food not in the plan, *slot frees in Xm*.
- Sources: bag + containers you can open within **20 m** (chests, carts, ship holds; wards and
  private chests respected; tombstones excluded) + placed feasts with portions left.
- Cook next: the best upgrade **ready now** (all ingredients in bag + chests, station in range at
  the right level) plus one **"if you had…"** pick that beats it, with have / need per ingredient
  and intermediates expanded up to 3 steps.
- Spoilers: only discovered recipes are suggested. `ShowUndiscovered` (off) lifts that. Food you
  own is never hidden.
- Eat button for bag items only, using the same code path as right-clicking the item. Food in
  chests is advice only.
- Feasts: a placed feast in range is a source; a feast item in bag or chest counts as "place it,
  then eat"; feast recipes are cook-next candidates. The feast buff is shown, not scored.
- UI built with Unity UI (uGUI + TextMeshPro) from the game's own assets, like Milestones.
- Thunderstore categories as the siblings: `client-side`, `utility`, `ai-generated`.

---

## 1. How vanilla food works

### 1.1 Food data (`ItemDrop.ItemData.SharedData`)

| Field | Meaning |
|---|---|
| `m_food` | max health added |
| `m_foodStamina` | max stamina added |
| `m_foodEitr` | max eitr added |
| `m_foodBurnTime` | duration in seconds |
| `m_foodRegen` | health healed per 10 s tick |
| `m_isDrink`, `m_foodEatAnimTime` | presentation only |
| `m_consumeStatusEffect` | optional buff applied on eating (feasts, meads) |

Everything comes from each item's shared data, so a food added by a patch or another mod is
picked up without code changes.

**Only Consumable items with `m_food > 0` occupy a food slot.** `Humanoid.CanConsumeItem`
returns false unless `m_itemType == Consumable`, and `Humanoid.UseItem` only consumes Consumables
(feast pieces are Consumable too), so raw meats, which carry `m_food > 0` but are Materials, can't
be eaten. `Player.CanConsumeItem` only calls `CanEat` when `m_food > 0`, and
`Player.ConsumeItem` only calls `EatFood` when `m_food > 0`. An item with stamina or eitr but zero
health is consumed for its status effect only. Larder's food filter is therefore
`m_itemType == Consumable && m_food > 0`, and the rule is the game's, not ours.

### 1.2 Slots, re-eating and decay (`Player`)

- `m_maxFoods = 3`; foods live in `m_foods` (`List<Player.Food>`: `m_item`, `m_time`, `m_health`,
  `m_stamina`, `m_eitr`).
- **No duplicates:** `CanEat` matches on `m_shared.m_name`. The same food can be eaten again
  only when `Food.CanEatAgain()`, i.e. `m_time < m_foodBurnTime / 2`, which resets its timer.
- **Full slots:** with three foods, `CanEat` still returns true if *any* food is under half.
  `EatFood` then replaces `GetMostDepletedFood()`, the refreshable food with the least time
  left. So a slot "frees" when a food drops under half its duration, not when it expires.
- **Decay** (`UpdateFood`, once per second scaled by `Game.m_foodRate`, the world modifier):
  `value = max × Clamp01(m_time / m_foodBurnTime)^0.3`. Every food follows the same curve, so
  the lifetime average is `max / 1.3 ≈ 77 %` for all of them and **duration never changes which
  food is stronger**; it only changes how often you eat. Hence ranking by max values.
- Totals (`GetTotalFoodValue`): health `m_baseHP = 25` + foods, stamina `m_baseStamina = 75` +
  foods, eitr `0` + foods. Regen: sum of `m_foodRegen` every 10 s.
- Real time left = `m_time / Game.m_foodRate`.

### 1.3 Feasts (`Feast : MonoBehaviour, Hoverable, Interactable`)

A placed feast has `m_eatStacks = 5` portions, and the stack lives in the ZDO (`ZDOVars.s_value`,
`-1` when empty; `GetStack()`). The food comes from `m_foodItem`, the feast's own `ItemDrop` if
unset. Eating goes through `RPC_TryEat` on the owner, then `RPC_EatConfirmation` back to the
eater, which applies `m_consumeStatusEffect` and calls `Player.EatFood(m_foodItem.m_itemData)`.
Larder only reads `GetStack()` and `m_foodItem`, and never eats a feast for you. A placed feast
is an `ItemDrop` whose `IsPiece()` is true, so it appears in `Piece.s_allPieces`.

### 1.4 Containers

- A `Container` loads its inventory from the ZDO and refreshes it from `CheckForChanges`
  (`InvokeRepeating`, every 1 s), on every client, whether or not it is open. Reading a chest's
  contents is safe on a client and needs no server support.
- Access, mirroring `Container.Interact`: `m_checkGuardStone && !PrivateArea.CheckAccess(pos, 0f,
  flash: false)` means no access (ward); then the private `Container.CheckAccess(playerID)`:
  `Public` yes, `Private` only for `m_piece.GetCreator()`, `Group` no. Private methods are
  reachable through the build-time publicizer.
- Carts (`m_wagon`) and ship holds are containers on pieces; the `Container` may sit on a child
  object. Tombstones (`TombStone` component) are excluded.
- No static list of containers exists. Enumerate `Piece.s_allPieces` (private, publicized),
  filter by distance, then `GetComponentInChildren<Container>()` / `GetComponent<Feast>()`.

### 1.5 Recipes, stations and conversions

- `ObjectDB.instance.m_recipes`: `Recipe { m_item, m_amount, m_enabled, m_craftingStation,
  m_minStationLevel, m_requireOnlyOneIngredient, m_resources: Piece.Requirement[] }`. Food recipes
  are those whose `m_item` passes the food filter (cauldron, food preparation table for feasts).
- Station presence and level: `CraftingStation.FindStationsInRange(name, point, range, list)`
  over `m_allStations`, then `GetLevel()` against `m_minStationLevel`.
- Conversions: `CookingStation.m_conversion` (cooking station, iron cooking station, oven,
  `m_from` → `m_to`) and `Smelter.m_conversion` (windmill: barley → flour, and the like). Read
  from the prefabs in `ZNetScene.instance.m_prefabs`, so modded stations are included.
- Discovery: `Player.IsRecipeKnown(itemSharedName)` for recipes, `Player.IsMaterialKnown(sharedName)`
  for items seen. A conversion product counts as discovered once its result is a known material.

### 1.6 Inventory screen and eating

- `InventoryGui.Show(Container, int)` / `Hide()`, `InventoryGui.IsVisible()`,
  `IsContainerOpen()`, `m_currentContainer`.
- Right-click to use goes through `Humanoid.UseItem(inventory, item, fromInventoryGui: true)` →
  `Player.ConsumeItem`, which runs every vanilla check (`CanEat` with messages, status-effect
  conflicts). The Eat button calls exactly this.

---

## 2. Design

### 2.1 Core model: `Core/Model/` (pure C#, no Unity or game types, xUnit-tested)

- `FoodStats { Id, Name, Health, Stamina, Eitr, BurnTime, Regen }`.
- `Goal { Health, Stamina, Eitr, Balanced }`; `Score(goal, stats)` returns a tuple compared
  lexicographically:
  - Health: `(ΣHP, ΣSt + ΣEitr, ΣRegen, ΣBurnTime)`; Stamina and Eitr likewise with their stat
    first.
  - Balanced: `(ΣHP + ΣSt, ΣEitr, ΣRegen, ΣBurnTime)`.
- `ComboSolver.Best(pool, goal)` → best 3 distinct foods (by `Id` = shared name) from the pool.
  Brute force over all triples; pools are at most a few hundred, so this is well under a
  millisecond. With fewer than three foods it returns what exists.
- `SlotAdvisor`: given the plan and the active foods (`m_time`, burn time, `foodRate`), label
  each planned food *active Xm left* / *can refresh now* / *eat now* / *eat in Xm* (all slots
  full and none refreshable) / *refresh X first*, and each active food outside the plan *slot
  frees in Xm* (time until it drops under half). Eviction follows `Player.EatFood`: with three
  foods, a new one replaces `GetMostDepletedFood()`, the refreshable food (under half) with the
  smallest `m_time` among all active foods, planned ones included (Player.cs:2434-2445, 2515).
  If that target is a planned food, the new food is `RefreshFirst` (`BlockedBy` = that food's
  id, no Eat button) and the planned food itself shows *can refresh now*; if it's a food outside
  the plan, *eat now*. Foods outside the plan are ordered by time until refreshable, then by
  time left, so they're assigned in the order the game takes them.
- `CookPlanner`: a producer graph. `Producer = Recipe(station, level, inputs×n, yield) |
  Conversion(stationKind, from, to)`. `Resolve(target, stock, stations, depth ≤ 3)` returns a
  tree of steps with have / need per leaf, the missing items, the missing stations, and
  `ReadyNow`. Guards against cycles, consumes stock across the tree so one Lox meat isn't
  counted twice, and treats `m_requireOnlyOneIngredient` as "any one of".
- `CookAdvisor`: for each discovered dish not already in the pool, re-solve the combo with it
  added; `gain = newScore − baseScore`. **Ready pick** = the highest gain with `ReadyNow`. **Almost
  pick** = the highest gain that beats the ready pick, where every missing ingredient is a known
  material (unless `ShowUndiscovered`). Dishes with zero gain are never suggested.

### 2.2 Game adapters: `Core/`

- `FoodCatalog`: built when `ObjectDB` and `ZNetScene` are ready, and rebuilt if the item count
  changes (mods registering late). It collects food items, producers from recipes and from
  `CookingStation` / `Smelter` / `Feast` prefabs, and the feast-item → food-item mapping.
- `StockScanner`: snapshot of bag + accessible containers within `Radius` + placed feasts with
  `GetStack() > 0`. Per item: count, and sources with a label and distance ("bag", "Chest 4m",
  "Cart 12m", "Feast 6m"). A feast item in a container counts as a food source labelled "place,
  then eat".
- `StationScanner`: crafting stations by name → highest level in range; conversion stations
  present in range.
- `ActiveFoods`: reads `Player.m_foods` and `Game.m_foodRate`.
- `Runtime`: owns the snapshot → model → view cycle. It runs only while the panel is visible and
  re-snapshots about once a second. Everything is wrapped in try/catch; on failure the panel shows
  "couldn't read …" and the log says why. Scanning must never break the inventory screen.

### 2.3 UI: `UI/`

- `LarderButton`: a small button on the inventory screen, cloned from a vanilla button so it
  takes the game's style. Toggles the panel. Gamepad: the panel follows `PanelOpen` (open by
  default), so gamepad players see the plan; the Eat and goal buttons are mouse-only in 0.1.0,
  and the goal can also be set with `larder goal <name>`.
- `LarderPanel`, hung off the inventory root (`InventoryGui.m_inventoryRoot`) and built from the
  game's panel background sprite (colour alpha forced to at least 0.94, so it's opaque even where
  the host block is translucent), fonts and item icons. `UI/Placement.cs` positions it in screen
  space on every Show (and each frame for the next 0.5 s, while the chest block appears and the
  screen animates in) and after every refresh, per `UI.Placement`:
  - **Auto** (default): just right of the whole cluster of UI beside the inventory. Start from
    `m_player`'s screen rect; candidates are the active, enabled Graphics under the inventory root
    with visible colour, at least 8×8 px, not Larder's own, not empty text and not wider than half
    the screen. A candidate that overlaps `m_player`'s vertical band and whose left edge lies
    between `m_player`'s left edge and the cluster's right edge + 24 px extends the cluster
    (`PlacementMath.ClusterRight`, repeated until nothing extends it), so another mod's extra-slot
    grid next to the inventory is stepped over but a far-away panel (crafting) is not. The panel's
    top-left goes 12 px right of the cluster, level with the top of `m_player`.
  - **Below**: under the chest block when it's open, else under `m_player`, left-aligned with it.
  - **Manual**: at the top-left of the screen, moved only by OffsetX/OffsetY.
  - OffsetX/OffsetY (canvas units) are added in every mode, then the panel is clamped on screen.
  Contents:
  - Goal selector (4 tabs).
  - **Best combo**: header row with an **Eat N** button (right of the header text, hidden when
    N < 2) that eats every planned food it safely can from the bag in one click — re-reading
    active foods and bag contents between eats, refreshing a planned food before a new one can
    evict it (`Core/Model/EatOrder.cs`, same rule as `SlotAdvisor`) — followed by 3 rows of
    icon · name · HP / St / Eitr · duration · source · status label · [Eat] when it's in the bag
    and edible now (`Player.CanEat(item, false)`).
  - Totals: the plan's HP / St / Eitr including base, plus its regen, and a "now …" line with
    your current HP / St / Eitr.
  - **Cook next**: ready pick and almost pick, each with its station (level, in range or not)
    and an ingredient list with have / need, intermediates indented.
  - Empty states: "No food nearby", "Nothing you can cook improves this combo".
- Item names come from the game's localisation (`Localization.instance.Localize`); Larder's own
  labels are English in 0.1.0.

### 2.4 Patches: `Patches/InventoryPatches.cs`

Only `InventoryGui.Show` is patched, with a postfix that opens/attaches the panel. There is no
`Hide` patch: closing is detected by polling `InventoryGui.IsVisible()` once a frame from
`Runtime.Tick`, which also stops re-planning while the screen is closed. No prefixes, no skipped
vanilla code.

### 2.5 Config (BepInEx, `com.jumpingmushroom.larder.cfg`, editable in-game via F1)

| Section | Setting | Default | Meaning |
|---|---|---|---|
| General | Enabled | `true` | Master switch. |
| General | Radius | `20` | Metres to search for chests, feasts and stations. |
| General | ShowUndiscovered | `false` | Allow suggestions of recipes you haven't discovered. |
| General | IncludeCartsAndShips | `true` | Count carts and ship holds as containers. |
| UI | PanelOpen | `true` | Remembered open/closed state. |
| UI | Placement | `Auto` | `Auto` (right of everything beside the inventory), `Below` (under the chest/inventory) or `Manual` (from the screen's top-left by the offsets). |
| UI | ToggleKey | none | Optional key that toggles the panel while the inventory is open. |
| UI | OffsetX / OffsetY | `0` | Nudge the panel (positive is right / up); in `Manual` the position from the screen's top-left. |
| UI | Scale | `1` | Panel size. |
| Logging | Verbose | `false` | Log snapshots, skipped items and plans. |

The goal is stored per character in `Player.m_customData["larder.goal"]` (local save only,
nothing synced).

### 2.6 Console

`larder` prints the current snapshot summary, the plan and the cook-next picks to the console
and the BepInEx log. `larder foods` lists the catalogue with stats. Used for rig testing via
`build/logs.sh`.

---

## 3. Project layout

Same as Milestones:

```
Larder/
  PLAN.md  README.md  CHANGELOG.md  CLAUDE.md  LICENSE (MIT)
  Directory.Build.props  Larder.sln  .gitignore
  build/  package.sh  publish.sh  make_icon.py      (+ local, gitignored: deploy.sh logs.sh shot.sh crop.sh)
  lib/    (gitignored, from the rig)
  src/Larder/  Larder.csproj  Plugin.cs  PluginConfig.cs  ConfigurationManagerAttributes.cs
               Core/  Core/Model/  Patches/  UI/
  tests/Larder.Tests/   (net8.0, xUnit, compiles Core/Model only)
  thunderstore/  manifest.json  README.md  icon.png
  docs/images/
```

Version lives in three places (`PluginVersion` in `Plugin.cs`, `<Version>` in the csproj,
`version_number` in `thunderstore/manifest.json`); `package.sh` refuses to package if the first
and last disagree.

## 4. Build order for 0.1.0

1. Scaffold from Milestones (props, csproj, sln, scripts, `.gitignore`, `CLAUDE.md`), a fresh
   `lib/` from the rig, a plugin that loads and logs.
2. `Core/Model` with tests: solver, scoring, slot advisor, cook planner, cook advisor.
3. `FoodCatalog`, `StockScanner`, `StationScanner`, `ActiveFoods`, and the `larder` console
   command. Verify on the rig from the log before any UI.
4. Button + panel with combo, totals and status labels.
5. Eat button.
6. Cook-next section.
7. Config polish, README + Thunderstore README with screenshots, icon, CHANGELOG.
8. Release 0.1.0: bump versions, `./build/package.sh`, commit `0.1.0`, tag `v0.1.0`, push,
   `gh release create v0.1.0 dist/Larder-0.1.0.zip`, then
   `scp dist/Larder-0.1.0.zip equ@192.168.1.160:~/Downloads/`. The Thunderstore upload is the
   user's.

Every change is committed and pushed. No AI attribution in commits, PRs, README or release notes.

## 5. Later (not 0.1.0)

- Take food from the open chest.
- Hint on the cooking station / cauldron hover.
- Planning 2–3 dishes at once.
- Scoring feast buffs, meads and potions.
- Translating Larder's own labels.

Out of scope: auto-eating, biome/role templates, saved plans.

## 6. To verify on the rig

Verified in-game (2026-09-28 sessions):

- `Piece.s_allPieces` found 22 containers in the user's base; a full snapshot took ~2 ms per plan
  with those 22 containers (target was < 2 ms — close enough, no caching needed for 0.1.0).
- Every vanilla feast item maps to itself as its food: `FeastFood[id] == id` for all of them,
  because `m_foodItem` is unset on the vanilla `Feast` prefabs (it falls back to the feast's own
  `ItemDrop`, per §1.3).
- Eat via `UseItem(…, fromInventoryGui: true)` behaves like a right-click — same animation and
  messages — confirmed by the user.
- Hammer placement ghosts (the translucent preview piece following the cursor) are skipped and
  never show up as a spurious container or feast — confirmed.
- Raw meats were wrongly counted as food before the `Consumable` half of the food filter was
  added (§1.1); they carry `m_food > 0` but are Materials, not Consumables, so vanilla can't eat
  them either. Fixed by requiring `m_itemType == Consumable`.
- The panel overlapped another mod's extra-slot inventory grid before `UI.Placement` shipped;
  `Auto` placement (stepping the panel past the whole UI cluster beside the inventory, not just
  the vanilla inventory) resolved it.

Still unverified: carts and ship holds (none available to test on the rig), and a placed feast
as a stock source (not tested in a session).
