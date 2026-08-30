using System.Collections;
using Project.Characters.Player.PlayerScripts.Core;
using UnityEngine;

namespace Project.Scripts.Controller
{
    public class ExplosionAutoDestroy : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 1f;

        private ObjectPool pool;
        private GameObject prefab;
        private GameObject pooledInstance;

        private void OnEnable()
        {
            StartCoroutine(ReleaseRoutine());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        public void Configure(ObjectPool sourcePool, GameObject sourcePrefab, GameObject sourceInstance)
        {
            pool = sourcePool;
            prefab = sourcePrefab;
            pooledInstance = sourceInstance;

            StopAllCoroutines();
            StartCoroutine(ReleaseRoutine());
        }

        private IEnumerator ReleaseRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, lifeTime));

            if (pool != null && prefab != null)
            {
                pool.ReturnObject(pooledInstance != null ? pooledInstance : gameObject, prefab);
                yield break;
            }

            Destroy(gameObject);
        }
    }
}
