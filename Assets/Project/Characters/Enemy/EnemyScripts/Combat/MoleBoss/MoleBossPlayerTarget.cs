using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Movement;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public sealed class MoleBossPlayerTarget
    {
        private readonly Transform root;
        private readonly Transform combatTransform;
        private readonly Health health;
        private readonly PlayerDodge dodge;
        private readonly PlayerMove movement;

        public MoleBossPlayerTarget(Transform playerRoot)
        {
            root = playerRoot;
            Rigidbody2D body = FindComponent<Rigidbody2D>();
            combatTransform = body != null ? body.transform : root;
            health = FindComponent<Health>();
            dodge = FindComponent<PlayerDodge>();
            movement = FindComponent<PlayerMove>();
        }

        public bool IsValid => root != null && combatTransform != null;
        public Vector2 Position => combatTransform != null ? combatTransform.position : Vector2.zero;
        public bool IsInvulnerable => dodge != null && dodge.IsInvulnerable;

        public bool TryDamage(float damage)
        {
            if (IsInvulnerable) return false;
            if (health != null) health.TakeDamage(Mathf.Max(0f, damage));
            return true;
        }

        public void ApplyKnockback(Vector2 velocity, float duration)
        {
            if (movement != null) movement.ApplyKnockback(velocity, duration);
        }

        private T FindComponent<T>() where T : Component
        {
            if (root == null) return null;
            T component = root.GetComponent<T>();
            return component != null ? component : root.GetComponentInChildren<T>();
        }
    }
}
