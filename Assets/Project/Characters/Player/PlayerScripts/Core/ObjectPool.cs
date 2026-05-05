using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Core
{
    public class ObjectPool : MonoBehaviour
    {
        private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new();

        public GameObject GetObject(GameObject prefab)
        {
            if (!poolDictionary.ContainsKey(prefab))
            {
                poolDictionary[prefab] = new Queue<GameObject>();
            }

            Queue<GameObject> pool = poolDictionary[prefab];

            if (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                obj.SetActive(true);
                return obj;
            }
            else
            {
                return Instantiate(prefab);
            }
        }

        public void ReturnObject(GameObject obj, GameObject prefab)
        { 
            obj.SetActive(false);

            if (!poolDictionary.ContainsKey(prefab))
            {
                poolDictionary[prefab] = new Queue<GameObject>();
            }

            poolDictionary[prefab].Enqueue(obj);
        }
    }
}