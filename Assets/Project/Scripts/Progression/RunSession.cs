using System;
using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Movement;
using UnityEngine;

namespace Project.Scripts.Progression
{
    public enum PlayerStatType
    {
        Speed,
        Strength,
        Cadence,
        Dexterity,
        Stamina
    }

    public static class RunSession
    {
        private const int BaseExperienceToNextLevel = 100;
        private const int ExperienceGrowthPerLevel = 75;
        private const float SpeedBonusPerPoint = 0.08f;
        private const float StrengthBonusPerPoint = 0.12f;
        private const float CadenceBonusPerPoint = 0.1f;
        private const float DexterityProjectileBonusPerPoint = 0.06f;
        private const float DexterityDashBonusPerPoint = 0.12f;
        private const float StaminaHealthBonusPerPoint = 0.1f;

        private static readonly PlayerStatType[] StatPool =
        {
            PlayerStatType.Speed,
            PlayerStatType.Strength,
            PlayerStatType.Cadence,
            PlayerStatType.Dexterity,
            PlayerStatType.Stamina
        };

        private static readonly List<PlayerStatType> currentChoices = new();
        private static Health trackedPlayerHealth;
        private static bool runStarted;
        private static int pendingLevelUps;
        private static int rerollsRemaining;

        public static event Action<int> OnPlayerDeathsChanged;
        public static event Action OnProgressionChanged;
        public static event Action OnLevelUpChoicesChanged;

        public static bool IsRunActive => runStarted;
        public static int PlayerDeaths { get; private set; }
        public static bool BossCheckpointReached { get; private set; }
        public static int Experience { get; private set; }
        public static int Level { get; private set; } = 1;
        public static int Strength { get; private set; }
        public static int Speed { get; private set; }
        public static int Cadence { get; private set; }
        public static int Dexterity { get; private set; }
        public static int Stamina { get; private set; }
        public static int ExperienceToNextLevel => RequiredExperience(Level);
        public static bool HasPendingLevelUp => pendingLevelUps > 0 && currentChoices.Count > 0;
        public static int RerollsRemaining => rerollsRemaining;
        public static IReadOnlyList<PlayerStatType> CurrentChoices => currentChoices;

        public static float MoveSpeedMultiplier => 1f + Speed * SpeedBonusPerPoint;
        public static float DamageMultiplier => 1f + Strength * StrengthBonusPerPoint;
        public static float AttackCooldownMultiplier =>
            1f / (1f + Cadence * CadenceBonusPerPoint);
        public static float ProjectileSpeedMultiplier =>
            1f + Dexterity * DexterityProjectileBonusPerPoint;
        public static float DashRechargeMultiplier =>
            1f / (1f + Dexterity * DexterityDashBonusPerPoint);
        public static float DashForceMultiplier =>
            1f + Dexterity * 0.04f;
        public static float PlayerHealthMultiplier =>
            1f + Stamina * StaminaHealthBonusPerPoint;
        public static int StaminaDashChargeBonus => Stamina;

        public static void BeginNewRun()
        {
            UnregisterTrackedPlayer();
            runStarted = true;
            PlayerDeaths = 0;
            BossCheckpointReached = false;
            Experience = 0;
            Level = 1;
            Strength = 0;
            Speed = 0;
            Cadence = 0;
            Dexterity = 0;
            Stamina = 0;
            pendingLevelUps = 0;
            rerollsRemaining = 0;
            currentChoices.Clear();
            OnPlayerDeathsChanged?.Invoke(PlayerDeaths);
            OnProgressionChanged?.Invoke();
            OnLevelUpChoicesChanged?.Invoke();
        }

        public static void EnsureRunStarted()
        {
            if (!runStarted) BeginNewRun();
        }

        public static void MarkBossCheckpoint()
        {
            EnsureRunStarted();
            BossCheckpointReached = true;
        }

        public static void RegisterPlayer(Health playerHealth)
        {
            if (playerHealth == null) return;
            EnsureRunStarted();

            if (ReferenceEquals(trackedPlayerHealth, playerHealth))
            {
                RunProgressionUI.Ensure();
                return;
            }

            UnregisterTrackedPlayer();

            trackedPlayerHealth = playerHealth;
            trackedPlayerHealth.OnDied += HandlePlayerDied;
            RunProgressionUI.Ensure();
        }

        public static void UnregisterPlayer(Health playerHealth)
        {
            if (!ReferenceEquals(trackedPlayerHealth, playerHealth)) return;
            UnregisterTrackedPlayer();
        }

        private static void HandlePlayerDied()
        {
            if (!runStarted) return;
            PlayerDeaths++;
            OnPlayerDeathsChanged?.Invoke(PlayerDeaths);
        }

        private static void UnregisterTrackedPlayer()
        {
            if (trackedPlayerHealth != null) trackedPlayerHealth.OnDied -= HandlePlayerDied;
            trackedPlayerHealth = null;
        }

