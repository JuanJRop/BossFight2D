using System;
using Project.Characters.Enemy.EnemyScripts.Core;

namespace Project.Scripts.Progression
{
    public static class RunSession
    {
        private static Health trackedPlayerHealth;
        private static bool runStarted;

        public static event Action<int> OnPlayerDeathsChanged;

        public static bool IsRunActive => runStarted;
        public static int PlayerDeaths { get; private set; }
        public static bool BossCheckpointReached { get; private set; }

        public static void BeginNewRun()
        {
            UnregisterTrackedPlayer();
            runStarted = true;
            PlayerDeaths = 0;
            BossCheckpointReached = false;
            OnPlayerDeathsChanged?.Invoke(PlayerDeaths);
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

            if (ReferenceEquals(trackedPlayerHealth, playerHealth)) return;
            UnregisterTrackedPlayer();

            trackedPlayerHealth = playerHealth;
            trackedPlayerHealth.OnDied += HandlePlayerDied;
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
    }
}
