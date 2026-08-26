using System;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public sealed class MoleMinionEnemy : MonoBehaviour
    {
        private MoleBossPlayerTarget target;
        private Health health;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Action<GameObject> defeated;
        private Vector2 arenaMinimum;
        private Vector2 arenaMaximum;
        private float moveSpeed;
        private float contactDamage;
        private float contactCooldown;
        private float nextContactTime;

        public bool IsAlive => health != null && health.IsAlive;

        public void Configure(MoleBossPlayerTarget playerTarget, Health minionHealth, Vector2 minimum,
            Vector2 maximum, float speed, float damage, float damageCooldown, Action<GameObject> onDefeated)
        {
            target = playerTarget;
            health = minionHealth;
            arenaMinimum = minimum;
            arenaMaximum = maximum;
            moveSpeed = Mathf.Max(0.1f, speed);
            contactDamage = Mathf.Max(0f, damage);
            contactCooldown = Mathf.Max(0.1f, damageCooldown);
            defeated = onDefeated;
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (health != null) health.OnDied += HandleDied;
        }

        private void FixedUpdate()
        {
            if (body == null || target == null || !target.IsValid || !IsAlive) return;
            if (UIManager.instance != null && UIManager.instance.IsPaused)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 direction = (target.Position - (Vector2)transform.position).normalized;
            body.linearVelocity = direction * moveSpeed;
            Vector2 clamped = new(
                Mathf.Clamp(body.position.x, arenaMinimum.x, arenaMaximum.x),
                Mathf.Clamp(body.position.y, arenaMinimum.y, arenaMaximum.y));
            body.position = clamped;
            if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.05f)
                spriteRenderer.flipX = direction.x < 0f;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!IsAlive || Time.time < nextContactTime || !other.CompareTag("Player")) return;
            if (target.TryDamage(contactDamage)) nextContactTime = Time.time + contactCooldown;
        }

        private void HandleDied()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
            defeated?.Invoke(gameObject);
        }

        private void OnDestroy()
        {
            if (health != null) health.OnDied -= HandleDied;
        }
    }
}
