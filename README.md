# BossFight2D

BossFight2D is a top-down Unity boss-rush prototype for keyboard and mouse. The first vertical slice focuses on one insect/robot boss, a fixed automatic rifle, readable attack patterns, and a two-phase encounter.

## Encounter design

- Phase 1 runs from 100% to 40% boss health and teaches attacks separately.
- Phase 2 runs from 40% to 0% and combines the learned patterns.
- Death restarts the current phase.
- The player keeps the checkpoint's ammunition, stamina, and power, but always restarts with at least 50% health.
- The target fight duration is 5-8 minutes.

Planned boss patterns:

1. A projectile corridor with a readable safe path.
2. A charged dash with a visible trajectory.
3. A hidden state followed by telegraphed falling rocks.
4. A basic chase-and-shoot attack between special patterns.

## Runtime structure

- `Characters/Player`: movement, dodge, shooting, sound, and cosmetic presentation.
- `Characters/Enemy`: boss movement and attack executors.
- `Scripts/Boss`: phase orchestration and phase checkpoints.
- `Scripts/Controller`: game flow and UI coordination.
- `ObjectPool`: shared lifetime management for projectiles and encounter hazards.

The three planned playable characters are cosmetic only and use the same gameplay prefab and statistics. `CharacterSkinData` stores visual differences without duplicating combat code.

## Project

- Unity: `6000.3.9f1`
- Render pipeline: URP 2D
- Main scenes: `Menu` and `BossFight`

Open the project in the matching Unity editor version. After compilation, wire `BossPhaseController`, `PhaseCheckpoint`, and `CharacterSkinSelector` in the relevant prefabs or scene objects before testing the new flow.
