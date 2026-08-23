using System.Collections;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Core;
using Project.Characters.Player.PlayerScripts.Movement;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Combat
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private float homingSpeed = 10f;

        private ObjectPool pool;
        private GameObject prefab;
        private GameObject pooledInstance;
        private BulletOwner owner;
        private float damage;
        private float lifeTime;
        private Rigidbody2D rb;
        private TrailRenderer trail;
        private Transform target;
        private bool isHoming;
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

        private void OnDisable()
        {
            target = null;
            isHoming = false;
        }

        public void SetPool(ObjectPool sourcePool, GameObject sourcePrefab, GameObject sourceInstance, float duration, BulletOwner sourceOwner, float sourceDamage)
        {
            pool = sourcePool;
            prefab = sourcePrefab;
            pooledInstance = sourceInstance;
            lifeTime = Mathf.Max(0.1f, duration);
            owner = sourceOwner;
            damage = Mathf.Max(0f, sourceDamage);

            StopAllCoroutines();
            StartCoroutine(LifeRoutine());
        }

        public void SetTarget(Transform newTarget, bool homing)
        {
            target = newTarget;
            isHoming = homing;
        }

        public void SetTarget(Transform newTarget, bool homing, float speed)
        {
            SetTarget(newTarget, homing);
            if (homing) homingSpeed = Mathf.Max(0.1f, speed);
        }

        private IEnumerator LifeRoutine()
        {
            yield return new WaitForSeconds(lifeTime);
            ReturnToPool();
        }

        private void Update()
        {
            if (!isHoming || target == null || rb == null) return;

            Vector2 direction = (target.position - transform.position).normalized;
            rb.linearVelocity = direction * homingSpeed;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasReturned) return;

            if (other.CompareTag("Wall"))
            {
                ReturnToPool();
                return;
            }

            if (owner == BulletOwner.Player && other.CompareTag("Enemy"))
            {
                ApplyDamage(other);
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

            if (explosionPrefab != null)
            {
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            }

            ReturnToPool();
        }

        public void ReturnToPool()
        {
            if (hasReturned) return;
            hasReturned = true;

            StopAllCoroutines();
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
    }
}
