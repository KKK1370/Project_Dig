---
name: born-to-dig-validation
description: Validate BORN TO DIG / Project_Dig Unity changes after implementation or bug fixes using compile/Console checks, current project-specific verifier entry points, Scene/Prefab/reference inspection, Play Mode checks, and Git diff review. Use before declaring a project change complete or when asked to audit Unity asset integrity and regressions.
---

# BORN TO DIG Validation

Select checks based on the files and systems changed. Read `AGENTS.md` plus the `Verification Methods` and `Do Not Break` sections of `docs/CODEX_PROJECT_STATE.md` first; verifier preconditions can differ from the current Scene.

## 1. Establish the expected diff

1. Record branch and pre-existing `git status --short` before validation.
2. Run `git diff --name-status` and `git diff --stat` with the repository's existing safe-directory handling if needed.
3. Classify every changed path as intended, pre-existing, generated, or unexpected.
4. Stop and investigate unexpected `.unity`, `.prefab`, `.meta`, ProjectSettings, import setting, model, texture, audio, or other binary changes.

Do not discard pre-existing user changes. Do not use an installer, builder, generator, or automatic Scene save as a validation shortcut; those operations can mutate assets.

## 2. Compile and inspect Console evidence

- Use the Unity version from `ProjectSettings/ProjectVersion.txt` (baseline `6000.5.7f1`); re-read the file before running Unity.
- Open/import the project or run the matching Unity Editor in batch mode.
- Treat nonzero exit, compiler errors, import errors, exceptions, assertion failures, and relevant red Console entries as failures until explained.
- Document related warnings without automatically treating every warning as a failure.
- Keep Editor-only API out of Runtime assemblies and confirm referenced namespaces/packages exist.
- Capture the relevant log path and decisive error/pass lines.

If the active Editor locks batch mode, use it for Console/Play Mode checks or report the lock as a limitation. Do not equate a generated `.csproj` build with full Unity import/serialization validation.

## 3. Run applicable existing verifiers

Inspect the implementation and current Scene preconditions before first use. Current documented entry points are:

- Voxel/FPS Edit Mode smoke: `BornToDig.EditorTools.VoxelRockMvpVerifier.VerifyBatch` (currently incompatible with a `VoxelRockMVP` Scene that contains no VoxelRock).
- Gold Edit Mode: `BornToDig.EditorTools.GoldNuggetMvpVerifier.VerifyBatch`.
- Gold Play Mode: `BornToDig.EditorTools.GoldNuggetMvpPlayModeVerifier.VerifyBatch`.
- Pebble A/B/C Edit Mode: `BornToDig.EditorTools.DestructiblePebbleVerifier.VerifyAllBatch`.
- Pebble Play Mode: `BornToDig.EditorTools.DestructiblePebblePlayModeVerifier.VerifyRockABatch`.
- Pebble cluster Edit Mode: `BornToDig.EditorTools.PebbleRockTestVerifier.VerifyEditModeBatch`.
- Pebble cluster Play Mode: `BornToDig.EditorTools.PebbleRockTestPlayModeVerifier.VerifyPlayModeBatch`.

Invoke verifier methods with Unity `-executeMethod` when appropriate. Confirm the explicit final PASS log and clean process completion; a Play Mode verifier is not complete merely because Play Mode started. Historical PASS results do not validate a newer working tree. Search again before stating that no project-owned NUnit/Test Runner suite applies.

## 4. Inspect Unity references when relevant

For every changed serialized asset, check:

- Missing Script and missing object references.
- Prefab links, overrides, `.meta`/GUID continuity, and expected active/enabled states.
- Exactly one enabled Camera, AudioListener, gameplay crosshair, FPS Player, `MiningTool`, and applicable bootstrap/manager.
- Layer, Tag, Trigger, collider type/size/convex state, Rigidbody kinematic/gravity need, and ray obstruction.
- SerializedField assignments and Prefab/Scene composition.
- `MeshFilter.sharedMesh`/`MeshCollider.sharedMesh` synchronization after Voxel mining.
- Runtime/Editor separation and asmdef compatibility.

Do not repair unrelated findings during validation; report them separately.

## 5. Exercise relevant Play Mode behavior

Select checks proportional to the change and record what was actually observed:

- FPS: movement, look, jump, sprint, cursor release/recapture, and one active camera/listener.
- Pickaxe/mining: held input, recapture-click gate, cadence synchronization, center ray, surface hit, repeated deepening or Pebble removal.
- Voxel rock: initial mesh, visible material, collider update, and density event.
- DestructiblePebble: expected hit count, Intact-to-Fractured replacement, five dynamic fragments only after break, stable physics, lifetime cleanup, and no always-on Rigidbody in the intact cluster.
- Skill: point gain, Tab/start toggle, time/input pause, upgrade cost, and mining/swing interval update.
- Treasure: initial hidden state, exposure transition, center targeting, distance gate, E/gamepad pickup, single collection, UI update, and delayed CLEAR.
- UI/Input: one crosshair, objective/prompt visibility, Japanese text, Tab/pause behavior, and New Input System controls.

Do not mark manual or Play Mode checks passed from static code inspection.

## 6. Final diff audit

1. Re-run `git diff --check`, `git diff --stat`, `git diff --name-status`, and `git status --short` after Unity exits.
2. Inspect targeted patches for Unity-generated noise and unintended Scene serialization/import changes.
3. Confirm game code, Scene, Prefab, Material, Texture, Model, ProjectSettings, and binaries changed only when intended.
4. Review `docs/CODEX_PROJECT_STATE.md` only for changes that meet its update threshold.

## 7. Report results

Separate results into:

- **Passed** — command/check and concrete evidence.
- **Failed** — exact failure and affected scope.
- **Not run** — reason and remaining risk.
- **Diff audit** — intended, pre-existing, and unexpected files.

Never use “all verified” when Unity Console, Play Mode, serialized references, or diff checks were not actually observed.
