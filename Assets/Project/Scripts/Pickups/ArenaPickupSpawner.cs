using System.Collections;
using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using UnityEngine;

namespace Project.Scripts.Pickups
{
    public sealed class ArenaPickupSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject healthKitPrefab;
        [SerializeField] private GameObject manaPotionPrefab;
        [SerializeField, Min(0)] private int initialHealthKits = 2;
        [SerializeField, Min(0)] private int initialManaPotions = 2;
        [SerializeField, Min(1)] private int maximumActivePickups = 5;
        [SerializeField, Min(1f)] private float respawnInterval = 20f;
        [SerializeField, Min(0f)] private float bossClearRadius = 2.8f;

        private readonly List<GameObject> activePickups = new();
        private Health bossHealth;
        private bool spawnHealthNext;

        private void Start()
        {
            bossHealth = GetComponent<Health>();
            if (bossHealth != null) bossHealth.OnDied += StopSpawning;

            for (int i = 0; i < initialHealthKits; i++) Spawn(healthKitPrefab);
            for (int i = 0; i < initialManaPotions; i++) Spawn(manaPotionPrefab);
            StartCoroutine(RespawnLoop());
        }

        private void OnDestroy()
        {
            if (bossHealth != null) bossHealth.OnDied -= StopSpawning;
        }

        private IEnumerator RespawnLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(respawnInterval);
                activePickups.RemoveAll(item => item == null);
                if (activePickups.Count >= maximumActivePickups) continue;
                Spawn(spawnHealthNext ? healthKitPrefab : manaPotionPrefab);
                spawnHealthNext = !spawnHealthNext;
            }
        }

        private void Spawn(GameObject prefab)
        {
            if (prefab == null) return;
            GameObject pickup = Instantiate(prefab, FindSpawnPosition(), Quaternion.identity);
            activePickups.Add(pickup);
        }

        private Vector2 FindSpawnPosition()
        {
            Camera camera = Camera.main;
            Vector2 minimum = new(-8f, -4.5f);
            Vector2 maximum = new(8f, 4.5f);
            if (camera != null && camera.orthographic)
            {
                float distance = Mathf.Abs(camera.transform.position.z);
                minimum = camera.ViewportToWorldPoint(new Vector3(0.1f, 0.12f, distance));
                maximum = camera.ViewportToWorldPoint(new Vector3(0.9f, 0.88f, distance));
            }

            Vector2 position = Vector2.zero;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                position = new Vector2(Random.Range(minimum.x, maximum.x), Random.Range(minimum.y, maximum.y));
                if (Vector2.Distance(position, transform.position) >= bossClearRadius) break;
            }
            return position;
        }

        private void StopSpawning()
        {
            StopAllCoroutines();
        }
    }
}
