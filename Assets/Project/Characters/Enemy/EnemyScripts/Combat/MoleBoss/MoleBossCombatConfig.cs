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

        [Header("AI Rhythm")]
        [SerializeField, Min(0f)] private float phaseOneMinDelay = 0.8f;
        [SerializeField, Min(0f)] private float phaseOneMaxDelay = 1.5f;
        [SerializeField, Min(0f)] private float phaseTwoMinDelay = 0.58f;
        [SerializeField, Min(0f)] private float phaseTwoMaxDelay = 1.08f;
        [SerializeField, Range(0f, 1f)] private float phaseOneBurrowChance = 0.45f;
        [SerializeField, Range(0f, 1f)] private float phaseTwoBurrowChance = 0.7f;
        [SerializeField, Min(0.1f)] private float burrowHideTime = 0.55f;
        [SerializeField, Min(0f)] private float phaseOneRecovery = 0.55f;
        [SerializeField, Min(0f)] private float phaseTwoRecovery = 0.35f;
        [SerializeField, Min(0.1f)] private float phaseTransitionTime = 1.25f;

        [Header("Aimed Fan")]
        [SerializeField, Min(1)] private int phaseOneFanVolleys = 3;
        [SerializeField, Min(1)] private int phaseTwoFanVolleys = 5;
        [SerializeField, Min(1)] private int phaseOneFanProjectiles = 3;
        [SerializeField, Min(1)] private int phaseTwoFanProjectiles = 5;
        [SerializeField, Min(0f)] private float phaseOneFanSpread = 16f;
        [SerializeField, Min(0f)] private float phaseTwoFanSpread = 13f;

        [Header("Radial and Spiral")]
        [SerializeField, Min(1)] private int phaseOneRadialRings = 2;
        [SerializeField, Min(1)] private int phaseTwoRadialRings = 3;
        [SerializeField, Min(1)] private int phaseOneRadialCount = 14;
        [SerializeField, Min(1)] private int phaseTwoRadialCount = 20;
        [SerializeField, Min(1)] private int phaseOneSpiralSteps = 24;
        [SerializeField, Min(1)] private int phaseTwoSpiralSteps = 34;
        [SerializeField, Min(1)] private int phaseOneSpiralArms = 3;
        [SerializeField, Min(1)] private int phaseTwoSpiralArms = 4;

        [Header("Corridor")]
        [SerializeField, Min(0.1f)] private float phaseOneCorridorWidth = 2.8f;
        [SerializeField, Min(0.1f)] private float phaseTwoCorridorWidth = 2.3f;
        [SerializeField, Min(1)] private int phaseOneCorridorWaves = 7;
        [SerializeField, Min(1)] private int phaseTwoCorridorWaves = 10;
        [SerializeField, Min(0.1f)] private float phaseOneCorridorSpacing = 0.86f;
        [SerializeField, Min(0.1f)] private float phaseTwoCorridorSpacing = 0.72f;

        [Header("Rock Rain")]
        [SerializeField, Min(1)] private int phaseOneRockCount = 4;
        [SerializeField, Min(1)] private int phaseTwoRockCount = 7;
        [SerializeField, Min(0.1f)] private float rockWarningTime = 1.15f;
        [SerializeField, Min(0.1f)] private float rockRadius = 1.15f;
        [SerializeField, Min(0f)] private float rockDamage = 18f;

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
        public float BurrowHideTime => burrowHideTime;
        public float PhaseTransitionTime => phaseTransitionTime;
        public float RockWarningTime => rockWarningTime;
        public float RockRadius => rockRadius;
        public float RockDamage => rockDamage;
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
        public float CorridorWidth(int phase) => phase == 2 ? phaseTwoCorridorWidth : phaseOneCorridorWidth;
        public int CorridorWaves(int phase) => phase == 2 ? phaseTwoCorridorWaves : phaseOneCorridorWaves;
        public float CorridorSpacing(int phase) => phase == 2 ? phaseTwoCorridorSpacing : phaseOneCorridorSpacing;
        public int RockCount(int phase) => phase == 2 ? phaseTwoRockCount : phaseOneRockCount;

        private void OnValidate()
        {
            phaseOneMaxDelay = Mathf.Max(phaseOneMinDelay, phaseOneMaxDelay);
            phaseTwoMaxDelay = Mathf.Max(phaseTwoMinDelay, phaseTwoMaxDelay);
        }
    }
}
