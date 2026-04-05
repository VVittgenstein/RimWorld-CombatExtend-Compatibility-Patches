# Doors Expanded -- CE Compatibility Patch

| Field          | Value                                                        |
|----------------|--------------------------------------------------------------|
| **Mod**        | Doors Expanded v1.5.0.3 by Jecrell, lbmaian, jebjordan      |
| **Steam ID**   | 3532342422                                                   |
| **packageId**  | `jecrell.doorsexpanded`                                      |
| **Mod type**   | DLL (custom `Building_DoorExpanded` class)                   |
| **Patch file** | `Source/V2CEPatch/Harmony/Patch_CollisionVertical_CalculateHeightRange.cs` |
| **Bootstrap**  | `Source/V2CEPatch/V2CEPatchMod.cs` (conditional application) |
| **RimWorld**   | 1.5, 1.6                                                    |

---

## 1. What Broke

Doors Expanded replaces the vanilla door system with its own class hierarchy.
`Building_DoorExpanded` extends `Building`, not `Building_Door`. This is a
deliberate design decision by the mod authors -- `Building_DoorExpanded`
implements its own open/close state machine, draw logic, and interaction
handlers from scratch rather than inheriting from the vanilla door class.

To make the vanilla pathfinding and region systems work despite this non-
standard inheritance, Doors Expanded spawns invisible `Building_DoorRegionHandler`
helper things on every cell the door occupies. These helpers *do* extend
`Building_Door`, so vanilla code that queries the map grid for doors (via
`GridsUtility.GetDoor()` or direct cell iteration) finds them and behaves
correctly. The helpers have `fillPercent=0`, making them transparent to
obstruction checks.

Combat Extended's projectile collision pipeline does not use grid queries. It
operates on `Thing` references directly. The critical method is
`CollisionVertical.CalculateHeightRange()`, a private static method that
determines how tall an obstacle is for the purpose of projectile intersection
tests. At line 79, this method performs a type check:

```csharp
thing is Building_Door
```

Vanilla doors pass this check and receive open/closed height handling.
`Building_DoorExpanded` fails it. The method falls through to the generic
`Building` branch, which checks `Fillage == FillCategory.Full`. All expanded
doors declare `fillPercent=1` (they are physically solid objects that fill
their cell), so this evaluates to true. The method assigns a height range of
`(0, WallCollisionHeight)` -- a 2-meter solid wall.

The result: every open Doors Expanded door behaves as a solid 2-meter wall
for CE projectiles. Pawns cannot shoot through open doorways. Projectiles
impact the invisible collision volume of the open door and stop.

### Why Only the Projectile Pipeline Is Affected

Other CE systems that interact with doors work correctly because they find
the invisible `Building_DoorRegionHandler` helpers:

| CE System          | Method Used                      | Finds Helpers? | Status |
|--------------------|----------------------------------|----------------|--------|
| IncendiaryFuel     | `thing is Building_Door` (L21-26)| Yes            | Works  |
| Smoke              | `GridsUtility.GetDoor()` (L51-52)| Yes           | Works  |
| SuppressionUtility | `GridsUtility.GetDoor()` (L297) | Yes            | Works  |
| CollisionVertical  | `thing is Building_Door` (L79)   | No -- checks the visible door | **Broken** |

The distinction is that `CalculateHeightRange` receives the visible door
`Thing` as a direct argument from the projectile collision candidate list.
It never queries the grid. The other systems either iterate cell contents
(finding helpers) or explicitly call `GetDoor()` on a cell (which returns
the helper).

---

## 2. Design Problem and Options Considered

The goal: make CE projectiles pass through open Doors Expanded doors and
collide with closed ones, without breaking either mod's internal logic.

### Option A: Patch Building_DoorExpanded to Appear as Building_Door

The most direct approach would be to make CE's type check succeed -- either
by transpiling `Building_DoorExpanded` to fake the inheritance, or by
injecting an interface. This was rejected for several reasons:

- `Building_DoorExpanded` has a fundamentally different API from
  `Building_Door`. It declares its own `Open` property rather than inheriting
  one. Faking the inheritance risks confusing Doors Expanded's own Harmony
  patches, which target `Building_DoorExpanded` specifically.
- Doors Expanded already applies transpilers to vanilla methods
  (`DoorExpandedIsOpenDoorTranspiler`) that match specific IL patterns. Adding
  synthetic inheritance could change the IL in ways that break these existing
  transpilers.
