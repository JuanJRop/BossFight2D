using Project.Characters.Player.PlayerScripts.Combat;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class HomingProjectile : AttackExecutorBase
    {
        public override void Execute(AttackContext ctx)
        {
            ShootAtPlayer(ctx);
        }

        private void ShootAtPlayer(AttackContext ctx)
        {
            if (ctx.player == null) return;

            Vector2 direction = (ctx.player.position - ctx.firePoint.position).normalized;

            GameObject bullet = ctx.pool.GetObject(ctx.data.bulletPrefab);
            AttackEntity entity = bullet.GetComponent<AttackEntity>();

            entity.SetPool(ctx.pool, ctx.data.bulletPrefab, BulletOwner.Enemy, ctx.data.damage);

            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            rb.linearVelocity = direction * ctx.data.speed;

            if (ctx.animator != null)
                ctx.animator.SetTrigger("Attack");
        }
    }
}