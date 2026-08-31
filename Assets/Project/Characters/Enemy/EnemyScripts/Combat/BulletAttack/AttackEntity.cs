using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Core;
using Project.Characters.Player.PlayerScripts.Movement;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class AttackEntity : MonoBehaviour
    {
        private Rigidbody2D rb;
        private TrailRenderer trail;
        private GameObject prefab;
        private GameObject pooledInstance;
        private ObjectPool pool;
        private BulletOwner owner;
        private float damage;
        private bool hasReturned;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            trail = GetComponent<TrailRenderer>();
        }

        private void OnEnable()
        {
            hasReturned = false;
        }

        public void SetPool(ObjectPool sourcePool, GameObject sourcePrefab, GameObject sourceInstance, BulletOwner sourceOwner, float sourceDamage, float lifeTime)
        {
            pool = sourcePool;
            owner = sourceOwner;
            prefab = sourcePrefab;
            pooledInstance = sourceInstance;
            damage = Mathf.Max(0f, sourceDamage);
            if (rb != null)
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            LifeTimer timer = GetComponent<LifeTimer>();
            if (timer != null)
            {
                timer.Configure(sourcePool, sourcePrefab, lifeTime);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasReturned) return;

            if (IsWall(other))
            {
                ReturnToPool();
                return;
            }

            if (owner == BulletOwner.Player)
            {
                Health health = other.GetComponentInParent<Health>();
                if (health != null && !health.CompareTag("Player")) ApplyDamage(other);
                return;
            }

            if (owner != BulletOwner.Enemy || !other.CompareTag("Player")) return;

            PlayerDodge dodge = other.GetComponentInParent<PlayerDodge>();
            if (dodge != null && dodge.IsInvulnerable) return;

            ApplyDamage(other);
        }

        private void ApplyDamage(Collider2D other)
        {
            Health health = other.GetComponentInParent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
            }

            ReturnToPool();
        }

        public void ReturnToPool()
        {
            if (hasReturned) return;
            hasReturned = true;

            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (trail != null) trail.Clear();

            if (pool != null && prefab != null)
            {
                pool.ReturnObject(pooledInstance != null ? pooledInstance : gameObject, prefab);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private static bool IsWall(Collider2D other)
        {
            if (other == null) return false;
            if (other.CompareTag("Wall")) return true;
            Transform root = other.transform.root;
            return root != null && root.CompareTag("Wall");
        }
    }
}
