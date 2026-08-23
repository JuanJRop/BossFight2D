using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Core
{
    public class ObjectPool : MonoBehaviour
    {
        [Header("Capacity")]
        [SerializeField, Min(1)] private int maxInstancesPerPrefab = 768;

        private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();
        private readonly Dictionary<GameObject, GameObject> activeObjects = new();
        private readonly Dictionary<GameObject, int> totalInstances = new();
        private readonly HashSet<GameObject> queuedObjects = new();

        public GameObject GetObject(GameObject prefab)
        {
            return GetObject(prefab, Vector3.zero, Quaternion.identity);
        }

        public GameObject GetObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!EnsureAvailable(prefab, 1)) return null;
            return ActivateNext(prefab, position, rotation);
        }

        public bool GetObjects(GameObject prefab, int count, List<GameObject> results)
        {
            if (results == null)
            {
                Debug.LogError("ObjectPool batch results collection cannot be null.", this);
                return false;
            }

            results.Clear();
            if (count <= 0) return true;
            if (!EnsureAvailable(prefab, count)) return false;

            for (int i = 0; i < count; i++)
            {
                GameObject instance = ActivateNext(prefab, Vector3.zero, Quaternion.identity);
                if (instance == null)
                {
                    ReturnBatch(results, prefab);
                    results.Clear();
                    Debug.LogError($"ObjectPool failed to reserve the complete batch of {count} objects.", this);
                    return false;
                }

                results.Add(instance);
            }

            return true;
        }

        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            int target = Mathf.Min(count, Mathf.Max(1, maxInstancesPerPrefab));
            EnsurePool(prefab);
            Sanitize(prefab);

            int total = totalInstances.TryGetValue(prefab, out int knownTotal) ? knownTotal : 0;
            while (total < target)
            {
                GameObject instance = CreateInstance(prefab);
                if (instance == null) break;
                pools[prefab].Enqueue(instance);
                queuedObjects.Add(instance);
                total++;
            }
        }

        public void ReturnObject(GameObject instance, GameObject prefab)
        {
            if (instance == null) return;
            if (prefab == null)
            {
                Destroy(instance);
                return;
            }

            if (!queuedObjects.Add(instance)) return;

            activeObjects.Remove(instance);
            instance.SetActive(false);
            instance.transform.SetParent(transform);
            EnsurePool(prefab);
            pools[prefab].Enqueue(instance);
        }

        public void ReturnAll()
        {
            var snapshot = new List<KeyValuePair<GameObject, GameObject>>(activeObjects);
            foreach (KeyValuePair<GameObject, GameObject> entry in snapshot)
            {
                if (entry.Key != null) ReturnObject(entry.Key, entry.Value);
            }
        }

        private bool EnsureAvailable(GameObject prefab, int required)
        {
            if (prefab == null)
            {
                Debug.LogError("ObjectPool cannot spawn a null prefab.", this);
                return false;
            }

            EnsurePool(prefab);
            Sanitize(prefab);
            Queue<GameObject> pool = pools[prefab];
            int total = totalInstances.TryGetValue(prefab, out int knownTotal) ? knownTotal : 0;
            int maximum = Mathf.Max(1, maxInstancesPerPrefab);

            while (pool.Count < required && total < maximum)
            {
                GameObject instance = CreateInstance(prefab);
                if (instance == null) break;
                pool.Enqueue(instance);
                queuedObjects.Add(instance);
                total++;
            }

            if (pool.Count >= required) return true;

            Debug.LogError(
                $"ObjectPool cannot reserve {required} complete objects for {prefab.name}. " +
                $"Available: {pool.Count}, active: {CountActive(prefab)}, maximum: {maximum}.",
                this);
            return false;
        }

        private GameObject ActivateNext(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            Queue<GameObject> pool = pools[prefab];
            GameObject instance = null;
            while (pool.Count > 0 && instance == null) instance = pool.Dequeue();
            if (instance == null) return null;

            queuedObjects.Remove(instance);
            activeObjects[instance] = prefab;
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            GameObject instance = Instantiate(prefab, transform);
            if (instance == null) return null;
            instance.SetActive(false);
            totalInstances[prefab] = (totalInstances.TryGetValue(prefab, out int total) ? total : 0) + 1;
            return instance;
        }

        private void EnsurePool(GameObject prefab)
        {
            if (!pools.ContainsKey(prefab)) pools[prefab] = new Queue<GameObject>();
            if (!totalInstances.ContainsKey(prefab)) totalInstances[prefab] = 0;
        }

        private void Sanitize(GameObject prefab)
        {
            Queue<GameObject> pool = pools[prefab];
            int count = pool.Count;
            for (int i = 0; i < count; i++)
            {
                GameObject instance = pool.Dequeue();
                if (instance != null)
                {
                    pool.Enqueue(instance);
                    continue;
                }

                totalInstances[prefab] = Mathf.Max(0, totalInstances[prefab] - 1);
            }
        }

        private int CountActive(GameObject prefab)
        {
            int count = 0;
            foreach (GameObject activePrefab in activeObjects.Values)
            {
                if (activePrefab == prefab) count++;
            }
            return count;
        }

        private void ReturnBatch(IEnumerable<GameObject> instances, GameObject prefab)
        {
            foreach (GameObject instance in instances)
            {
                if (instance != null) ReturnObject(instance, prefab);
            }
        }

        private void OnValidate()
        {
            maxInstancesPerPrefab = Mathf.Max(1, maxInstancesPerPrefab);
        }
    }
}
