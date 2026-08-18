---
name: born-to-dig-validation
description: Validate BORN TO DIG / Project_Dig changes after implementation, including Unity compilation, Console state, Scene/Prefab references, physics setup, Play Mode behavior, project-specific verifiers, and Git diff. Use after code or asset changes and for focused regression checks.
---

# BORN TO DIG Validation

Select checks based on the files and systems changed. Read `AGENTS.md` and the `Verification Methods` and `Do Not Break` sections of `docs/CODEX_PROJECT_STATE.md` first; Project_Dig verifier preconditions can differ from the current Scene.

## Preflight

- Record branch and pre-existing `git status --short` before validation.
- Identify every changed code, Scene, Prefab, `.meta`, import setting, package, or ProjectSettings file.
- Do not use an installer, builder, generator, or automatic Scene save as a validation shortcut; those operations can mutate assets.

## Compile and Console

- Let Unity complete script compilation and confirm there are no C# compile errors.
- Inspect Console red errors and exceptions. Do not treat warnings as errors automatically, but document warnings related to the change.
- Keep Editor-only API out of Runtime assemblies and confirm referenced namespaces/packages exist.

If Unity cannot run because of license, IPC, or environment constraints, perform static checks that are meaningful and explicitly list Play Mode/Console as unverified.

## Scene and Prefab Integrity

For every changed serialized asset, check:

- Missing Script and missing object references.
- Prefab link and overrides; `.meta`/GUID continuity.
- Expected active/enabled states and no duplicate manager, bootstrap, FPS Player, Camera, AudioListener, crosshair, or MiningTool.
- Layer, Tag, Trigger, collider shape, convex setting, Rigidbody kinematic/gravity state, and raycast obstruction.
- SerializedField values and references match the intended Scene composition.
- Runtime and Editor components are placed in their correct folders/assemblies.

## Gameplay Regression

- Mining: input timing, cursor gate, center ray, repeated hits, surface deepening or Pebble removal, and collider updates.
- FPS: movement, look, jump, sprint, cursor release/relock, one active camera/listener.
- Treasure: initially hidden state, exposure transition, distance/center targeting, E pickup, one collection, UI update, delayed CLEAR.
- DestructiblePebble: expected hit count, Intact-to-Fractured replacement, five dynamic fragments only after break, stable physics, lifetime cleanup, no always-on Rigidbody in the intact cluster.
- UI/Input: crosshair ownership, prompt visibility, Tab/pause behavior, and New Input System controls.

Run only the project-specific Edit/Play verifier whose current preconditions match the Scene. Entry points are documented in Project State; inspect their implementation before first use. A historical PASS does not validate a newer working tree.

## Diff Audit and Report

Run `git diff --check`, `git diff --stat`, `git diff --name-status`, `git status --short`, and inspect relevant patches. Confirm no unintended Scene, Prefab, Material, Model, Texture, Import, ProjectSettings, binary, or generated-file changes.

Report each check as passed, failed, or not run. For not-run checks, give the concrete reason and the exact user-side Play Mode action if useful. Never combine static YAML validation and runtime validation into one claim.
