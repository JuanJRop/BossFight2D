using System;
using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Movement;
using UnityEngine;

namespace Project.Scripts.Progression
{
    // Kept as a compatibility surface for older scripts. Progression now uses skill nodes.
    public enum PlayerStatType
    {
        Speed,
        Strength,
        Cadence,
        Dexterity,
        Stamina
    }

    public enum RunClassType
    {
        None,
        Warrior,
        Archer,
        Mage,
        Healer
    }

    public enum RunSkillType
    {
        WarriorCore,
        BladeWave,
        Whirlwind,
        IronGuard,
        ArcherCore,
        QuickDraw,
        PiercingArrows,
        ArrowRain,
        MageCore,
        FireBullets,
        Firestorm,
        ArcaneBeam,
        HealerCore,
        RadiantBolts,
        HealingAura,
        Sanctuary
    }

    // Legacy names are translated to the new tree for old scene or prefab references.
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
        private const int SkillPointsPerLevel = 1;
        private const int PuzzleChestSkillPoints = 2;

        private static readonly RunSkillType[] WarriorSkills =
        {
            RunSkillType.WarriorCore,
            RunSkillType.BladeWave,
            RunSkillType.Whirlwind,
            RunSkillType.IronGuard
        };

        private static readonly RunSkillType[] ArcherSkills =
        {
            RunSkillType.ArcherCore,
            RunSkillType.QuickDraw,
            RunSkillType.PiercingArrows,
            RunSkillType.ArrowRain
        };

        private static readonly RunSkillType[] MageSkills =
        {
            RunSkillType.MageCore,
            RunSkillType.FireBullets,
            RunSkillType.Firestorm,
            RunSkillType.ArcaneBeam
        };

        private static readonly RunSkillType[] HealerSkills =
        {
            RunSkillType.HealerCore,
            RunSkillType.RadiantBolts,
            RunSkillType.HealingAura,
            RunSkillType.Sanctuary
        };

        private static readonly int[] skillRanks = new int[16];
        private static Health trackedPlayerHealth;
        private static bool runStarted;

        public static event Action<int> OnPlayerDeathsChanged;
        public static event Action OnProgressionChanged;
        public static event Action OnLevelUpChoicesChanged;

        public static bool IsRunActive => runStarted;
        public static int PlayerDeaths { get; private set; }
        public static bool BossCheckpointReached { get; private set; }
        public static int Experience { get; private set; }
        public static int Level { get; private set; } = 1;
        public static RunClassType SelectedClass { get; private set; } = RunClassType.None;
        public static int AvailableSkillPoints { get; private set; }
        public static int SkillPoints => AvailableSkillPoints;
        public static int AllocatedSkillPoints
        {
            get
            {
                int total = 0;
                for (int index = 0; index < skillRanks.Length; index++) total += skillRanks[index];
                return total;
            }
        }

        // Compatibility aliases for the previous status panel.
        public static int AvailableStatPoints => AvailableSkillPoints;
        public static int AllocatedStatPoints => AllocatedSkillPoints;
        public static int AbilityLevelInterval => 5;
        public static int MaximumAbilityRank => 3;
        public static int ExperienceToNextLevel => RequiredExperience(Level);
        public static bool HasPendingAbilityChoice => false;
        public static bool HasPendingLevelUp => false;
        public static int PendingAbilityRewards => 0;
        public static IReadOnlyList<RunAbilityType> CurrentAbilityChoices => Array.Empty<RunAbilityType>();

        // These values are consumed by movement, health and combat at runtime.
        public static float MoveSpeedMultiplier =>
            1f + GetSkillRank(RunSkillType.ArcherCore) * 0.035f +
            GetSkillRank(RunSkillType.QuickDraw) * 0.065f +
            GetSkillRank(RunSkillType.Whirlwind) * 0.025f;

        public static float DamageMultiplier =>
            1f + GetSkillRank(RunSkillType.WarriorCore) * 0.045f +
            GetSkillRank(RunSkillType.ArcherCore) * 0.035f +
            GetSkillRank(RunSkillType.MageCore) * 0.04f +
            GetSkillRank(RunSkillType.HealerCore) * 0.025f +
            GetSkillRank(RunSkillType.Whirlwind) * 0.08f;

