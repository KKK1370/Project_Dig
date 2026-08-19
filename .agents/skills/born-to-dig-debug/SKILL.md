---
name: born-to-dig-debug
description: Diagnose and fix reproducible Unity bugs in BORN TO DIG / Project_Dig by tracing evidence through gameplay code, Scene/Prefab serialization, physics, input, and existing verifiers. Use for Console errors, broken mining/FPS/treasure/UI behavior, missing references, regressions, or Play Mode failures where root cause must be found instead of masked.
---

# BORN TO DIG Debugging

## 1. Define the failure

1. Read repository-root `AGENTS.md` and `docs/CODEX_PROJECT_STATE.md`.
2. Record expected behavior, actual behavior, reproduction steps, frequency, scene, input device, and relevant Console error/stack trace.
3. Distinguish a confirmed symptom from assumptions. If reproduction is unavailable, state that before forming conclusions.

## 2. Collect evidence

- Reproduce with the smallest reliable path when Unity is available.
- Preserve the first relevant error and full stack trace; later errors may be cascades.
- Trace the involved code path, serialized references, event subscriptions, lifecycle order, input/cursor state, Layer/Tag, Collider/Trigger/Rigidbody, and active Scene objects.
- Compare the current Scene/Prefab values with builders/installers and Project State.
- Use existing verifier entry points when they cover the failing subsystem.
- Review recent targeted Git history/diff when the problem is a regression.

Do not scan large binary assets unless evidence points to an import/model problem.

## 3. Test hypotheses

1. List the most likely causes in evidence order.
2. For each cause, name an observation that would confirm or reject it.
3. Inspect or reproduce until one cause explains the symptom and relevant evidence.
4. If evidence remains incomplete, label the diagnosis as a hypothesis rather than fact.

Do not add a generic null check, catch block, retry, object search, or disabled error path merely to hide the symptom. Add a guard only when the missing/late state is valid by design and the guard preserves correct behavior.

## 4. Apply the smallest root fix

- Change the source of the invalid state or broken dependency.
- Preserve the current mining pipeline, event relationships, Scene tuning, Prefab references, and input conventions unless they are the confirmed cause and the requested fix requires changing them.
- Avoid unrelated cleanup and refactoring.
- Add or extend a focused verifier only when it provides durable regression coverage and matches the project's existing validation style.

## 5. Reverify and check regression

Read and apply sibling Skill `../born-to-dig-validation/SKILL.md`.

- Run the original reproduction and confirm the exact symptom is gone.
- Test the nearest success and failure boundaries.
- Check adjacent FPS, mining, rock mesh/collider, skill progression, treasure exposure/pickup, and UI paths when connected.
- Confirm no new Console error, missing reference, duplicate object, ray obstruction, or unintended asset change.
- Review the complete Git diff.

## 6. Preserve the lesson

- Update `docs/CODEX_PROJECT_STATE.md` only when the fix changes current architecture, resolves/adds a significant known problem, or establishes a critical design decision.
- Propose/save a Codex Memory only for a reusable root cause, failed approach, recurring project quirk, or high-value diagnostic pattern when policy permits.
- Do not turn Project State into a chronological bug log.

## 7. Report

Report the reproduced symptom, root cause and evidence, minimal fix, verification performed, regression coverage, remaining uncertainty, files changed, and final Git diff. If the root cause was not proven, say so directly.

