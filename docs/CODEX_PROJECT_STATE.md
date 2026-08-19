# BORN TO DIG — Codex Project State

Last verified: **2026-08-18**  
Verified branch/commit at investigation start: **`test` / `52ac306`**

This file records current project state, not permanent rules. Permanent rules live in `AGENTS.md`; reusable procedures live in `.agents/skills/`.

## Project Overview

- **BORN TO DIG** is a first-person mining MVP in Unity project **Project_Dig**.
- Unity Editor: **6000.5.7f1**.
- Render pipeline: **URP 17.5.0**.
- Input System package: **1.20.0**; `activeInputHandler: 1` selects the New Input System.
- Other installed packages include uGUI 2.5.0, Test Framework 1.7.0, AI Navigation, AI Inference, Timeline, Visual Scripting, and Meshy integration.
- Project-owned game Runtime code has no asmdef and compiles into `Assembly-CSharp`; code under `Editor/` compiles into `Assembly-CSharp-Editor`. The only project asmdef found is `Assets/ai.meshy/Editor/Script/MeshyAssembly.asmdef`.

## Current MVP Goal

Confirmed implemented and verified-by-code target loop:

`FPSで岩へ接近する → ツルハシでVoxelRockを掘る → 埋没した金塊を露出させる → 中央注視してEで取得する → MVP CLEARを表示する`

The implemented MVP also includes a mining-skill screen that converts mined amount into Skill Points and shortens the mining/swing interval. A broader product goal beyond this loop is not documented in the inspected files; do not infer one.

## Architecture

1. `FpsCharacterController` owns movement, look, jump, sprint, and cursor capture.
2. `PickaxeViewModel` reads the same attack input for first-person swing visuals; it does not mine the rock.
3. `MiningTool` on `Main Camera` raycasts from screen center and calls `VoxelRock.Mine()` only when the hit collider is that rock's `RockCollider`.
4. `VoxelRock` owns a `VoxelGrid`, generates a runtime mesh through `MarchingCubes`, and assigns the same mesh to `MeshFilter` and `MeshCollider`.
5. Successful carving emits `VoxelRock.DensityRemoved`.
6. `MiningSkillProgression` consumes removed density for Skill Points and synchronizes `MiningTool` and `PickaxeViewModel` intervals.
7. `GoldNuggetMVP` also reacts to density changes, samples the rock around its collider, and becomes collectible once sufficiently exposed.
8. `MVPGameManager` consumes collection events; `MVPUI` updates objective, prompt, and clear state.

`CharacterMvpHud` and `MiningSkillProgression` use IMGUI. Gold objective/prompt/clear UI uses a separate Screen Space Overlay Canvas with TextMeshPro.

## Important Files

### Project and configuration

- `ProjectSettings/ProjectVersion.txt` — authoritative Unity version.
- `Packages/manifest.json` — direct package dependencies.
- `ProjectSettings/ProjectSettings.asset` — includes Active Input Handling.
- `ProjectSettings/EditorBuildSettings.asset` — registered build scenes.
- `Assets/InputSystem_Actions.inputactions` — Player/UI action maps; main Runtime scripts currently poll devices directly instead of using `PlayerInput`.

### Voxel mining Runtime

- `Assets/BornToDig/VoxelRock/Scripts/MiningTool.cs` — camera-center mining input, interval gate, raycast, and `VoxelRock.Mine()` call.
- `Assets/BornToDig/VoxelRock/Scripts/VoxelRock.cs` — source-model voxelization, runtime mesh/collider rebuild, density removal event, and world-density query API.
- `Assets/BornToDig/VoxelRock/Scripts/VoxelGrid.cs` — scalar density storage, trilinear sampling, gradient sampling, and spherical carving.
- `Assets/BornToDig/VoxelRock/Scripts/VoxelMeshVoxelizer.cs` — fills the density grid from the closed source mesh.
- `Assets/BornToDig/VoxelRock/Scripts/MarchingCubes.cs` — converts the scalar field to runtime mesh data.

### Player and mining skill Runtime

- `Assets/FpsCharacterMVP/Runtime/FpsCharacterController.cs` — CharacterController-based FPS and cursor state.
- `Assets/FpsCharacterMVP/Runtime/PickaxeViewModel.cs` — visual pickaxe construction/swing; interval can be updated by the skill system.
- `Assets/FpsCharacterMVP/Runtime/DwarfVisualSlot.cs` — optional future character-visual Prefab slot; currently unassigned in the MVP scene.
- `Assets/FpsCharacterMVP/Runtime/CharacterMvpHud.cs` — IMGUI controls help and crosshair.
- `Assets/MiningSkillMVP/Runtime/MiningSkillProgression.cs` — auto-bootstrapped mining points, speed upgrades, skill UI, and compatibility with current/legacy rocks.