        public static float AttackCooldownMultiplier =>
            1f / (1f + GetSkillRank(RunSkillType.QuickDraw) * 0.12f +
                GetSkillRank(RunSkillType.FireBullets) * 0.045f +
                GetSkillRank(RunSkillType.RadiantBolts) * 0.035f);

        public static float ProjectileSpeedMultiplier =>
            1f + GetSkillRank(RunSkillType.QuickDraw) * 0.08f +
            GetSkillRank(RunSkillType.ArcaneBeam) * 0.045f;

        public static float DashRechargeMultiplier =>
            1f / (1f + GetSkillRank(RunSkillType.QuickDraw) * 0.08f +
                GetSkillRank(RunSkillType.Whirlwind) * 0.04f);

        public static float DashForceMultiplier =>
            1f + GetSkillRank(RunSkillType.Whirlwind) * 0.1f +
            GetSkillRank(RunSkillType.IronGuard) * 0.035f;

        public static float PlayerHealthMultiplier =>
            1f + GetSkillRank(RunSkillType.IronGuard) * 0.15f +
            GetSkillRank(RunSkillType.HealingAura) * 0.07f +
            GetSkillRank(RunSkillType.Sanctuary) * 0.1f;

        public static float PlayerDamageTakenMultiplier => Mathf.Clamp01(
            1f - GetSkillRank(RunSkillType.IronGuard) * 0.055f -
            GetSkillRank(RunSkillType.Sanctuary) * 0.085f -
            GetSkillRank(RunSkillType.HealingAura) * 0.025f);

        public static int StaminaDashChargeBonus => GetSkillRank(RunSkillType.IronGuard);

        public static int BasicProjectileCount
        {
            get
            {
                int count = 1;
                if (GetSkillRank(RunSkillType.ArcherCore) > 0 && GetSkillRank(RunSkillType.QuickDraw) >= 2)
                    count++;
                if (GetSkillRank(RunSkillType.ArrowRain) >= 3) count++;
                return Mathf.Clamp(count, 1, 3);
            }
        }

        public static float BasicProjectileSpread => BasicProjectileCount <= 1
            ? 0f
            : 8f + GetSkillRank(RunSkillType.ArrowRain) * 2f;

        public static float BasicProjectileDamageMultiplier =>
            1f + GetSkillRank(RunSkillType.BladeWave) * 0.1f +
            GetSkillRank(RunSkillType.PiercingArrows) * 0.12f +
            GetSkillRank(RunSkillType.FireBullets) * 0.2f +
            GetSkillRank(RunSkillType.RadiantBolts) * 0.16f;

        public static float BasicProjectileVisualScale =>
            1f + GetSkillRank(RunSkillType.BladeWave) * 0.06f +
            GetSkillRank(RunSkillType.PiercingArrows) * 0.07f +
            GetSkillRank(RunSkillType.FireBullets) * 0.12f +
            GetSkillRank(RunSkillType.RadiantBolts) * 0.1f;

        public static bool BasicProjectileHoming => GetSkillRank(RunSkillType.ArcaneBeam) >= 3;

        public static Color BasicProjectileColor
        {
            get
            {
                if (GetSkillRank(RunSkillType.FireBullets) > 0)
                    return new Color(1f, 0.38f, 0.08f, 1f);
                if (GetSkillRank(RunSkillType.RadiantBolts) > 0)
                    return new Color(1f, 0.86f, 0.28f, 1f);
                if (GetSkillRank(RunSkillType.PiercingArrows) > 0)
                    return new Color(0.35f, 1f, 0.48f, 1f);
                if (GetSkillRank(RunSkillType.BladeWave) > 0)
                    return new Color(1f, 0.27f, 0.08f, 1f);

                switch (GetCombatClass())
                {
                    case RunClassType.Warrior:
                        return new Color(1f, 0.27f, 0.08f, 1f);
                    case RunClassType.Archer:
                        return new Color(0.35f, 1f, 0.48f, 1f);
                    case RunClassType.Mage:
                        return new Color(1f, 0.38f, 0.08f, 1f);
                    case RunClassType.Healer:
                        return new Color(1f, 0.86f, 0.28f, 1f);
                }

                return new Color(0.24f, 0.92f, 1f, 1f);
            }
        }

        public static int PowerProjectileCount => Mathf.Clamp(
            3 + (GetSkillRank(RunSkillType.ArcherCore) > 0 ? 1 : 0) +
            (GetSkillRank(RunSkillType.FireBullets) >= 3 ? 1 : 0), 3, 5);