- The invisible helpers already handle every non-projectile door interaction
  correctly. The problem is surgical -- only one method needs to know about
  `Building_DoorExpanded`.

### Option B: Transpiler on CalculateHeightRange

A Harmony transpiler could inject IL into `CalculateHeightRange` to add a
second type-check branch for `Building_DoorExpanded` alongside the existing
`Building_Door` branch. This was rejected because:

- `CalculateHeightRange` is a private static method with `out` parameters.
  Transpiling it requires matching exact IL patterns in CE's decompiled code,
  which is fragile across CE updates (CE ships as a compiled DLL without
  guaranteed IL stability).
- Doors Expanded's own transpiler approach (`DoorExpandedIsOpenDoorTranspiler`)
  works because it targets *vanilla* methods with stable IL. CE's decompiled
  methods do not offer the same stability guarantee.
- A prefix achieves the same result with none of the fragility.

### Option C: Harmony Prefix on CalculateHeightRange (Chosen)

A prefix that intercepts only `Building_DoorExpanded` instances and lets all
other things pass through to the original method. This is minimally invasive:
one method, one type check, no modification to either mod's internals.

---

## 3. Implementation

The fix consists of two components: the patch class and the conditional
bootstrap.

### 3.1 Patch Class

**File:** `Source/V2CEPatch/Harmony/Patch_CollisionVertical_CalculateHeightRange.cs`

```csharp
[HarmonyPatch]
public static class Patch_CollisionVertical_CalculateHeightRange
{
    private static Type doorExpandedType;
    private static PropertyInfo openProperty;
    private static bool initialized;

    static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(CollisionVertical), "CalculateHeightRange");
    }

    static bool Prefix(Thing thing, ref FloatRange heightRange, ref float shotHeight)
    {
        // ... see below
    }
}
```

**Target method resolution.** Because `CalculateHeightRange` is private static,
the patch uses `[HarmonyPatch]` with a `TargetMethod()` resolver rather than
attribute-based targeting. `AccessTools.Method` handles the private visibility.

**Soft reference to Doors Expanded.** The patch uses reflection-based type
resolution to avoid any hard DLL dependency on DoorsExpanded.dll:

```csharp
doorExpandedType = AccessTools.TypeByName("DoorsExpanded.Building_DoorExpanded");
if (doorExpandedType != null)
    openProperty = AccessTools.Property(doorExpandedType, "Open");
```

This resolution runs once on first invocation and is cached in static fields.
If Doors Expanded is not loaded (which should not happen given the bootstrap
gate, but is defended against), `doorExpandedType` is null and the prefix
returns `true` on every call, making it a no-op.

**Core logic.** The prefix checks whether the incoming `Thing` is an instance
of `Building_DoorExpanded`. If not, it returns `true` and the original CE
method runs unmodified:

```csharp
if (doorExpandedType == null || !doorExpandedType.IsInstanceOfType(thing))
    return true;
```

If the thing is a `Building_DoorExpanded`, the prefix reads its `Open`
property via the cached `PropertyInfo` and sets the out parameters directly:

- **Open door:** `heightRange = (0, 0)`, `shotHeight = 0`. The door has zero
  collision height. Projectiles pass through.
- **Closed door:** `heightRange = (0, WallCollisionHeight)`,
  `shotHeight = WallCollisionHeight`. The door is a solid 2-meter wall.
  Projectiles impact it.

The prefix returns `false`, skipping the original method entirely for this
thing. The original method's logic for `Building_Door`, generic buildings,
plants, pawns, and all other thing types is never reached for
`Building_DoorExpanded` instances -- and never needs to be.

### 3.2 Conditional Bootstrap

**File:** `Source/V2CEPatch/V2CEPatchMod.cs` (lines 17-22)

```csharp
if (ModDetection.DoorsExpandedActive)
{
    harmony.CreateClassProcessor(typeof(Patch_CollisionVertical_CalculateHeightRange)).Patch();
    Log.Message("[V2CEPatch] Applied Doors Expanded collision fix");
}
```

The patch is only applied when `ModDetection.DoorsExpandedActive` is true.
This flag is set during `ModDetection.Init()` via
`ModsConfig.IsActive("jecrell.doorsexpanded")` (see
`Source/V2CEPatch/Utility/ModDetection.cs`, line 15). If a user does not
have Doors Expanded installed, the Harmony patch is never registered and has
zero runtime cost.

