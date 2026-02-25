# Adding a New Powerup (Quantum / NSMB-MarioVsLuigi)

This document describes the correct process to add a new powerup so it can be selected by coin reward randomness and used by gameplay without breaking asset refs.

## Scope

Use this when adding a new `PowerupAsset` (for example, a projectile powerup like Super Ball).

This process avoids two common failure modes:
1. `NullReferenceException` in `GamemodeAsset.GetRandomItem(...)` from unresolved `AllCoinItems` refs.
2. "Always/mostly Mushroom" behavior from fallback + weighted spawn expectations.

## Rules

- Prefer Unity Inspector workflows over manual YAML edits for asset references.
- Keep changes additive where possible.
- Do not change coin counting or random-selection logic unless intentionally redesigning game rules.
- Treat Quantum asset identity (`Identifier.Guid`) as critical data.

## Step-by-Step

1. Create the asset object
- Duplicate an existing similar powerup asset in Unity (for example `FireFlower.asset` or `PropellerMushroom.asset`).
- Rename it (for example `SuperBallFlower.asset`).
- Update fields on the duplicated asset:
  - `Prefab`
  - `State`
  - `SpawnChance`
  - `LosingSpawnBonus`
  - flags like `CustomPowerup`, `BigPowerup`, `VerticalPowerup`, `CanSpawnFromBlock`.

2. Create/wire prototypes and gameplay assets
- Create or duplicate the needed entity prototype/prefab pair for the item itself.
- If projectile-based, create/wire projectile asset + projectile prototype.
- Ensure `SimulationConfig` references are set when applicable (for example `SuperBallPrototype`).

3. Add to gamemode random pools
- In gamemode assets, add the powerup into `AllCoinItems` using Inspector object selection.
- Do not type numeric IDs by hand in YAML.
- Verify `FallbackCoinItem` remains intentional (commonly Mushroom).

4. Verify Quantum asset identity
- Open `Window/Quantum/Quantum Unity DB`.
- Confirm the new asset exists and has correct type (`PowerupAsset` / `CoinItemAsset` chain).
- Confirm the asset is loadable and not shown missing.
- If selection in `AllCoinItems` snaps back to `None`, asset identity is invalid or unresolved.

5. Regenerate and refresh
- Run `Tools/Quantum/CodeGen/Run Qtn CodeGen`.
- Reimport `Assets/QuantumUser/Resources/QuantumUnityDB.qunitydb`.
- Save assets and restart editor once.

6. Runtime verification
- Enter play mode.
- Collect enough coins to trigger reward spawn.
- Validate item spawns and that gameplay behavior (state transitions, projectile spawn, reserve sprite) works.

7. Player costume/shader setup (if the powerup changes Mario/Luigi look)
- Decide which `PowerupState` index should be used by the player shader.
  - Current mapping is in `Assets/Scripts/Entity/Player/MarioPlayerAnimator.cs` (`ps` switch inside `HandleMiscStates`).
  - Example: `FireFlower => 1`, `SuperBallFlower => 5`.
- If using a new shader state index, ensure shader state range supports it in both graphs:
  - `Assets/Shaders/3D/PlayerShader.shadergraph`
  - `Assets/Shaders/3D/RainbowPlayerShader.shadergraph`
  - Update `PowerupState` property max range (for example from `1` to `5`).
- If the shader uses the player texture array path (default setup), add texture slices for the new state:
  - Source arrays:
    - `Assets/Models/Players/mario_big/mario_big.png`
    - `Assets/Models/Players/luigi_big/luigi_big.png`
  - Append new costume rows to the bottom of each image (same width/layout as existing rows).
  - Increase `flipbookRows` in:
    - `Assets/Models/Players/mario_big/mario_big.png.meta`
    - `Assets/Models/Players/luigi_big/luigi_big.png.meta`
  - Example: `20 -> 24` when appending four 64px rows.
