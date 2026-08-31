using Project.Characters.Player.PlayerScripts.Core;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class LifeTimer : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 3f;

        private ObjectPool pool;
        private GameObject sourcePrefab;
        private float timer;
        private bool configured;

        private void OnEnable()
        {
            timer = Mathf.Max(0.1f, lifeTime);
        }

        public void Configure(ObjectPool sourcePool, GameObject prefab, float duration)
        {
            pool = sourcePool;
            sourcePrefab = prefab;
            lifeTime = Mathf.Max(0.1f, duration);
            timer = lifeTime;
            configured = pool != null && sourcePrefab != null;
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                Deactivate();
            }
        }

        private void Deactivate()
        {
            AttackEntity entity = GetComponent<AttackEntity>();
            if (configured && entity != null)
            {
                entity.ReturnToPool();
                return;
            }

            if (configured)
            {
                pool.ReturnObject(gameObject, sourcePrefab);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
