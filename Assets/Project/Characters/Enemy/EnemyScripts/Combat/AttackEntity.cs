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
        private ObjectPool pool;
        private BulletOwner owner;
        private AttackData data;
        private float damage;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            trail = GetComponent<TrailRenderer>();
        }
        public void SetPool(ObjectPool pool, GameObject prefab, BulletOwner owner, float damage)
        {
            this.pool = pool;
            this.owner = owner;
            this.prefab = prefab;
            this.damage = damage;
        }
        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Wall"))
            {
                ReturnToPool();
                return;
            }
            if (owner == BulletOwner.Player && other.CompareTag("Enemy"))
            {
                ApplyDamage(other);
            }
            if (other.CompareTag("Player") && other.GetComponent<PlayerDodge>().IsInvulnerable) return;
            if (owner == BulletOwner.Enemy && other.CompareTag("Player"))
            {
                ApplyDamage(other);
            }
        }
        private void ApplyDamage(Collider2D other)
        {
            Health health = other.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
            }
            ReturnToPool();
        }
        private void ReturnToPool()
        {
            rb.linearVelocity = Vector2.zero;

            if (trail != null)
                trail.Clear();

            pool.ReturnObject(gameObject, prefab);
        }
    }
}
