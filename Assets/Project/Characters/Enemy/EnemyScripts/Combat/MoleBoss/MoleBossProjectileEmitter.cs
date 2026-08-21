using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Core;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public sealed class MoleBossProjectileEmitter
    {
        private readonly ObjectPool pool;
        private readonly MoleBossCombatConfig config;

        public MoleBossProjectileEmitter(ObjectPool pool, MoleBossCombatConfig config)
        {
            this.pool = pool;
            this.config = config;
        }

        public bool IsValid => pool != null && config != null && config.ProjectileData != null &&
                               config.ProjectileData.BulletPrefab != null;
        public Sprite ProjectileSprite
        {
            get
            {
                if (!IsValid) return null;
                SpriteRenderer renderer = config.ProjectileData.BulletPrefab.GetComponentInChildren<SpriteRenderer>(true);
                return renderer != null ? renderer.sprite : null;
            }
        }

        public bool Spawn(Vector2 position, Vector2 direction, float speedMultiplier = 1f, float damageMultiplier = 1f,
            MoleProjectilePalette palette = MoleProjectilePalette.Ember)
        {
            if (!IsValid || direction.sqrMagnitude < 0.001f) return false;

            direction.Normalize();
            GameObject prefab = config.ProjectileData.BulletPrefab;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GameObject projectile = pool.GetObject(prefab, position, Quaternion.Euler(0f, 0f, angle));
            if (projectile == null) return false;

            AttackEntity entity = projectile.GetComponentInChildren<AttackEntity>(true);
            Rigidbody2D body = projectile.GetComponentInChildren<Rigidbody2D>(true);
            if (entity == null || body == null)
            {
                pool.ReturnObject(projectile, prefab);
                return false;
            }

            entity.SetPool(pool, prefab, projectile, BulletOwner.Enemy,
                config.BulletDamage * Mathf.Max(0f, damageMultiplier), config.BulletLifeTime);
            MoleProjectileVisual visual = projectile.GetComponentInChildren<MoleProjectileVisual>(true);
            if (visual != null) visual.Apply(palette);
            body.linearVelocity = direction * config.BulletSpeed * Mathf.Max(0.1f, speedMultiplier);
            return true;
        }
    }
}
