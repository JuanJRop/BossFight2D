using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Core
{
    public class ObjectPool : MonoBehaviour
    {
        private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();
        private readonly Dictionary<GameObject, GameObject> activeObjects = new();
        private readonly HashSet<GameObject> queuedObjects = new();

        public GameObject GetObject(GameObject prefab)
        {
            return GetObject(prefab, Vector3.zero, Quaternion.identity);
        }

        public GameObject GetObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogError("ObjectPool cannot spawn a null prefab.", this);
                return null;
            }

            if (!pools.TryGetValue(prefab, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                pools[prefab] = pool;
            }

            GameObject instance = null;
            while (pool.Count > 0 && instance == null)
            {
                instance = pool.Dequeue();
            }

            if (instance == null)
            {
                instance = Instantiate(prefab, transform);
            }

            queuedObjects.Remove(instance);
            activeObjects[instance] = prefab;
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
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

            if (!pools.TryGetValue(prefab, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                pools[prefab] = pool;
            }

            pool.Enqueue(instance);
        }

        public void ReturnAll()
        {
            var snapshot = new List<KeyValuePair<GameObject, GameObject>>(activeObjects);
            foreach (KeyValuePair<GameObject, GameObject> entry in snapshot)
            {
                if (entry.Key != null)
                {
                    ReturnObject(entry.Key, entry.Value);
                }
            }
        }
    }
}
