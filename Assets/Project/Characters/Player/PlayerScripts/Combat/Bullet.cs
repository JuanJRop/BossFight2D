using System;
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
        private SpriteRenderer spriteRenderer;
        private Transform target;
        private Action<float> onEnemyHit;
        private Vector3 originalScale;
        private Vector3 empoweredScale;
        private Color originalSpriteColor;
        private float originalTrailStartWidth;
        private float originalTrailEndWidth;
        private bool isHoming;
        private bool isEmpowered;
        private bool hasReturned;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            trail = GetComponent<TrailRenderer>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            originalScale = transform.localScale;
            empoweredScale = originalScale;
            if (spriteRenderer != null) originalSpriteColor = spriteRenderer.color;
            if (trail != null)
            {
                originalTrailStartWidth = trail.startWidth;
                originalTrailEndWidth = trail.endWidth;
            }
        }

        private void OnEnable()
        {
            hasReturned = false;
            ResetVisual();
        }

        private void OnDisable()
        {
            target = null;
            onEnemyHit = null;
            isHoming = false;
            ResetVisual();
        }

        public void SetPool(ObjectPool sourcePool, GameObject sourcePrefab, GameObject sourceInstance,
            float duration, BulletOwner sourceOwner, float sourceDamage)
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

        public void SetHitCallback(Action<float> callback)
        {
            onEnemyHit = callback;
        }

        public void SetEmpoweredVisual(bool empowered, float scale, Color color)
        {
            ResetVisual();
            isEmpowered = empowered;
            if (!empowered) return;

            empoweredScale = originalScale * Mathf.Max(1f, scale);
            transform.localScale = empoweredScale;
            if (spriteRenderer != null) spriteRenderer.color = color;
            if (trail != null)
            {
                trail.startWidth = originalTrailStartWidth * Mathf.Max(1.25f, scale);
                trail.endWidth = originalTrailEndWidth * Mathf.Max(1.1f, scale);
            }
        }

        private IEnumerator LifeRoutine()
        {
            yield return new WaitForSeconds(lifeTime);
            ReturnToPool();
        }

        private void Update()
        {
            if (isHoming && target != null && rb != null)
            {
                Vector2 direction = (target.position - transform.position).normalized;
                rb.linearVelocity = direction * homingSpeed;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (isEmpowered)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.09f;
                transform.localScale = empoweredScale * pulse;
            }
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
            float before = health != null ? health.CurrentHealth : 0f;
            if (health != null) health.TakeDamage(damage);
            float dealtDamage = health != null ? Mathf.Max(0f, before - health.CurrentHealth) : 0f;
            if (owner == BulletOwner.Player && dealtDamage > 0f) onEnemyHit?.Invoke(dealtDamage);

            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);

            ReturnToPool();
        }

        public void ReturnToPool()
        {
            if (hasReturned) return;
            hasReturned = true;

            StopAllCoroutines();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (trail != null) trail.Clear();
            ResetVisual();

            if (pool != null && prefab != null)
                pool.ReturnObject(pooledInstance != null ? pooledInstance : gameObject, prefab);
            else
                Destroy(gameObject);
        }

        private void ResetVisual()
        {
            isEmpowered = false;
            empoweredScale = originalScale;
            transform.localScale = originalScale;
            if (spriteRenderer != null) spriteRenderer.color = originalSpriteColor;
            if (trail != null)
            {
                trail.startWidth = originalTrailStartWidth;
                trail.endWidth = originalTrailEndWidth;
            }
        }
    }
}
