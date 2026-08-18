---
name: born-to-dig-unity
description: Implement or change gameplay, Unity assets, scenes, prefabs, input, or UI in BORN TO DIG / Project_Dig while preserving its existing mining, FPS, treasure, and destruction integrations. Use for Project_Dig feature work; do not use for documentation-only or pure investigation tasks.
---

# BORN TO DIG Unity Development

Use this workflow for feature or integration work in `Project_Dig`.

## Establish Current Context

1. Read repository-root `AGENTS.md` completely.
2. Read `docs/CODEX_PROJECT_STATE.md` and check its review date, current Scene composition, known problems, and verification entries.
3. Record the current branch, `git status --short`, and relevant existing diffs. Preserve all unrelated user changes.
4. Inspect only the requested system's scripts, serialized Scene/Prefab references, and direct dependencies. Treat Project State as a map, not proof that files are unchanged.

Distinguish confirmed code/serialization from inferred design. If the requested behavior depends on an unresolved product choice, report the choice instead of silently making it permanent.

## Implement a Minimal Compatible Change

- Keep the existing ownership boundaries: Pickaxe for visuals, MiningTool for hit dispatch, VoxelRock/VoxelGrid/MarchingCubes for voxel state and mesh, DestructiblePebble for fracture, and Gold/Manager/UI for pickup and clear.
- Extend an existing event or narrow API when appropriate; do not duplicate the mining, exposure, pickup, input, or clear flow.
- Match existing namespaces, serialized-field style, New Input System access, and Runtime/Editor separation.
- Before changing a Scene or Prefab, identify its source prefab, `.meta` GUIDs, layers, colliders, and references. Do not run a builder/installer merely to save manual wiring.
- Preserve the single FPS Player, Main Camera, AudioListener, crosshair, MiningTool, and bootstrap invariants.
- Do not modify source models, materials, textures, import settings, or ProjectSettings unless the request requires them.

## Verify and Hand Off

Use `.agents/skills/born-to-dig-validation/SKILL.md` to choose checks proportional to the change. At minimum, inspect compile/Console status when Unity code changed, serialized references when assets changed, the nearby gameplay loop, and final Git diff.

Before finishing, reread `docs/CODEX_PROJECT_STATE.md`. Update it only when architecture, a major Scene/Prefab role, dependency, important decision, known problem, or verification method changed. Keep recent changes short and current.

Report implemented files, verified behavior, unverified items and why, and any pre-existing working-tree changes left untouched. Do not commit or push unless explicitly requested.
