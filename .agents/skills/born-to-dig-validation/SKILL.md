---
name: born-to-dig-validation
description: Validate BORN TO DIG / Project_Dig Unity changes after implementation or bug fixes using compile/Console checks, existing voxel and gold verifier entry points, Scene/Prefab/reference inspection, Play Mode checks, and Git diff review. Use before declaring any project change complete or when asked to audit Unity asset integrity and regressions.
---

# BORN TO DIG Validation

## 1. Establish the expected diff

1. Read repository-root `AGENTS.md` and `docs/CODEX_PROJECT_STATE.md`.
2. Run `git status`, `git diff --name-status`, and `git diff --stat` with the repository's existing safe-directory handling if needed.
3. Classify every changed path as intended, pre-existing, generated, or unexpected.
4. Stop and investigate unexpected `.unity`, `.prefab`, `.meta`, ProjectSettings, import setting, model, texture, audio, or other binary changes.

Do not discard pre-existing user changes.

## 2. Compile and inspect Console evidence

- Use Unity **6000.5.7f1** from `ProjectSettings/ProjectVersion.txt`; re-read the file if it changes.
- Open/import the project or run the matching Unity Editor in batch mode.
- Treat nonzero exit, compiler errors, import errors, exceptions, assertion failures, and red Console entries as failures until explained.
- Capture the relevant log path and decisive error/pass lines.
- If the project is already open and locks batch mode, use the active Editor for Console/Play Mode checks or report the lock as a limitation.

Do not equate a generated `.csproj` build with full Unity import/serialization validation.

## 3. Run applicable existing verifiers

Use only verifiers present in the current tree:

- Voxel/FPS Edit Mode smoke: `BornToDig.EditorTools.VoxelRockMvpVerifier.VerifyBatch`.
- Gold Edit Mode: `BornToDig.EditorTools.GoldNuggetMvpVerifier.VerifyBatch`.
- Gold Play Mode: `BornToDig.EditorTools.GoldNuggetMvpPlayModeVerifier.VerifyBatch`.

Invoke Editor verifier methods with Unity `-executeMethod` when appropriate. Confirm their explicit PASS log and clean process completion. The Play Mode verifier is complete only after its final pass/fail log, not when Play Mode starts.

No project-owned NUnit/Test Runner test suite existed at the 2026-08-18 baseline. Search again before stating that no formal tests apply.

## 4. Inspect Unity references when relevant

For changed Scene/Prefab/component work, check:

- Missing Script and missing object references;
- SerializedField assignments and Prefab overrides;
- exactly one enabled Camera, AudioListener, and gameplay crosshair in the MVP scene;
- no duplicate FPS player, `MiningTool`, `MiningSkillProgression`, or bootstrap result;
- Collider enabled/type/size, Trigger, Rigidbody need, Layer, Tag, and ray obstruction;
- `MeshFilter.sharedMesh`/`MeshCollider.sharedMesh` synchronization after mining;
- Editor/Runtime separation and asmdef compatibility;
- `.meta` presence and GUID/reference integrity after asset operations.

Do not repair unrelated findings during validation; report them separately.

## 5. Exercise relevant Play Mode behavior

Select checks proportional to the change:

- FPS: move, look, jump, sprint, cursor release/recapture.
- Pickaxe/mining: held input, recapture-click gate, cadence synchronization, surface hit, repeated deepening, penetration.
- Rock: initial mesh, visible material, collider update, density event.
- Skill: point gain, Tab/start toggle, time/input pause, upgrade cost, mining/swing interval update.
- Treasure: initial burial, exposure threshold, center targeting, distance gate, E/gamepad pickup, single collection.
- UI: one crosshair, objective, pickup prompt, clear delay/panel, Japanese text.

Record what was observed. Do not mark manual or Play Mode checks passed from static code inspection.

## 6. Final diff audit

1. Re-run `git status` and targeted diffs after Unity exits.
2. Check for Unity-generated noise and unintended scene serialization/import changes.
3. Confirm game code, Scene, Prefab, Material, Texture, Model, ProjectSettings, and binaries changed only when intended.
4. Review `docs/CODEX_PROJECT_STATE.md` only for changes that meet its update threshold.

## 7. Report results

Separate results into:

- **Passed** — command/check and concrete evidence;
- **Failed** — exact failure and affected scope;
- **Not run** — reason and remaining risk;
- **Diff audit** — intended, pre-existing, and unexpected files.

Never use “all verified” when Unity Console, Play Mode, serialized references, or diff checks were not actually observed.

