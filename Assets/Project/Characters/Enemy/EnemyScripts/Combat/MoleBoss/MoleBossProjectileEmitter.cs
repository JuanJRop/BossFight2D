using System.Collections.Generic;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Core;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public readonly struct MoleProjectileShot
    {
        public MoleProjectileShot(Vector2 direction, float speedMultiplier = 1f, float damageMultiplier = 1f,
            MoleProjectilePalette palette = MoleProjectilePalette.Ember)
        {
            Direction = direction;
            SpeedMultiplier = speedMultiplier;
            DamageMultiplier = damageMultiplier;
            Palette = palette;
        }

        public Vector2 Direction { get; }
        public float SpeedMultiplier { get; }
        public float DamageMultiplier { get; }
        public MoleProjectilePalette Palette { get; }
    }

    public sealed class MoleBossProjectileEmitter
    {
        private readonly ObjectPool pool;
        private readonly MoleBossCombatConfig config;
        private readonly List<GameObject> reservedProjectiles = new();

        public MoleBossProjectileEmitter(ObjectPool pool, MoleBossCombatConfig config)
        {
            this.pool = pool;
            this.config = config;
            if (IsValid) pool.Prewarm(config.ProjectileData.BulletPrefab, config.ProjectilePoolPrewarm);
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
            MoleProjectileShot[] singleShot =
            {
                new(direction, speedMultiplier, damageMultiplier, palette)
            };
            return SpawnVolley(position, singleShot);
        }

        public bool SpawnVolley(Vector2 origin, IReadOnlyList<MoleProjectileShot> shots)
        {
            if (!IsValid || shots == null || shots.Count == 0) return false;

            GameObject prefab = config.ProjectileData.BulletPrefab;
            if (!pool.GetObjects(prefab, shots.Count, reservedProjectiles))
            {
                Debug.LogError($"Boss volley cancelled: the pool could not reserve all {shots.Count} projectiles.");
                return false;
            }

            for (int i = 0; i < shots.Count; i++)
            {
                if (shots[i].Direction.sqrMagnitude >= 0.001f &&
                    reservedProjectiles[i].GetComponentInChildren<AttackEntity>(true) != null &&
                    reservedProjectiles[i].GetComponentInChildren<Rigidbody2D>(true) != null)
                    continue;

                ReturnReserved(prefab);
                Debug.LogError($"Boss volley cancelled: projectile {i + 1}/{shots.Count} is invalid.");
                return false;
            }

            for (int i = 0; i < shots.Count; i++)
            {
                ConfigureProjectile(reservedProjectiles[i], prefab, origin, shots[i]);
            }

            reservedProjectiles.Clear();
            return true;
        }

        private void ConfigureProjectile(GameObject projectile, GameObject prefab, Vector2 origin,
            MoleProjectileShot shot)
        {
            Vector2 direction = shot.Direction.normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            projectile.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, angle));

            AttackEntity entity = projectile.GetComponentInChildren<AttackEntity>(true);
            Rigidbody2D body = projectile.GetComponentInChildren<Rigidbody2D>(true);
            entity.SetPool(pool, prefab, projectile, BulletOwner.Enemy,
                config.BulletDamage * Mathf.Max(0f, shot.DamageMultiplier), config.BulletLifeTime);

            MoleProjectileVisual visual = projectile.GetComponentInChildren<MoleProjectileVisual>(true);
            if (visual != null) visual.Apply(shot.Palette);
            body.linearVelocity = direction * config.BulletSpeed * Mathf.Max(0.1f, shot.SpeedMultiplier);
        }

        private void ReturnReserved(GameObject prefab)
        {
            foreach (GameObject projectile in reservedProjectiles)
            {
                if (projectile != null) pool.ReturnObject(projectile, prefab);
            }
            reservedProjectiles.Clear();
        }
    }
}
