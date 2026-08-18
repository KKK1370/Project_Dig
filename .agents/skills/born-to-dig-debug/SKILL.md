---
name: born-to-dig-debug
description: Diagnose and fix reproducible Unity gameplay, serialization, reference, physics, input, or Editor-tool bugs in BORN TO DIG / Project_Dig. Use when Project_Dig behavior is broken or a verifier fails; do not use for feature requests without a defect.
---

# BORN TO DIG Debugging

## Build Evidence Before Editing

1. Read `AGENTS.md`, `docs/CODEX_PROJECT_STATE.md`, and the current Git state.
2. Capture the exact symptom, affected Scene, expected behavior, reproduction steps, Console text, and whether the result is Edit Mode, Play Mode, or Player-build specific.
3. Inspect the failing component and its direct data path. For Scene/Prefab issues, trace `.meta` GUIDs, prefab overrides, layers, colliders, serialized references, and active/enabled state.
4. Form a cause hypothesis that predicts an observable result. Use logs, targeted inspection, or an existing verifier to test it before broad changes.

Do not add a null check, retry, `Find` call, or exception suppression merely to hide the symptom. First determine why the reference/state is absent and whether absence is valid.

## Fix the Cause Minimally

- Preserve existing subsystem ownership and event flow.
- Change the smallest code or serialized surface that resolves the confirmed cause.
- Avoid rerunning Scene/Prefab builders unless the broken generated artifact is the confirmed target and overwrites are understood.
- Do not clean up unrelated code while debugging.
- Keep temporary diagnostics easy to remove; remove them before completion unless they provide lasting operational value.

## Reproduce, Regress, Record

1. Repeat the original reproduction and prove the symptom is gone.
2. Run the nearest Project_Dig verifier or manual checks for adjacent behavior. Separate compile, Edit Mode, Play Mode, and YAML/reference evidence.
3. Review the Git diff for unrelated Scene/Prefab/import churn.
4. If the fix changes a lasting constraint, known problem, architecture, or reusable verification method, update `docs/CODEX_PROJECT_STATE.md`.
5. A reusable failure mode, failed approach, or Project_Dig-specific debugging lesson is a Memory candidate; do not duplicate transient state into `AGENTS.md`.

Report root cause, evidence, exact fix, regression coverage, and remaining uncertainty. Do not claim runtime confirmation when only compilation or static serialization was checked.
