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

    public enum RunAbilityType
    {
        BouncingOrb,
        AutoBullets,
        ChainLaser,
        VoidNova,
        Overclock
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

        private static readonly RunAbilityType[] AbilityPool =
        {
            RunAbilityType.BouncingOrb,
            RunAbilityType.AutoBullets,
            RunAbilityType.ChainLaser,
            RunAbilityType.VoidNova,
            RunAbilityType.Overclock
        };

        private static readonly List<RunAbilityType> currentAbilityChoices = new();
        private static readonly int[] abilityRanks = new int[5];
        private static Health trackedPlayerHealth;
        private static bool runStarted;
        private static int pendingAbilityRewards;

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
        public static int AvailableStatPoints { get; private set; }
        public static int AllocatedStatPoints => Strength + Speed + Cadence + Dexterity + Stamina;
        public static int AbilityLevelInterval => 5;
        public static int MaximumAbilityRank => 5;
        public static int ExperienceToNextLevel => RequiredExperience(Level);
        public static bool HasPendingAbilityChoice =>
            pendingAbilityRewards > 0 && currentAbilityChoices.Count > 0;
        public static bool HasPendingLevelUp => HasPendingAbilityChoice;
        public static int PendingAbilityRewards => pendingAbilityRewards;
        public static IReadOnlyList<RunAbilityType> CurrentAbilityChoices => currentAbilityChoices;

        public static float MoveSpeedMultiplier =>
            1f + Speed * SpeedBonusPerPoint + GetAbilityRank(RunAbilityType.Overclock) * 0.08f;
        public static float DamageMultiplier =>
            1f + Strength * StrengthBonusPerPoint + GetAbilityRank(RunAbilityType.Overclock) * 0.1f;
        public static float AttackCooldownMultiplier =>
            1f / (1f + Cadence * CadenceBonusPerPoint +
                GetAbilityRank(RunAbilityType.Overclock) * 0.12f);
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
            AvailableStatPoints = 0;
            pendingAbilityRewards = 0;
            currentAbilityChoices.Clear();
            for (int index = 0; index < abilityRanks.Length; index++) abilityRanks[index] = 0;
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
                RunAbilityController.Ensure(playerHealth);
                RunProgressionUI.Ensure();
                return;
            }

            UnregisterTrackedPlayer();

            trackedPlayerHealth = playerHealth;
            trackedPlayerHealth.OnDied += HandlePlayerDied;
            RunAbilityController.Ensure(playerHealth);
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
                AvailableStatPoints++;
                if (Level % AbilityLevelInterval == 0) pendingAbilityRewards++;
            }

            if (pendingAbilityRewards > 0 && currentAbilityChoices.Count == 0)
                PrepareAbilityChoices();

            OnProgressionChanged?.Invoke();
            OnLevelUpChoicesChanged?.Invoke();
        }

        public static bool SelectAbility(RunAbilityType ability)
        {
            if (!HasPendingAbilityChoice || !currentAbilityChoices.Contains(ability)) return false;

            int abilityIndex = (int)ability;
            if (abilityIndex < 0 || abilityIndex >= abilityRanks.Length) return false;
            abilityRanks[abilityIndex] = Mathf.Min(MaximumAbilityRank, abilityRanks[abilityIndex] + 1);
            pendingAbilityRewards = Mathf.Max(0, pendingAbilityRewards - 1);
            currentAbilityChoices.Clear();

            if (pendingAbilityRewards > 0) PrepareAbilityChoices();
            RefreshPlayerStats();
            OnProgressionChanged?.Invoke();
            OnLevelUpChoicesChanged?.Invoke();
            return true;
        }

        public static bool SpendStatPoint(PlayerStatType stat)
        {
            if (AvailableStatPoints <= 0) return false;

            ChangeStat(stat, 1);
            AvailableStatPoints--;
            RefreshPlayerStats();
            OnProgressionChanged?.Invoke();
            return true;
        }

        public static bool RefundStatPoint(PlayerStatType stat)
        {
            if (GetStatValue(stat) <= 0) return false;

            ChangeStat(stat, -1);
            AvailableStatPoints++;
            RefreshPlayerStats();
            OnProgressionChanged?.Invoke();
            return true;
        }

        public static void GrantAbility(RunAbilityType ability, int ranks = 1)
        {
            EnsureRunStarted();
            int abilityIndex = (int)ability;
            if (abilityIndex < 0 || abilityIndex >= abilityRanks.Length || ranks <= 0) return;

            abilityRanks[abilityIndex] = Mathf.Min(MaximumAbilityRank, abilityRanks[abilityIndex] + ranks);
            RefreshPlayerStats();
            OnProgressionChanged?.Invoke();
        }

        public static void GrantPuzzleChestReward()
        {
            EnsureRunStarted();
            abilityRanks[(int)RunAbilityType.BouncingOrb] = Mathf.Min(MaximumAbilityRank,
                abilityRanks[(int)RunAbilityType.BouncingOrb] + 1);
            abilityRanks[(int)RunAbilityType.ChainLaser] = Mathf.Min(MaximumAbilityRank,
                abilityRanks[(int)RunAbilityType.ChainLaser] + 1);
            trackedPlayerHealth?.Heal(trackedPlayerHealth.MaxHealth * 0.35f);
            RefreshPlayerStats();
            OnProgressionChanged?.Invoke();
        }

        public static bool HasAbility(RunAbilityType ability)
        {
            return GetAbilityRank(ability) > 0;
        }

        public static int GetAbilityRank(RunAbilityType ability)
        {
            int index = (int)ability;
            return index >= 0 && index < abilityRanks.Length ? abilityRanks[index] : 0;
        }

        public static string GetAbilityName(RunAbilityType ability, bool spanish)
        {
            return ability switch
            {
                RunAbilityType.BouncingOrb => spanish ? "ORBE REBOTADOR" : "BOUNCING ORB",
                RunAbilityType.AutoBullets => spanish ? "BALAS AUTOMATICAS" : "AUTO BULLETS",
                RunAbilityType.ChainLaser => spanish ? "RAYO ENCADENADO" : "CHAIN LASER",
                RunAbilityType.VoidNova => spanish ? "NOVA DEL VACIO" : "VOID NOVA",
                RunAbilityType.Overclock => spanish ? "SOBRECARGA" : "OVERCLOCK",
                _ => string.Empty
            };
        }

        public static string GetAbilityDescription(RunAbilityType ability, bool spanish)
        {
            return ability switch
            {
                RunAbilityType.BouncingOrb => spanish
                    ? "Una esfera rebota por la sala y golpea enemigos"
                    : "A sphere ricochets around the room and hits enemies",
                RunAbilityType.AutoBullets => spanish
                    ? "Dispara automaticamente al enemigo mas cercano"
                    : "Automatically fires at the nearest enemy",
                RunAbilityType.ChainLaser => spanish
                    ? "Un rayo salta entre varios enemigos"
                    : "A laser jumps between multiple enemies",
                RunAbilityType.VoidNova => spanish
                    ? "Explosiones periodicas alrededor del jugador"
                    : "Periodic explosions around the player",
                RunAbilityType.Overclock => spanish
                    ? "+8% velocidad, +10% dano y +12% cadencia por rango"
                    : "+8% speed, +10% damage and +12% fire rate per rank",
                _ => string.Empty
            };
        }

        public static string GetAbilitySummary(bool spanish)
        {
            List<string> equipped = new();
            foreach (RunAbilityType ability in AbilityPool)
            {
                int rank = GetAbilityRank(ability);
                if (rank <= 0) continue;
                equipped.Add($"{GetAbilityName(ability, spanish)} {rank}");
            }

            if (equipped.Count == 0) return spanish ? "HABILIDADES: NINGUNA" : "ABILITIES: NONE";
            return (spanish ? "HABILIDADES: " : "ABILITIES: ") + string.Join("  |  ", equipped);
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

        private static void ChangeStat(PlayerStatType stat, int amount)
        {
            switch (stat)
            {
                case PlayerStatType.Speed:
                    Speed = Mathf.Max(0, Speed + amount);
                    break;
                case PlayerStatType.Strength:
                    Strength = Mathf.Max(0, Strength + amount);
                    break;
                case PlayerStatType.Cadence:
                    Cadence = Mathf.Max(0, Cadence + amount);
                    break;
                case PlayerStatType.Dexterity:
                    Dexterity = Mathf.Max(0, Dexterity + amount);
                    break;
                case PlayerStatType.Stamina:
                    Stamina = Mathf.Max(0, Stamina + amount);
                    break;
            }
        }

        private static void PrepareAbilityChoices()
        {
            currentAbilityChoices.Clear();
            foreach (RunAbilityType ability in AbilityPool) currentAbilityChoices.Add(ability);
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
