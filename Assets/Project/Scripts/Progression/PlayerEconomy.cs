using System;
using UnityEngine;

namespace Project.Scripts.Progression
{
    public static class PlayerEconomy
    {
        private const string GoldKey = "progression.gold";
        private const string ExperienceKey = "progression.experience";
        private const string LevelKey = "progression.level";

        public static event Action<int> OnGoldChanged;
        public static event Action<int, int, int> OnExperienceChanged;

        public static int Gold => Mathf.Max(0, PlayerPrefs.GetInt(GoldKey, 0));
        public static int Experience => Mathf.Max(0, PlayerPrefs.GetInt(ExperienceKey, 0));
        public static int Level => Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1));
        public static int ExperienceForNextLevel => RequiredExperience(Level);

        public static void AddGold(int amount)
        {
            if (amount <= 0) return;
            int value = Gold + amount;
            PlayerPrefs.SetInt(GoldKey, value);
            PlayerPrefs.Save();
            OnGoldChanged?.Invoke(value);
        }

        public static bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            int value = Gold - amount;
            PlayerPrefs.SetInt(GoldKey, value);
            PlayerPrefs.Save();
            OnGoldChanged?.Invoke(value);
            return true;
        }

        public static void AddExperience(int amount)
        {
            if (amount <= 0) return;
            int experience = Experience + amount;
            int level = Level;
            while (experience >= RequiredExperience(level))
            {
                experience -= RequiredExperience(level);
                level++;
            }

            PlayerPrefs.SetInt(ExperienceKey, experience);
            PlayerPrefs.SetInt(LevelKey, level);
            PlayerPrefs.Save();
            OnExperienceChanged?.Invoke(experience, RequiredExperience(level), level);
        }

        public static int RequiredExperience(int level)
        {
            return 60 + Mathf.Max(1, level) * 40;
        }
    }
}