### Treasure and UI Runtime

- `Assets/BornToDig/GoldNuggetMVP/Runtime/GoldNuggetMVP.cs` — exposure sampling, targeting, E/gamepad pickup, and collection events.
- `Assets/BornToDig/GoldNuggetMVP/Runtime/MVPGameManager.cs` — one-item count and delayed clear state.
- `Assets/BornToDig/GoldNuggetMVP/Runtime/MVPUI.cs` — TextMeshPro objective, prompt, clear panel, and runtime Japanese font asset.

### Editor tools and validation

- `Assets/BornToDig/VoxelRock/Editor/VoxelRockMvpSceneBuilder.cs` — creates the voxel MVP scene; can overwrite current tuning.
- `Assets/BornToDig/VoxelRock/Editor/VoxelRockFpsCompatibility.cs` — integrates FPS and enforces a single camera/listener/crosshair arrangement.
- `Assets/BornToDig/VoxelRock/Editor/VoxelRockMvpVerifier.cs` — voxel generation, collider, repeated carving, penetration, and FPS-compatibility smoke verifier.
- `Assets/FpsCharacterMVP/Editor/FpsCharacterBuilder.cs` — builds the FPS object and Prefab; do not rerun casually on an adjusted player.
- `Assets/BornToDig/GoldNuggetMVP/Editor/GoldNuggetMvpInstaller.cs` — creates/installs gold assets and scene setup; can overwrite scene tuning.
- `Assets/BornToDig/GoldNuggetMVP/Editor/GoldNuggetMvpVerifier.cs` — Edit Mode and Play Mode verification for treasure/UI plus existing FPS/mining.

### Legacy and auxiliary code

- `Assets/Scripts/ClickableVoxelRock.cs` — legacy 32³ bool-grid rock with exposed-face cube mesh; not active in the current MVP scene.
- `Assets/Scripts/FlyCameraController.cs` — legacy test camera; not active in the current MVP scene.
- `Assets/ai.meshy/` — Meshy Editor integration, outside the core mining loop.
- `Assets/TutorialInfo/` and root `Readme.asset` — Unity template-derived auxiliary content.

## Scenes

- `Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity` — current MVP scene. Key roots/components: `Voxel Rock`, `MVP_FPS_Player`, `Main Camera` + `MiningTool`, `GoldNugget_MVP`, `MVP_GameManager`, `MVP_UI`, `Ground`, and `Directional Light`.
- `Assets/Scenes/SampleScene.unity` — older/separate test scene containing legacy approaches; do not treat it as the current MVP.

Current Build Settings registers only `Assets/Scenes/SampleScene.unity`. Open `VoxelRockMVP` explicitly for current MVP verification.

## Prefabs

- `Assets/FpsCharacterMVP/Prefabs/MVP_FPS_Player.prefab` — player root, `CameraPivot`, `Main Camera`, `PickaxeViewModel`, simple `Handle`/`Metal Head`/`Left Tip` pickaxe parts, and future dwarf model root.
- `Assets/BornToDig/GoldNuggetMVP/Prefabs/GoldNugget_MVP.prefab` — Layer 2 (`Ignore Raycast`), trigger `BoxCollider`, no Rigidbody, gold behaviour, and visual model child.

Related source assets:

- Rock: `Assets/BornToDig/VoxelRock/Models/BORN_TO_DIG_Rock.fbx`, `Assets/BornToDig/VoxelRock/Materials/BORN_TO_DIG_Rock.mat`, and original `Assets/BornToDig/VoxelRock/Source/BORN_TO_DIG_Rock.glb`.
- Gold: `Assets/BornToDig/GoldNuggetMVP/Models/GoldNugget_MVP.fbx` exported from `お宝.blend`, `Assets/BornToDig/GoldNuggetMVP/Materials/GoldNugget_MVP.mat`, and Japanese font `Assets/BornToDig/GoldNuggetMVP/Fonts/NotoSansJP-VF.ttf`.
- TextMeshPro base resources: `Assets/TextMesh Pro/`.

## Player

- `MVP_FPS_Player` uses `CharacterController` and an existing child `Main Camera` under `CameraPivot`.
- Keyboard controls: WASD/arrow movement, mouse look, Space jump, left Shift sprint, Esc release cursor, left click recapture.
- Gamepad movement/look/jump/sprint are read directly from the current gamepad.
- `DwarfVisualSlot` keeps a future visual Prefab separate from movement, camera, and mining; the scene slot is currently empty.
- `FpsCharacterBuilder` can inherit an existing Camera when generating the FPS setup, but can also rewrite the player/Prefab and must not be rerun casually.

## Mining System