`CreateClassProcessor` is used rather than `harmony.PatchAll()` to ensure
each patch class is applied individually and only when its corresponding mod
is detected. This is the standard V2CEPatch pattern -- every supported mod
has its own detection gate.

### 3.3 Downstream Propagation

A critical property of this fix is that `CollisionVertical.CalculateHeightRange`
is the single point of truth for collision height in CE's projectile pipeline.
All downstream consumers inherit the corrected values automatically:

- **`ProjectileCE.CheckCellForCollisions`** (line ~1104): Iterates cell
  contents where `Fillage > 0`. Open `Building_DoorExpanded` doors (with
  `fillPercent=1`) are added as collision candidates. But `CanCollideWith()`
  calls `GetBoundsFor()`, which calls `CollisionVertical.CalculateHeightRange`.
  The patched method returns `(0, 0)` for open doors.
  `ExactRectangle.IntersectRay()` returns false for a zero-height bounding
  box. The door is discarded as a collision candidate.

- **`ProjectileCE.ImpactSomething`** (line ~1680): Same call chain. Same
  zero-height bounds. No false impact.

- **`Verb_LaunchProjectileCE.GetTargetHeight`**: When evaluating cover between
  shooter and target, this method calls `CollisionVertical` on cover
  candidates. An open door correctly provides 0 height (no cover). A closed
  door correctly provides 2m height (full cover).

No additional patch sites are needed. One prefix fixes the entire pipeline.

---

## 4. Why This Fix Beat Alternatives

### Minimal Surface Area

The fix touches exactly one CE method. It does not modify any Doors Expanded
code, does not alter any def XML, does not inject comps, and does not change
any inheritance hierarchy. The entire patch is 50 lines of C# including
imports and whitespace.

### No Hard Dependencies

The reflection-based soft reference (`AccessTools.TypeByName`) means
V2CEPatch.dll does not reference DoorsExpanded.dll at compile time. There is
no assembly dependency to manage, no load-order constraint beyond what
`ModDetection` already handles, and no risk of `TypeLoadException` if Doors
Expanded updates its assembly.

### Fragility Profile

A transpiler on `CalculateHeightRange` would break if CE rearranges the IL of
that method in any update. A prefix is resilient to internal refactoring of
the original method -- as long as the method signature remains
`(Thing, out FloatRange, out float)`, the prefix works. The only change that
could break this patch is CE renaming the method or changing its parameter
types, both of which would be major API changes that would break far more
than this patch.

### Correctness of the Binary Model

The fix is purely binary: open doors are transparent (`heightRange = (0, 0)`),
closed doors are solid (`heightRange = (0, WallCollisionHeight)`). This
matches CE's own handling of vanilla `Building_Door` exactly. There are no
fractional fill states, no partially-open collision volumes, and no
interpolation. All Doors Expanded door variants (curtains, double doors,
stretch doors, etc.) share the `Building_DoorExpanded` base class and declare
`fillPercent=1`, so the one prefix handles every variant uniformly.

No numerical balancing is involved. There are no stats, ammo types, armor
values, or damage multipliers to tune. The patch is a type-system correction:
teaching CE that `Building_DoorExpanded` is a door.

---

## 5. Testing Notes

To verify the patch is active:

1. Load a game with Combat Extended, Doors Expanded, and V2CEPatch enabled.
2. Check the debug log for `[V2CEPatch] Applied Doors Expanded collision fix`.
3. Place any Doors Expanded door (e.g., a curtain door or double door).
4. Open the door and position a shooter on one side, a target on the other.
5. Fire through the open doorway. Projectiles should pass through and hit
   the target (or the far wall).
6. Close the door and fire again. Projectiles should impact the closed door.
7. Verify that vanilla `Building_Door` doors continue to function identically
   to an unpatched CE installation.

To verify no regression in other CE systems:

- Place a Doors Expanded door adjacent to a fire. Confirm the IncendiaryFuel
  system still recognizes the door (via the invisible helper).
- Generate smoke near a Doors Expanded door. Confirm smoke diffusion respects
  the door state.
- Suppress a pawn behind a Doors Expanded door. Confirm suppression
  calculations account for the door correctly.
