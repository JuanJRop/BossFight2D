using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    [CreateAssetMenu(fileName = "MoleBossCombatConfig", menuName = "BossFight2D/Combat/Mole Boss Config")]
    public sealed class MoleBossCombatConfig : ScriptableObject
    {
        [Header("Projectile")]
        [SerializeField] private AttackData projectileData;
        [SerializeField, Min(0f)] private float bulletDamage = 8f;
        [SerializeField, Min(0.1f)] private float bulletSpeed = 7f;
        [SerializeField, Min(0.1f)] private float bulletLifeTime = 8f;
        [SerializeField, Min(1)] private int projectilePoolPrewarm = 512;

        [Header("Presentation Prefabs")]
        [SerializeField] private GameObject rockVisualPrefab;
        [SerializeField] private GameObject rockImpactPrefab;
        [SerializeField] private GameObject dashChargeFxPrefab;

        [Header("Phase Two Presentation")]
        [SerializeField] private Color phaseTwoBossColor = new(0.78f, 0.16f, 0.08f, 1f);
        [SerializeField, Range(1f, 1.5f)] private float phaseTwoBossScale = 1.22f;

        [Header("AI Rhythm")]
        [SerializeField, Min(0f)] private float phaseOneMinDelay = 0.65f;
        [SerializeField, Min(0f)] private float phaseOneMaxDelay = 1.2f;
        [SerializeField, Min(0f)] private float phaseTwoMinDelay = 0.32f;
        [SerializeField, Min(0f)] private float phaseTwoMaxDelay = 0.68f;
        [SerializeField, Range(0f, 1f)] private float phaseOneBurrowChance = 0.45f;
        [SerializeField, Range(0f, 1f)] private float phaseTwoBurrowChance = 0.78f;
        [SerializeField, Min(0.1f)] private float burrowHideTime = 0.55f;
        [SerializeField, Min(0f)] private float phaseOneRecovery = 0.45f;
        [SerializeField, Min(0f)] private float phaseTwoRecovery = 0.18f;
        [SerializeField, Min(0.1f)] private float phaseTransitionTime = 1.25f;

        [Header("Aimed Fan")]
        [SerializeField, Min(1)] private int phaseOneFanVolleys = 4;
        [SerializeField, Min(1)] private int phaseTwoFanVolleys = 7;
        [SerializeField, Min(1)] private int phaseOneFanProjectiles = 5;
        [SerializeField, Min(1)] private int phaseTwoFanProjectiles = 9;
        [SerializeField, Min(0f)] private float phaseOneFanSpread = 16f;
        [SerializeField, Min(0f)] private float phaseTwoFanSpread = 11f;

        [Header("Radial and Spiral")]
        [SerializeField, Min(1)] private int phaseOneRadialRings = 3;
        [SerializeField, Min(1)] private int phaseTwoRadialRings = 5;
        [SerializeField, Min(1)] private int phaseOneRadialCount = 18;
        [SerializeField, Min(1)] private int phaseTwoRadialCount = 30;
        [SerializeField, Min(1)] private int phaseOneSpiralSteps = 30;
        [SerializeField, Min(1)] private int phaseTwoSpiralSteps = 52;
        [SerializeField, Min(1)] private int phaseOneSpiralArms = 4;
        [SerializeField, Min(1)] private int phaseTwoSpiralArms = 6;

        [Header("Rock Rain")]
        [SerializeField, Min(1)] private int phaseOneRockCount = 10;
        [SerializeField, Min(1)] private int phaseTwoRockCount = 22;
        [SerializeField, Min(0.1f)] private float rockWarningTime = 1f;
        [SerializeField, Min(0.1f)] private float rockRadius = 1.7f;
        [SerializeField, Min(0f)] private float rockDamage = 18f;

        [Header("Twin Mole Laser")]
        [SerializeField, Min(0.1f)] private float twinLaserWarningTime = 1.25f;
        [SerializeField, Min(0.1f)] private float phaseOneTwinLaserDuration = 4.8f;
        [SerializeField, Min(0.1f)] private float phaseTwoTwinLaserDuration = 7f;
        [SerializeField, Min(0.1f)] private float twinLaserMoveSpeed = 4.4f;
        [SerializeField, Min(0.05f)] private float twinLaserRadius = 0.62f;
        [SerializeField, Min(0f)] private float twinLaserDamage = 16f;
        [SerializeField, Min(0.1f)] private float twinLaserDamageCooldown = 0.7f;
        [SerializeField, Min(0.05f)] private float twinLaserStunDuration = 0.24f;
        [SerializeField, Range(0.2f, 0.9f)] private float miniMoleScale = 0.5f;
        [SerializeField, Min(0f)] private float twinLaserTilt = 1.35f;

        [Header("Minion Horde")]
        [SerializeField, Min(1)] private int phaseOneHordeCount = 6;
        [SerializeField, Min(1)] private int phaseTwoHordeCount = 9;
        [SerializeField, Min(1f)] private float phaseOneMinionHealth = 28f;
        [SerializeField, Min(1f)] private float phaseTwoMinionHealth = 42f;
        [SerializeField, Min(0.1f)] private float phaseOneMinionSpeed = 3.4f;
        [SerializeField, Min(0.1f)] private float phaseTwoMinionSpeed = 4.5f;
        [SerializeField, Min(1f)] private float hordeMaxDuration = 14f;
        [SerializeField, Min(0f)] private float hordeContactDamage = 10f;
        [SerializeField, Min(0.1f)] private float hordeContactCooldown = 0.75f;
        [SerializeField, Min(0)] private int minionGoldReward = 3;
        [SerializeField, Min(0)] private int minionExperienceReward = 8;

        [Header("Combat Audio")]
        [SerializeField] private AudioClip minionSpawnSfx;
        [SerializeField] private AudioClip laserChargeSfx;
        [SerializeField] private AudioClip laserFireSfx;
        [SerializeField] private AudioClip rockImpactSfx;

        [Header("Charge Dash")]
        [SerializeField, Min(0.1f)] private float dashChargeTime = 0.85f;
        [SerializeField, Min(0.1f)] private float dashSpeed = 15f;
        [SerializeField, Min(0.1f)] private float dashMaxDistance = 10f;
        [SerializeField, Min(0.1f)] private float dashHitRadius = 1.2f;
        [SerializeField, Min(0f)] private float dashDamage = 22f;
        [SerializeField, Min(0f)] private float dashPushForce = 13f;

        public AttackData ProjectileData => projectileData;
        public float BulletDamage => bulletDamage;
        public float BulletSpeed => bulletSpeed;
        public float BulletLifeTime => bulletLifeTime;
        public int ProjectilePoolPrewarm => projectilePoolPrewarm;
        public GameObject RockVisualPrefab => rockVisualPrefab;
        public GameObject RockImpactPrefab => rockImpactPrefab;
        public GameObject DashChargeFxPrefab => dashChargeFxPrefab;
        public Color PhaseTwoBossColor => phaseTwoBossColor;
        public float PhaseTwoBossScale => phaseTwoBossScale;
        public float BurrowHideTime => burrowHideTime;
        public float PhaseTransitionTime => phaseTransitionTime;
        public float RockWarningTime => rockWarningTime;
        public float RockRadius => rockRadius;
        public float RockDamage => rockDamage;
        public float TwinLaserWarningTime => twinLaserWarningTime;
        public float TwinLaserRadius => twinLaserRadius;
        public float TwinLaserDamage => twinLaserDamage;
        public float TwinLaserDamageCooldown => twinLaserDamageCooldown;
        public float TwinLaserStunDuration => twinLaserStunDuration;
        public float MiniMoleScale => miniMoleScale;
        public float TwinLaserTilt => twinLaserTilt;
        public float HordeMaxDuration => hordeMaxDuration;
        public float HordeContactDamage => hordeContactDamage;
        public float HordeContactCooldown => hordeContactCooldown;
        public int MinionGoldReward => minionGoldReward;
        public int MinionExperienceReward => minionExperienceReward;
        public AudioClip MinionSpawnSfx => minionSpawnSfx;
        public AudioClip LaserChargeSfx => laserChargeSfx;
        public AudioClip LaserFireSfx => laserFireSfx;
        public AudioClip RockImpactSfx => rockImpactSfx;
        public float DashChargeTime => dashChargeTime;
        public float DashSpeed => dashSpeed;
        public float DashMaxDistance => dashMaxDistance;
        public float DashHitRadius => dashHitRadius;
        public float DashDamage => dashDamage;
        public float DashPushForce => dashPushForce;

        public float MinDelay(int phase) => phase == 2 ? phaseTwoMinDelay : phaseOneMinDelay;
        public float MaxDelay(int phase) => phase == 2 ? phaseTwoMaxDelay : phaseOneMaxDelay;
        public float BurrowChance(int phase) => phase == 2 ? phaseTwoBurrowChance : phaseOneBurrowChance;
        public float Recovery(int phase) => phase == 2 ? phaseTwoRecovery : phaseOneRecovery;
        public int FanVolleys(int phase) => phase == 2 ? phaseTwoFanVolleys : phaseOneFanVolleys;
        public int FanProjectiles(int phase) => phase == 2 ? phaseTwoFanProjectiles : phaseOneFanProjectiles;
        public float FanSpread(int phase) => phase == 2 ? phaseTwoFanSpread : phaseOneFanSpread;
        public int RadialRings(int phase) => phase == 2 ? phaseTwoRadialRings : phaseOneRadialRings;
        public int RadialCount(int phase) => phase == 2 ? phaseTwoRadialCount : phaseOneRadialCount;
        public int SpiralSteps(int phase) => phase == 2 ? phaseTwoSpiralSteps : phaseOneSpiralSteps;
        public int SpiralArms(int phase) => phase == 2 ? phaseTwoSpiralArms : phaseOneSpiralArms;
        public int RockCount(int phase) => phase == 2 ? phaseTwoRockCount : phaseOneRockCount;
        public float TwinLaserDuration(int phase) => phase == 2 ? phaseTwoTwinLaserDuration : phaseOneTwinLaserDuration;
        public float TwinLaserMoveSpeed(int phase) => twinLaserMoveSpeed * (phase == 2 ? 1.32f : 1f);
        public int HordeCount(int phase) => phase == 2 ? phaseTwoHordeCount : phaseOneHordeCount;
        public float MinionHealth(int phase) => phase == 2 ? phaseTwoMinionHealth : phaseOneMinionHealth;
        public float MinionSpeed(int phase) => phase == 2 ? phaseTwoMinionSpeed : phaseOneMinionSpeed;

        private void OnValidate()
        {
            phaseOneMaxDelay = Mathf.Max(phaseOneMinDelay, phaseOneMaxDelay);
            phaseTwoMaxDelay = Mathf.Max(phaseTwoMinDelay, phaseTwoMaxDelay);
            projectilePoolPrewarm = Mathf.Max(1, projectilePoolPrewarm);
            phaseTwoBossScale = Mathf.Clamp(phaseTwoBossScale, 1f, 1.5f);
        }
    }
}