- Main attack input is held left mouse or gamepad right trigger.
- `PickaxeViewModel` animates at a default 0.48-second interval.
- `MiningTool` performs the actual raycast at its interval and ignores triggers.
- The current scene has tuned `MiningTool` values: distance `15.186258`, radius `0.585`, strength `1.528`, interval `0.48`, center ray enabled, its own crosshair disabled, and previous-cursor-lock required.
- Cursor recapture clicks are gated so they do not mine immediately.
- `MiningSkillProgression` is created after scene load when absent. It consumes `DensityRemoved`, opens with Tab/gamepad Start, pauses time, blocks FPS input, and updates both mining and swing intervals.

## Rock / Voxel System

- `VoxelRock.Initialize()` transforms the readable source mesh into rock-local space, expands bounds, fills a `48³` `VoxelGrid`, hides the source renderers/colliders, and builds the runtime mesh.
- Initialization requires the imported source mesh to be Read/Write enabled. Hiding the source only disables its renderers/colliders; it does not delete unrelated GameObjects.
- Current scene values: resolution `48`, iso level `0.5`, bounds padding `0.06`, rock scale `(5,5,5)`.
- `VoxelMeshVoxelizer.FillFromMesh()` fills the closed-mesh interior with density `1`; empty space is `0`.
- `VoxelRock.Mine()` calls `VoxelGrid.CarveSphereAmount()` in world space. Mesh/collider rebuild and `DensityRemoved` occur only when density changed.
- `VoxelRock.SampleDensityWorld()` and `IsSolidAtWorldPoint()` are the public read-only bridge used by treasure exposure logic. Reuse these instead of introducing a second voxel data owner.

## Destruction System

Current destruction is the `VoxelRock` density-carving and runtime mesh/collider rebuild described above.

No file, type, or asset named `DestructiblePebble` (or containing `Pebble`) was found in the project outside generated Unity directories on 2026-08-18. Do not assume a DestructiblePebble system exists on this branch; search again if the branch changes or a future task mentions it.

## Treasure System

- One independent `GoldNugget_MVP` instance is placed inside the rock at approximately `(0.738, 1.690, -3.427)`.
- The Edit Mode verifier expects the gold center and sampled surface to start inside solid rock; this is the confirmed initial-burial contract.
- The gold Prefab is on `Ignore Raycast` so it does not block `MiningTool`, whose Physics raycast ignores triggers.
- `GoldNuggetMVP` samples 14 directions just outside its collider after startup and each density-removal event. Exposure becomes permanent at the configured 50% empty-sample threshold.
- Collection requires exposure, distance at most `2.75m`, a camera-center ray hit against the trigger, and E/gamepad West.
- Collection disables renderers and collider, emits `Collected`, updates count to `1`, and shows clear after `0.75s`. It does not change scene or quit the app.
- Inventory, multiple treasure items, persistence, save data, and scene transitions are not implemented in this MVP.

## UI

- `CharacterMvpHud`: IMGUI help and the single gameplay crosshair.
- `MiningSkillProgression`: IMGUI compact mining status and Tab skill screen.
- `MVP_UI`: Screen Space Overlay Canvas with `ObjectiveText`, `PickupPrompt`, and `ClearPanel` containing `ClearTitle`/`ClearSubtitle`.
- `MVPUI` creates a runtime TMP font asset from Noto Sans JP and sets the Japanese objective/prompt/clear strings.
- Current strings are `金塊を探す 0 / 1`, `E 金塊を拾う`, `金塊を入手！ 1 / 1`, `MVP CLEAR`, and `金塊を発見しました！`.
- `MiningTool.showCrosshair` is disabled in the MVP scene so the FPS HUD remains the single crosshair.

## Input

- `Assets/InputSystem_Actions.inputactions` defines `Player` and `UI` maps, including Move, Look, Attack, Interact, Crouch, Jump, Previous, Next, and Sprint actions.
- Current main Runtime scripts directly read `Keyboard.current`, `Mouse.current`, and `Gamepad.current`; they do not use `PlayerInput` or a generated action wrapper.
- Conditional legacy input remains in `MiningTool`, but current Project Settings select the New Input System.
- A migration to action-driven input would be a separate architectural change, not a local feature edit.

## Important Decisions

- Keep visual pickaxe animation separate from mining collision/density logic.
- Keep `VoxelRock` as the sole owner of current voxel density and expose narrow read-only queries/events for other systems.
- Keep treasure as an independent trigger object rather than embedding it into generated rock mesh data.
- Keep a single enabled Camera, AudioListener, and gameplay crosshair in the MVP scene.
- Keep the future dwarf visual replaceable through `DwarfVisualSlot` without coupling it to movement, camera, or mining.
- Keep the mining-skill system compatible with both current `VoxelRock.DensityRemoved` and legacy `ClickableVoxelRock.VoxelsRemoved` until that compatibility is deliberately removed.
- Keep Editor builders/installers separate from Runtime; treat them as potentially destructive regeneration tools.