        public static float PowerProjectileSpread =>
            18f + GetSkillRank(RunSkillType.ArcherCore) * 7f +
            GetSkillRank(RunSkillType.FireBullets) * 4f;

        public static float PowerProjectileDamageMultiplier =>
            1.35f + GetSkillRank(RunSkillType.BladeWave) * 0.14f +
            GetSkillRank(RunSkillType.PiercingArrows) * 0.16f +
            GetSkillRank(RunSkillType.FireBullets) * 0.24f +
            GetSkillRank(RunSkillType.RadiantBolts) * 0.2f;

        public static float PowerProjectileVisualScale =>
            1.22f + GetSkillRank(RunSkillType.BladeWave) * 0.08f +
            GetSkillRank(RunSkillType.FireBullets) * 0.12f +
            GetSkillRank(RunSkillType.RadiantBolts) * 0.1f;

        public static bool PowerProjectileHoming =>
            GetSkillRank(RunSkillType.ArrowRain) >= 2 || GetSkillRank(RunSkillType.ArcaneBeam) >= 2;

        public static Color PowerProjectileColor => Color.Lerp(BasicProjectileColor, Color.white, 0.2f);

        public static void BeginNewRun()
        {
            UnregisterTrackedPlayer();
            runStarted = true;
            PlayerDeaths = 0;
            BossCheckpointReached = false;
            Experience = 0;
            Level = 1;
            SelectedClass = RunClassType.None;
            AvailableSkillPoints = 0;
            for (int index = 0; index < skillRanks.Length; index++) skillRanks[index] = 0;

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
            RefreshPlayerStats();
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
                AvailableSkillPoints += SkillPointsPerLevel;
            }

