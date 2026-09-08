---
name: born-to-dig-unity
description: Add or change Unity gameplay, UI, Scene, Prefab, or Editor functionality in BORN TO DIG / Project_Dig with minimal, dependency-aware edits while preserving its mining, FPS, treasure, and destruction integrations. Use for Project_Dig feature work; use born-to-dig-debug instead when the primary task is diagnosing a defect.
---

# BORN TO DIG Unity Development

## 1. Establish current context

1. Read repository-root `AGENTS.md` completely.
2. Read `docs/CODEX_PROJECT_STATE.md`, including its review date, current Scene composition, known problems, `Do Not Break`, and verification entries.
3. Record the current branch, `git status --short`, and relevant existing diffs. Preserve all unrelated user changes.
4. Identify the requested user-visible outcome and separate it from optional refactoring.
5. Inspect only the related Runtime code, Editor code, Scene/Prefab YAML, `.meta` files, settings, and direct dependencies.

Treat Project State as a map, not proof. Confirm volatile values and references in the actual files. Distinguish confirmed code/serialization from inferred design; if the request depends on an unresolved product choice, report it instead of silently making it permanent.

## 2. Map the impact surface

- Trace input → controller/tool → gameplay state → events → UI for the requested behavior.
- Search call sites, event subscribers, serialized references, builders, verifiers, and legacy compatibility paths.
- Decide whether code-only implementation is sufficient. Change Scene/Prefab/ProjectSettings only when the feature requires it.
- Note affected Camera, Collider, Rigidbody, Layer, Tag, and SerializedField relationships.
- Preserve `.meta` files and GUIDs. Avoid moving assets unless the task requires it.

## 3. Implement a minimal compatible change

- Keep existing ownership boundaries: Pickaxe for visuals, `MiningTool` for hit dispatch, `VoxelRock`/`VoxelGrid`/`MarchingCubes` for Voxel state and mesh, DestructiblePebble for fracture, and Gold/Manager/UI for pickup and clear.
- Extend an existing event or narrow API when appropriate; do not duplicate mining, exposure, pickup, input, or clear flows.
- Match existing namespaces, serialized-field style, New Input System access, and Runtime/Editor separation.
- Before changing a Scene or Prefab, identify its source Prefab, `.meta` GUIDs, layers, colliders, overrides, and references.
- Do not run a builder, installer, or generator merely to save manual wiring; review every overwrite first.
- Preserve the single FPS Player, Main Camera, AudioListener, crosshair, `MiningTool`, and bootstrap invariants.
- Do not modify source models, materials, textures, import settings, or ProjectSettings unless the request requires them.
- Avoid repeated `Find` calls, per-frame allocations, unnecessary heavy work in `Update`, and speculative abstractions.

## 4. Validate the change

Read and apply sibling Skill `../born-to-dig-validation/SKILL.md`.

At minimum:

1. Review the complete diff.
2. Compile with the installed Unity version when available and check Console errors.
3. Run a relevant existing verifier only when its current preconditions match.
4. Inspect Scene/Prefab references when serialized assets changed.
5. Exercise the changed Play Mode path and nearby mining/FPS/Pebble/treasure regressions when possible.

Never report an unrun check as passed.

## 5. Update long-term state selectively

Before finishing, reread `docs/CODEX_PROJECT_STATE.md`. Update it only if the work changed a major system, dependency, Scene, major Prefab, design decision, verification method, confirmed known problem, or critical bug behavior. Skip updates for typos, small tuning, temporary diagnostics, and minor localized fixes.

Put reusable debugging lessons or project quirks in Codex Memories only when the environment and user policy permit; do not duplicate current architecture there.

## 6. Report

Report the requested outcome and result, files changed and why, Unity/Play Mode/verifier checks actually run, unverified items and blockers, Project State update decision, and final Git diff. Explicitly confirm any intended Scene/Prefab/binary change and identify pre-existing working-tree changes left untouched. Do not commit or push unless explicitly requested.