## Known Problems

- `VoxelRockMVP.unity` is not registered in Build Settings; only `SampleScene.unity` is registered.
- Scene-tuned mining values differ substantially from `VoxelRockMvpSceneBuilder`/README defaults. Rebuilding the scene can reset the current feel and gold integration.

No current Unity Console status was assumed during the static investigation. Record new confirmed errors here only when reproduced and still relevant.

## Technical Debt

- Project-owned game code has no dedicated Runtime/Editor asmdefs, so most code uses the broad default assemblies.
- No project-owned NUnit/Test Runner tests were found. Validation depends on custom Editor verifier entry points and manual Play Mode checks.
- UI is split across two IMGUI systems and a TextMeshPro Canvas.
- Main gameplay input bypasses the existing Input Actions asset and directly polls devices.
- `MiningSkillProgression` retains current and legacy rock paths, increasing compatibility surface.

These are recorded facts, not authorization for unrelated refactoring.

## Do Not Break

- `MiningTool` must continue hitting only the current `VoxelRock.RockCollider`; new triggers/colliders must not steal the mining ray.
- `MeshFilter.sharedMesh` and `MeshCollider.sharedMesh` must stay synchronized after carving.
- `VoxelRock.DensityRemoved` feeds both mining progression and gold exposure.
- `MiningSkillProgression` must not be duplicated; its Bootstrap creates one when missing.
- `MiningTool` and `PickaxeViewModel` mining/swing intervals are intentionally synchronized by the skill system.
- Gold scene references to `VoxelRock`, `Main Camera`, `MVPGameManager`, and `MVPUI` must remain valid.
- Keep exactly one enabled scene Camera, AudioListener, and gameplay crosshair.
- Do not enable `FlyCameraController` with `FpsCharacterController`, or activate `ClickableVoxelRock` in `VoxelRockMVP`.
- Do not rerun scene builders/installers without comparing the existing scene/prefab and preserving tuned values and integrations.
- Preserve `.meta` files and GUID-based references when moving or renaming Unity assets.

## Verification Methods

### Available automated/custom checks

- Unity import/compile: open the project in Unity 6000.5.7f1 or run that Editor in batch mode and inspect the exit code/log for compiler and import errors.
- Voxel Edit Mode smoke verifier: `BornToDig.EditorTools.VoxelRockMvpVerifier.VerifyBatch`.
- Gold Edit Mode verifier: `BornToDig.EditorTools.GoldNuggetMvpVerifier.VerifyBatch`.
- Gold Play Mode verifier: `BornToDig.EditorTools.GoldNuggetMvpPlayModeVerifier.VerifyBatch`.

The verifier methods are implemented under the Editor folders and can be used with Unity `-executeMethod`. The gold Play Mode verifier enters Play Mode and requires its completion log, not just process startup.

### Required change-sensitive checks

- Compile/Console: no C# compile errors and no new red Console errors.
- Scene/Prefab: Missing Script, Missing Reference, serialized links, Prefab overrides, Camera/AudioListener/crosshair duplication.
- Physics: Collider type/enabled state, Rigidbody need, Trigger, Layer, Tag, ray obstruction, and collider/mesh synchronization.
- Play Mode when relevant: FPS movement/look/jump/cursor, pickaxe swing, repeated mining/deepening/penetration, gold exposure/target/pickup, UI clear, and mining-skill pause/upgrade.
- Git: `git status`, `git diff --stat`, `git diff --name-status`, and targeted diff review. Confirm no unintended `.unity`, `.prefab`, `.meta`, ProjectSettings, import setting, or binary changes.

Do not claim any check that was not actually run. If Unity cannot be launched, report static checks and remaining Play Mode/Console work separately.

## Recent Significant Changes

- **2026-08-18 (`8d8b2a1`)**: Added the gold nugget MVP, UI, Editor installer/verifiers, density query API, and integrated the treasure loop into `VoxelRockMVP`.
- **2026-08-18 (`163063d`)**: Added mining Skill Points/speed progression and interval synchronization across current and legacy mining paths.
- **2026-08-17 (`bceacef`)**: Integrated the FPS player with the voxel-rock MVP and added compatibility/smoke verification.
- **2026-08-17 (`5c27439`)**: Added the FPS character Runtime, builder, HUD, pickaxe view model, and visual slot.
- **2026-08-18 (working tree setup)**: Added the Codex 4-layer knowledge system: stable rules, current Project State, project Skills, and Memory placement guidance. No game assets or game Runtime code were intentionally changed.
