using Project.Characters.Enemy.EnemyScripts.Core;
using UnityEngine;

namespace Project.Scripts.Pickups
{
    public sealed class ArenaPickupSpawner : MonoBehaviour
    {
        private Health bossHealth;

        private void Start()
        {
            bossHealth = GetComponent<Health>();
            if (bossHealth != null) bossHealth.OnDied += StopSpawning;

            // Recovery now comes from destructible cover, so the arena has no random drops.
            enabled = false;
        }

        private void OnDestroy()
        {
            if (bossHealth != null) bossHealth.OnDied -= StopSpawning;
        }

        private void StopSpawning()
        {
            enabled = false;
        }
    }
}
