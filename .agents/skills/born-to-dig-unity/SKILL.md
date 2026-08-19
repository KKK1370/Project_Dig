---
name: born-to-dig-unity
description: Add or change Unity gameplay, UI, Scene, Prefab, or Editor functionality in the BORN TO DIG / Project_Dig project with minimal, dependency-aware edits and project-specific verification. Use for feature implementation, system extension, or intentional Unity asset changes in this project; use born-to-dig-debug instead when the primary task is diagnosing a bug.
---

# BORN TO DIG Unity Development

## 1. Load project context

1. Read repository-root `AGENTS.md` completely.
2. Read `docs/CODEX_PROJECT_STATE.md` completely.
3. Identify the requested user-visible outcome and explicitly separate it from optional refactoring.
4. Inspect only the related Runtime code, Editor code, Scene/Prefab YAML, `.meta` files, and settings needed for the change.

Treat Project State as a map, not proof. Confirm volatile values and references in the actual files before editing.

## 2. Map the impact surface

- Trace input → controller/tool → gameplay state → events → UI for the requested behavior.
- Search call sites, event subscribers, serialized references, builders, verifiers, and legacy compatibility paths.
- Decide whether code-only implementation is sufficient. Change Scene/Prefab/ProjectSettings only when the feature requires it.
- Note the affected Camera, Collider, Rigidbody, Layer, Tag, and SerializedField relationships.
- Preserve `.meta` files and GUIDs. Avoid moving assets unless the task requires it.

## 3. Implement minimally

- Follow the local namespace, naming, serialization, and lifecycle style.
- Extend the current `VoxelRock`/`MiningTool` event and query boundaries instead of creating a competing voxel/mining data owner.
- Keep pickaxe visuals separate from mining physics.
- Follow the current New Input System device-polling style unless an input architecture migration is explicitly requested.
- Avoid repeated `Find` calls, per-frame allocations, unnecessary heavy work in `Update`, and speculative abstractions.
- Do not run scene builders/installers as a shortcut without first reviewing what they overwrite.
- Do not add duplicate Camera, AudioListener, crosshair, player, `MiningTool`, or bootstrap manager.

## 4. Validate the change

Read and apply sibling Skill `../born-to-dig-validation/SKILL.md`.

At minimum:

1. Review the complete diff.
2. Compile with the installed Unity version when available.
3. Check Unity Console errors.
4. Run the relevant existing verifier or explain why it does not apply.
5. Inspect Scene/Prefab references when serialized assets changed.
6. Exercise the changed Play Mode path and nearby mining/FPS/treasure regressions when possible.

Never report an unrun check as passed.

## 5. Update long-term state selectively

Before finishing, inspect `docs/CODEX_PROJECT_STATE.md`. Update it only if the work changed a major system, important dependency, Scene, major Prefab, design decision, verification method, confirmed known problem, or critical bug behavior. Skip state updates for typos, small tuning, temporary debug work, and minor localized fixes.

Put reusable debugging lessons or project quirks in Codex Memories only when the environment and user policy permit; do not duplicate current architecture there.

## 6. Report

Report:

- requested outcome and result;
- files changed and why;
- Unity/Play Mode/verifier checks actually run;
- unverified items and blockers;
- Project State update decision;
- final Git diff, including confirmation of any intended Scene/Prefab/binary change.