- Reimport the updated textures and shader graphs in Unity (or restart editor).
- Verify in play mode that collecting the powerup updates costume colors/textures as expected.

## Randomness Expectations

`GetRandomItem(...)` is weighted, not uniform.

- Mushroom usually has much larger `SpawnChance` than many special items.
- Seeing "mostly Mushroom" can be correct under current weights.
- If behavior is "only Mushroom", verify pool eligibility and fallback conditions below.

## Eligibility Filters That Remove Items

In `GamemodeAsset.GetRandomItem(...)`, items can be filtered out by:
- stage settings (`BigPowerup`, `VerticalPowerup` support),
- room rules (`CustomPowerupsEnabled`, lives),
- block-origin gating (`CanSpawnFromBlock` when `fromBlock == true`),
- singleton gating (`OnlyOneCanExist`),
- mega uniqueness constraint.

If all candidates are filtered, fallback item is returned.

## Troubleshooting

1. Crash at `GamemodeAsset.GetRandomItem` line with `coinItem.BigPowerup`
- Cause: one `AllCoinItems` entry resolved to null (`f.FindAsset` failed).
- Fix path:
  - Verify that ID exists in Quantum Unity DB.
  - Reassign `AllCoinItems` entries via Inspector.
  - Rebuild Quantum DB and codegen.

2. Cannot assign powerup in `AllCoinItems` (selection reverts to `None`)
- Cause: invalid or unresolved Quantum identity for that asset.
- Fix path:
  - Recreate asset by duplicating a known-good powerup asset in Unity.
  - Rewire fields on duplicate.
  - Re-add to `AllCoinItems` via Inspector.
  - Rebuild Quantum DB and codegen.

3. Mostly Mushroom drops
- Check actual `SpawnChance` values and `LosingSpawnBonus` for all candidate items.
- Confirm room rules and stage filters are not excluding most items.
- Confirm this is not fallback collapse.

4. Player becomes solid black / wrong costume color
- Cause (common): shader state index points to a path with no valid texture data.
- Fix path:
  - Confirm `MarioPlayerAnimator` sets the intended `PowerupState` shader value.
  - Confirm shader graph `PowerupState` range includes that value.
  - Confirm player texture-array source images contain rows for that state.
  - Confirm `flipbookRows` matches the new total rows and assets were reimported.

## Files Typically Touched

- `Assets/QuantumUser/Resources/AssetObjects/Items/<NewPowerup>.asset`
- `Assets/QuantumUser/Resources/AssetObjects/Gamemodes/CoinRunnersGamemode.asset`
- `Assets/QuantumUser/Resources/AssetObjects/Gamemodes/StarChasersGamemode.asset`
- `Assets/QuantumUser/Resources/EntityPrototypes/Items/<NewPowerup>.prefab`
- `Assets/QuantumUser/Resources/AssetObjects/Projectile/<NewProjectile>.asset` (if needed)
- `Assets/QuantumUser/Resources/QuantumDefaultConfigs.asset` (if config wiring needed)
- `Assets/QuantumUser/Simulation/NSMB/Entity/Powerup/Powerup.qtn` and generated code if adding new `PowerupState`
- `Assets/Scripts/Entity/Player/MarioPlayerAnimator.cs` (shader `PowerupState` mapping)
- `Assets/Shaders/3D/PlayerShader.shadergraph` (standard player shader)
- `Assets/Shaders/3D/RainbowPlayerShader.shadergraph` (starman/rainbow shader)
- `Assets/Models/Players/mario_big/mario_big.png` + `.meta` (texture array source + row count)
- `Assets/Models/Players/luigi_big/luigi_big.png` + `.meta` (texture array source + row count)

## Commit Checklist

- New powerup appears in Quantum Unity DB with correct type.
- `AllCoinItems` references persist after re-open/restart.
- No runtime null refs on coin reward spawn.
- Expected weighted variety observed over enough samples.
- No unrelated gameplay logic changes in coin counting/random systems.
