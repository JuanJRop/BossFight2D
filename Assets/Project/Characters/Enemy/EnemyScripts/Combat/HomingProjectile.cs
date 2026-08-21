using Project.Characters.Player.PlayerScripts.Combat;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class HomingProjectile : AttackExecutorBase
    {
        public override void Execute(AttackContext ctx)
        {
            if (ctx == null || ctx.player == null || ctx.firePoint == null || ctx.pool == null || ctx.data == null)
                return;

            if (ctx.data.BulletPrefab == null) return;

            Rigidbody2D playerBody = ctx.player.GetComponent<Rigidbody2D>();
            if (playerBody == null) playerBody = ctx.player.GetComponentInChildren<Rigidbody2D>();
            Vector2 targetPosition = playerBody != null ? playerBody.position : (Vector2)ctx.player.position;
            Vector2 direction = (targetPosition - (Vector2)ctx.firePoint.position).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            GameObject bullet = ctx.pool.GetObject(
                ctx.data.BulletPrefab,
                ctx.firePoint.position,
                Quaternion.Euler(0f, 0f, angle)
            );

            if (bullet == null) return;

            AttackEntity entity = bullet.GetComponentInChildren<AttackEntity>(true);
            Rigidbody2D body = bullet.GetComponentInChildren<Rigidbody2D>(true);
            if (entity == null || body == null)
            {
                ctx.pool.ReturnObject(bullet, ctx.data.BulletPrefab);
                Debug.LogError("Enemy projectile requires AttackEntity and Rigidbody2D.", bullet);
                return;
            }

            entity.SetPool(
                ctx.pool,
                ctx.data.BulletPrefab,
                bullet,
                BulletOwner.Enemy,
                ctx.data.Damage,
                ctx.data.LifeTime
            );

            body.linearVelocity = direction * ctx.data.Speed;
            if (ctx.animator != null) ctx.animator.SetTrigger("Attack");
        }
    }
}
