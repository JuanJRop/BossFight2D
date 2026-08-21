# BossFight2D

BossFight2D is a top-down Unity boss-rush prototype for keyboard and mouse. The first vertical slice focuses on one mechanical mole boss, a fixed automatic rifle, readable attack patterns, and a two-phase encounter.

## Encounter design

- Phase 1 runs from 100% to 40% boss health and teaches attacks separately.
- Phase 2 runs from 40% to 0% and combines the learned patterns.
- Death restarts the current phase.
- The player keeps the checkpoint's ammunition, stamina, and power, but always restarts with at least 50% health.
- The target fight duration is 5-8 minutes.

Implemented mole-boss patterns:

1. An aimed fan that teaches projectile reading.
2. Radial rings and a rotating spiral bullet hell.
3. A projectile corridor with a readable moving safe path.
4. Telegraph circles followed by falling rocks and impact shards.
5. A charged dash with a visible trajectory, contact damage, and knockback.

The AI uses a readable state cycle: burrow, emerge, telegraph, attack, and recover. Phase 1 introduces attacks one by one. At 40% health, phase 2 begins with a transition shockwave, shorter recovery windows, denser patterns, and more frequent burrowing.

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

Open the project in the matching Unity editor version. `BossPhaseController` and the mole-boss AI are wired into the enemy prefab. `PhaseCheckpoint` and `CharacterSkinSelector` still need to be connected in the relevant scene objects before testing those flows.