            RefreshPlayerStats();
            OnProgressionChanged?.Invoke();
            OnLevelUpChoicesChanged?.Invoke();
        }

        public static bool SelectClass(RunClassType classType)
        {
            if (classType == RunClassType.None) return false;
            SelectedClass = classType;
            RefreshPlayerStats();
            OnProgressionChanged?.Invoke();
            return true;
        }

        public static bool PurchaseSkill(RunSkillType skill)
        {
            EnsureRunStarted();
            if (!CanPurchaseSkill(skill)) return false;

            skillRanks[(int)skill]++;
            AvailableSkillPoints--;
            RunClassType skillClass = GetSkillClass(skill);
            if (IsCoreSkill(skill)) SelectedClass = skillClass;
            RefreshPlayerStats();
            OnProgressionChanged?.Invoke();
            return true;
        }

        public static bool RefundSkill(RunSkillType skill)
        {
            int index = (int)skill;
            if (index < 0 || index >= skillRanks.Length || skillRanks[index] <= 0) return false;
            if (IsCoreSkill(skill) && HasChildRanks(skill)) return false;

            skillRanks[index]--;
            AvailableSkillPoints++;
            if (IsCoreSkill(skill) && SelectedClass == GetSkillClass(skill))
                SelectedClass = FindHighestRankedClass();
            RefreshPlayerStats();
            OnProgressionChanged?.Invoke();
            return true;
        }

        public static bool CanPurchaseSkill(RunSkillType skill)
        {
            int index = (int)skill;
            if (index < 0 || index >= skillRanks.Length || AvailableSkillPoints <= 0) return false;
            if (skillRanks[index] >= GetSkillMaxRank(skill)) return false;
            return IsCoreSkill(skill) || GetSkillRank(GetSkillRoot(skill)) > 0;
        }

        public static int GetSkillRank(RunSkillType skill)
        {
            int index = (int)skill;
            return index >= 0 && index < skillRanks.Length ? skillRanks[index] : 0;
        }

        public static int GetSkillMaxRank(RunSkillType skill)
        {
            return IsCoreSkill(skill) ? 1 : 3;
        }

        public static bool IsSkillUnlocked(RunSkillType skill)
        {
            return IsCoreSkill(skill) || GetSkillRank(GetSkillRoot(skill)) > 0;
        }

        public static RunClassType GetSkillClass(RunSkillType skill)
        {
            if (skill >= RunSkillType.WarriorCore && skill <= RunSkillType.IronGuard)
                return RunClassType.Warrior;
            if (skill >= RunSkillType.ArcherCore && skill <= RunSkillType.ArrowRain)
                return RunClassType.Archer;
            if (skill >= RunSkillType.MageCore && skill <= RunSkillType.ArcaneBeam)
                return RunClassType.Mage;
            return RunClassType.Healer;
        }

        public static IReadOnlyList<RunSkillType> GetClassSkills(RunClassType classType)
        {
            switch (classType)
            {
                case RunClassType.Warrior: return WarriorSkills;
                case RunClassType.Archer: return ArcherSkills;
                case RunClassType.Mage: return MageSkills;
                case RunClassType.Healer: return HealerSkills;
                default: return Array.Empty<RunSkillType>();
            }
        }

        public static string GetClassName(RunClassType classType, bool spanish)
        {
            switch (classType)
            {
                case RunClassType.Warrior: return spanish ? "GUERRERO" : "WARRIOR";
                case RunClassType.Archer: return spanish ? "ARQUERO" : "ARCHER";
                case RunClassType.Mage: return spanish ? "MAGO" : "MAGE";
                case RunClassType.Healer: return "HEALER";
                default: return spanish ? "SIN CLASE" : "NO CLASS";
            }
        }

        public static string GetClassDescription(RunClassType classType, bool spanish)
        {
            switch (classType)
            {
                case RunClassType.Warrior:
                    return spanish ? "Resiste y domina el espacio cercano." : "Endure yourself and own close range.";
                case RunClassType.Archer:
                    return spanish ? "Movilidad, precision y lluvia de flechas." : "Mobility, precision and arrow rain.";
                case RunClassType.Mage:
                    return spanish ? "Convierte tus proyectiles en fuego y rayos." : "Turn your projectiles into fire and beams.";
                case RunClassType.Healer:
                    return spanish ? "Protege al cazador y recupera vida durante el combate." : "Protect the hunter and recover health in combat.";
                default:
                    return spanish ? "Elige una rama para definir tu estilo." : "Choose a branch to define your style.";
            }
        }

        public static Color GetClassColor(RunClassType classType)
        {
            switch (classType)
            {
                case RunClassType.Warrior: return new Color(1f, 0.3f, 0.12f, 1f);
                case RunClassType.Archer: return new Color(0.32f, 1f, 0.48f, 1f);
                case RunClassType.Mage: return new Color(1f, 0.42f, 0.08f, 1f);
                case RunClassType.Healer: return new Color(1f, 0.86f, 0.28f, 1f);
                default: return new Color(0.24f, 0.92f, 1f, 1f);
            }
        }

        public static string GetSkillName(RunSkillType skill, bool spanish)
        {
            switch (skill)
            {
                case RunSkillType.WarriorCore: return spanish ? "NUCLEO GUERRERO" : "WARRIOR CORE";
                case RunSkillType.BladeWave: return spanish ? "OLA DE ESPADA" : "BLADE WAVE";
                case RunSkillType.Whirlwind: return spanish ? "GOLPE TORNADO" : "WHIRLWIND";
                case RunSkillType.IronGuard: return spanish ? "GUARDIA DE HIERRO" : "IRON GUARD";
                case RunSkillType.ArcherCore: return spanish ? "NUCLEO ARQUERO" : "ARCHER CORE";
                case RunSkillType.QuickDraw: return spanish ? "DISPARO RAPIDO" : "QUICK DRAW";
                case RunSkillType.PiercingArrows: return spanish ? "FLECHAS PERFORANTES" : "PIERCING ARROWS";
                case RunSkillType.ArrowRain: return spanish ? "LLUVIA DE FLECHAS" : "ARROW RAIN";
                case RunSkillType.MageCore: return spanish ? "NUCLEO MAGO" : "MAGE CORE";
                case RunSkillType.FireBullets: return spanish ? "BALAS DE FUEGO" : "FIRE BULLETS";
                case RunSkillType.Firestorm: return spanish ? "TORMENTA DE FUEGO" : "FIRESTORM";
                case RunSkillType.ArcaneBeam: return spanish ? "RAYO ARCANO" : "ARCANE BEAM";
                case RunSkillType.HealerCore: return spanish ? "NUCLEO HEALER" : "HEALER CORE";
                case RunSkillType.RadiantBolts: return spanish ? "RAYOS RADIANTES" : "RADIANT BOLTS";
                case RunSkillType.HealingAura: return spanish ? "AURA SANADORA" : "HEALING AURA";
                case RunSkillType.Sanctuary: return spanish ? "SANTUARIO" : "SANCTUARY";
                default: return string.Empty;
            }
        }

        public static string GetSkillDescription(RunSkillType skill, bool spanish)
        {
            switch (skill)
            {
                case RunSkillType.WarriorCore: return spanish ? "Desbloquea la rama de combate pesado." : "Unlocks the heavy combat branch.";
                case RunSkillType.BladeWave: return spanish ? "+10% dano de proyectil por rango y ondas de espada." : "+10% projectile damage per rank and blade waves.";
                case RunSkillType.Whirlwind: return spanish ? "Golpes circulares automaticos y mas fuerza de dash." : "Automatic circular strikes and stronger dash.";
                case RunSkillType.IronGuard: return spanish ? "+15% vida y resistencia por rango." : "+15% health and resistance per rank.";
                case RunSkillType.ArcherCore: return spanish ? "Desbloquea velocidad y la rama de arco." : "Unlocks speed and the bow branch.";
                case RunSkillType.QuickDraw: return spanish ? "Cadencia y movilidad; rango 2 abre doble flecha." : "Fire rate and mobility; rank 2 opens double arrows.";
                case RunSkillType.PiercingArrows: return spanish ? "Flechas mas grandes y dano perforante." : "Larger arrows and piercing damage.";
                case RunSkillType.ArrowRain: return spanish ? "Disparos automaticos que persiguen al enemigo." : "Automatic shots that hunt enemies.";
                case RunSkillType.MageCore: return spanish ? "Desbloquea magia elemental y la rama de fuego." : "Unlocks elemental magic and the fire branch.";
                case RunSkillType.FireBullets: return spanish ? "Tus balas se vuelven fuego: dano y tamano." : "Your bullets become fire: damage and size.";
                case RunSkillType.Firestorm: return spanish ? "Una explosion de fuego periodica alrededor del jugador." : "A periodic fire explosion around the player.";
                case RunSkillType.ArcaneBeam: return spanish ? "Rayo encadenado y punteria arcana." : "Chaining beam and arcane targeting.";
                case RunSkillType.HealerCore: return spanish ? "Desbloquea luz, curacion y proteccion." : "Unlocks light, healing and protection.";
                case RunSkillType.RadiantBolts: return spanish ? "Proyectiles dorados que hacen mas dano." : "Golden projectiles that deal more damage.";
                case RunSkillType.HealingAura: return spanish ? "Regenera vida y mejora la vida maxima." : "Regenerates health and improves max health.";
                case RunSkillType.Sanctuary: return spanish ? "Reduce el dano recibido y crea una zona segura." : "Reduces damage taken and creates a safe zone.";
                default: return string.Empty;
            }
        }

        public static string GetSkillSummary(bool spanish)
        {
            List<string> selected = new();
            for (int index = 0; index < skillRanks.Length; index++)
            {
                if (skillRanks[index] <= 0) continue;
                RunSkillType skill = (RunSkillType)index;
                selected.Add(GetSkillName(skill, spanish) + " " + skillRanks[index]);
            }

            if (selected.Count == 0) return spanish ? "RAMA: SIN HABILIDADES" : "BRANCH: NO SKILLS";
            return (spanish ? "HABILIDADES: " : "SKILLS: ") + string.Join("  |  ", selected);
        }

        public static RunClassType GetCombatClass()
        {
            if (SelectedClass != RunClassType.None) return SelectedClass;
            return FindHighestRankedClass();
        }

        public static int RequiredExperience(int level)
        {
            return BaseExperienceToNextLevel + Mathf.Max(0, level - 1) * ExperienceGrowthPerLevel;
        }

        public static void GrantPuzzleChestReward()
        {
            EnsureRunStarted();
            AvailableSkillPoints += PuzzleChestSkillPoints;
            trackedPlayerHealth?.Heal(trackedPlayerHealth.MaxHealth * 0.35f);
            RefreshPlayerStats();
            OnProgressionChanged?.Invoke();
        }

        // Legacy wrappers map old pickups to meaningful nodes in the new tree.
        public static bool SelectAbility(RunAbilityType ability)
        {
            return PurchaseSkill(MapLegacyAbility(ability));
        }

        public static void GrantAbility(RunAbilityType ability, int ranks = 1)
        {
            EnsureRunStarted();
            RunSkillType skill = MapLegacyAbility(ability);
            RunSkillType root = GetSkillRoot(skill);
            if (!IsCoreSkill(skill) && GetSkillRank(root) == 0) skillRanks[(int)root] = 1;
            skillRanks[(int)skill] = Mathf.Clamp(skillRanks[(int)skill] + Mathf.Max(0, ranks), 0,
                GetSkillMaxRank(skill));
            SelectedClass = GetSkillClass(skill);
            RefreshPlayerStats();
            OnProgressionChanged?.Invoke();
        }

        public static bool HasAbility(RunAbilityType ability)
        {
            return GetAbilityRank(ability) > 0;
        }

        public static int GetAbilityRank(RunAbilityType ability)
        {
            return GetSkillRank(MapLegacyAbility(ability));
        }

        public static string GetAbilityName(RunAbilityType ability, bool spanish)
        {
            return GetSkillName(MapLegacyAbility(ability), spanish);
        }

        public static string GetAbilityDescription(RunAbilityType ability, bool spanish)
        {
            return GetSkillDescription(MapLegacyAbility(ability), spanish);
        }

        public static string GetAbilitySummary(bool spanish)
        {
            return GetSkillSummary(spanish);
        }

        // Older save/UI callers can still ask for stat values; skill points are the only spendable currency now.
        public static int GetStatValue(PlayerStatType stat) => 0;

        public static string GetStatName(PlayerStatType stat, bool spanish)
        {
            switch (stat)
            {
                case PlayerStatType.Speed: return spanish ? "VELOCIDAD" : "SPEED";
                case PlayerStatType.Strength: return spanish ? "FUERZA" : "STRENGTH";
                case PlayerStatType.Cadence: return spanish ? "CADENCIA" : "CADENCE";
                case PlayerStatType.Dexterity: return spanish ? "DESTREZA" : "DEXTERITY";
                case PlayerStatType.Stamina: return "STAMINA";
                default: return string.Empty;
            }
        }

        public static string GetStatDescription(PlayerStatType stat, bool spanish)
        {
            return spanish ? "Sustituido por el arbol de habilidades" : "Replaced by the skill tree";
        }

        public static bool SpendStatPoint(PlayerStatType stat)
        {
            return false;
        }

        public static bool RefundStatPoint(PlayerStatType stat)
        {
            return false;
        }

        public static void RefreshPlayerStats()
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

        private static bool IsCoreSkill(RunSkillType skill)
        {
            return skill == RunSkillType.WarriorCore || skill == RunSkillType.ArcherCore ||
                skill == RunSkillType.MageCore || skill == RunSkillType.HealerCore;
        }

        private static RunSkillType GetSkillRoot(RunSkillType skill)
        {
            switch (GetSkillClass(skill))
            {
                case RunClassType.Warrior: return RunSkillType.WarriorCore;
                case RunClassType.Archer: return RunSkillType.ArcherCore;
                case RunClassType.Mage: return RunSkillType.MageCore;
                default: return RunSkillType.HealerCore;
            }
        }

        private static bool HasChildRanks(RunSkillType root)
        {
            IReadOnlyList<RunSkillType> skills = GetClassSkills(GetSkillClass(root));
            for (int index = 1; index < skills.Count; index++)
            {
                if (GetSkillRank(skills[index]) > 0) return true;
            }

            return false;
        }

        private static RunClassType FindHighestRankedClass()
        {
            RunClassType bestClass = RunClassType.None;
            int bestRank = 0;
            RunClassType[] classes = { RunClassType.Warrior, RunClassType.Archer, RunClassType.Mage, RunClassType.Healer };
            foreach (RunClassType classType in classes)
            {
                IReadOnlyList<RunSkillType> skills = GetClassSkills(classType);
                int rank = 0;
                for (int index = 0; index < skills.Count; index++) rank += GetSkillRank(skills[index]);
                if (rank <= bestRank) continue;
                bestRank = rank;
                bestClass = classType;
            }

            return bestClass;
        }

        private static RunSkillType MapLegacyAbility(RunAbilityType ability)
        {
            switch (ability)
            {
                case RunAbilityType.BouncingOrb: return RunSkillType.ArcaneBeam;
                case RunAbilityType.AutoBullets: return RunSkillType.ArrowRain;
                case RunAbilityType.ChainLaser: return RunSkillType.ArcaneBeam;
                case RunAbilityType.VoidNova: return RunSkillType.Firestorm;
                default: return RunSkillType.QuickDraw;
            }
        }
    }
}
