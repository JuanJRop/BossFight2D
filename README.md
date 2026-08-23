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
2. Complete radial rings and a rotating spiral bullet hell.
3. Telegraph circles followed by large falling rocks.
4. A charged dash with a visible trajectory, contact damage, and knockback.

Every boss projectile originates at the boss fire point. Volleys reserve their complete projectile batch before firing, so a pattern is either emitted in full or cancelled with a diagnostic instead of degrading into partial counts.

The AI uses a readable state cycle: burrow, emerge, telegraph, attack, and recover. Phase 1 introduces attacks one by one. At 40% health, phase 2 begins with a transition shockwave, shorter recovery windows, denser patterns, and more frequent burrowing.

## Runtime structure

- `Characters/Player`: movement, dodge, shooting, sound, and cosmetic presentation.
- `Characters/Enemy`: boss movement and an encapsulated combat domain.
- `Scripts/Boss`: phase orchestration and phase checkpoints.
- `Scripts/ArenaBounds`: rectangular physical walls, visible boundary, and shared logical limits.
- `Scripts/ArenaHazardDirector`: environmental lasers independent from the enemy AI.
- `Scripts/Controller`: game flow and UI coordination.
- `ObjectPool`: prewarmed projectile storage with atomic batch reservations.

The mole combat follows a composition-based architecture:

- `EnemyAttackController` only owns the AI loop and state transitions.
- `IMoleBossAttack` isolates every pattern in its own class under `MoleBoss/Attacks`.
- `MoleBossAttackSelector` owns introductions, weighting, distance bias, and repetition rules.
- `MoleBossCombatContext` exposes only the operations attacks are allowed to use.
- player targeting, projectile pooling, and temporary telegraphs are separate services.
- `MoleBossCombatConfig` is the single ScriptableObject for encounter tuning; attacks contain no mutable balance data.

Adding a boss pattern now means implementing `IMoleBossAttack` and registering it, without modifying the execution flow or the existing attacks. Arena hazards are registered separately and never enter the boss attack selector.

The `BossFight` scene owns an independent `Arena Systems` object. It creates four physical walls from the visible camera rectangle, clamps the player and boss inside, provides bounds to rocks, dashes and pickups, and runs parallel full-map laser cycles. Warning lines and active beams terminate exactly at the arena boundary.

Combat presentation is prefab-driven while attack decisions remain in code. The shared enemy projectile uses an animated ember/fireball from the installed pixel-art pack, rock rain instantiates a rotating mine-rock prefab plus an animated impact, and the charge dash uses a looping red energy effect. These references live in `MoleBossCombatConfig`, so presentation can be replaced without editing attack logic.

The three planned playable characters are cosmetic only and use the same gameplay prefab and statistics. `CharacterSkinData` stores visual differences without duplicating combat code.

## Project

- Unity: `6000.3.9f1`
- Render pipeline: URP 2D
- Main scenes: `Menu` and `BossFight`

Open the project in the matching Unity editor version. `BossPhaseController` and the mole-boss AI are wired into the enemy prefab. `PhaseCheckpoint` and `CharacterSkinSelector` still need to be connected in the relevant scene objects before testing those flows.