        public static void AwardExperience(int amount)
        {
            if (amount <= 0) return;
            EnsureRunStarted();

            Experience += amount;
            while (Experience >= RequiredExperience(Level))
            {
                Experience -= RequiredExperience(Level);
                Level++;
                pendingLevelUps++;
            }

            if (pendingLevelUps > 0 && currentChoices.Count == 0)
                PrepareLevelUpChoices(3);

            OnProgressionChanged?.Invoke();
            OnLevelUpChoicesChanged?.Invoke();
        }

        public static bool SelectUpgrade(PlayerStatType stat)
        {
            if (!HasPendingLevelUp || !currentChoices.Contains(stat)) return false;

            switch (stat)
            {
                case PlayerStatType.Speed:
                    Speed++;
                    break;
                case PlayerStatType.Strength:
                    Strength++;
                    break;
                case PlayerStatType.Cadence:
                    Cadence++;
                    break;
                case PlayerStatType.Dexterity:
                    Dexterity++;
                    break;
                case PlayerStatType.Stamina:
                    Stamina++;
                    trackedPlayerHealth?.AddMaxHealthPercent(StaminaHealthBonusPerPoint);
                    break;
            }

            pendingLevelUps--;
            currentChoices.Clear();
            rerollsRemaining = 0;
            RefreshPlayerStats();

            if (pendingLevelUps > 0)
                PrepareLevelUpChoices(3);

            OnProgressionChanged?.Invoke();
            OnLevelUpChoicesChanged?.Invoke();
            return true;
        }

        public static bool RerollLevelUpChoices()
        {
            if (!HasPendingLevelUp || rerollsRemaining <= 0) return false;
            rerollsRemaining--;
            PrepareLevelUpChoices(2, false);
            OnProgressionChanged?.Invoke();
            OnLevelUpChoicesChanged?.Invoke();
            return true;
        }

        public static int RequiredExperience(int level)
        {
            return BaseExperienceToNextLevel + Mathf.Max(0, level - 1) * ExperienceGrowthPerLevel;
        }

        public static int GetStatValue(PlayerStatType stat)
        {
            return stat switch
            {
                PlayerStatType.Speed => Speed,
                PlayerStatType.Strength => Strength,
                PlayerStatType.Cadence => Cadence,
                PlayerStatType.Dexterity => Dexterity,
                PlayerStatType.Stamina => Stamina,
                _ => 0
            };
        }

        public static string GetStatName(PlayerStatType stat, bool spanish)
        {
            return stat switch
            {
                PlayerStatType.Speed => spanish ? "VELOCIDAD" : "SPEED",
                PlayerStatType.Strength => spanish ? "FUERZA" : "STRENGTH",
                PlayerStatType.Cadence => spanish ? "CADENCIA" : "CADENCE",
                PlayerStatType.Dexterity => spanish ? "DESTREZA" : "DEXTERITY",
                PlayerStatType.Stamina => spanish ? "STAMINA" : "STAMINA",
                _ => string.Empty
            };
        }

        public static string GetStatDescription(PlayerStatType stat, bool spanish)
        {
            return stat switch
            {
                PlayerStatType.Speed => spanish ? "+8% movimiento" : "+8% movement",
                PlayerStatType.Strength => spanish ? "+12% dano" : "+12% damage",
                PlayerStatType.Cadence => spanish ? "+10% frecuencia" : "+10% fire rate",
                PlayerStatType.Dexterity => spanish ? "+6% proyectil, dash agil" : "+6% projectile, faster dash",
                PlayerStatType.Stamina => spanish ? "+10% vida, +1 dash" : "+10% health, +1 dash",
                _ => string.Empty
            };
        }

        private static void PrepareLevelUpChoices(int count, bool resetRerolls = true)
        {
            currentChoices.Clear();
            int targetCount = Mathf.Clamp(count, 1, StatPool.Length);
            int guard = 0;
            while (currentChoices.Count < targetCount && guard++ < 100)
            {
                PlayerStatType candidate = StatPool[UnityEngine.Random.Range(0, StatPool.Length)];
                if (!currentChoices.Contains(candidate)) currentChoices.Add(candidate);
            }

            if (resetRerolls) rerollsRemaining = 1;
        }

        private static void RefreshPlayerStats()
        {
            if (trackedPlayerHealth == null) return;
            PlayerMove playerMove = trackedPlayerHealth.GetComponent<PlayerMove>();
            if (playerMove == null) playerMove = trackedPlayerHealth.GetComponentInParent<PlayerMove>();
            if (playerMove == null) playerMove = trackedPlayerHealth.GetComponentInChildren<PlayerMove>(true);
            playerMove?.RefreshProgressionStats();

            AttackPlayer attack = trackedPlayerHealth.GetComponent<AttackPlayer>();
            if (attack == null) attack = trackedPlayerHealth.GetComponentInParent<AttackPlayer>();
            if (attack == null) attack = trackedPlayerHealth.GetComponentInChildren<AttackPlayer>(true);
            attack?.RefreshProgressionStats();

            PlayerDodge dodge = trackedPlayerHealth.GetComponent<PlayerDodge>();
            if (dodge == null) dodge = trackedPlayerHealth.GetComponentInParent<PlayerDodge>();
            if (dodge == null) dodge = trackedPlayerHealth.GetComponentInChildren<PlayerDodge>(true);
            dodge?.RefreshProgressionStats();
        }
    }
}
