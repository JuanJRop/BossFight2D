using System.Collections;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks
{
    public sealed class ChargeDashAttack : IMoleBossAttack
    {
        public MoleBossAttack Id => MoleBossAttack.ChargeDash;

        public IEnumerator Execute(MoleBossCombatContext context, int phase)
        {
            if (context.Body == null) yield break;
            if (context.Movement != null) context.Movement.ForceEmerge();

            Vector2 start = context.Body.position;
            Vector2 direction = (context.Player.Position - start).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.down;
            context.GetArenaBounds(out Vector2 minimum, out Vector2 maximum);
            Vector2 destination = start + direction * context.Config.DashMaxDistance;
            destination.x = Mathf.Clamp(destination.x, minimum.x + 0.6f, maximum.x - 0.6f);
            destination.y = Mathf.Clamp(destination.y, minimum.y + 0.6f, maximum.y - 0.6f);

            context.SetState(MoleBossState.Telegraphing);
            GameObject chargeFx = context.Telegraphs.CreatePrefab("Dash charge FX",
                context.Config.DashChargeFxPrefab, start, 1.35f);
            GameObject guide = context.Telegraphs.CreateLine("Dash trajectory", new Color(1f, 0.08f, 0.08f, 0.95f),
                0.12f, start, destination);
            LineRenderer line = guide != null ? guide.GetComponent<LineRenderer>() : null;
            float elapsed = 0f;
            while (elapsed < context.Config.DashChargeTime)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    if (line != null)
                    {
                        float pulse = 0.1f + Mathf.PingPong(elapsed * 0.22f, 0.13f);
                        line.startWidth = pulse;
                        line.endWidth = pulse;
                    }
                }
                yield return null;
            }

            context.SetState(MoleBossState.Attacking);
            context.Telegraphs.Release(chargeFx);
            context.TriggerAttackAnimation();
            bool hitPlayer = false;
            float speed = context.Config.DashSpeed * (phase == 2 ? 1.18f : 1f);
            while (Vector2.Distance(context.Body.position, destination) > 0.08f)
            {
                if (!context.IsPaused)
                {
                    Vector2 next = Vector2.MoveTowards(context.Body.position, destination, speed * Time.fixedDeltaTime);
                    context.Body.MovePosition(next);
                    if (!hitPlayer && Vector2.Distance(next, context.Player.Position) <= context.Config.DashHitRadius)
                    {
                        hitPlayer = context.Player.TryDamage(context.Config.DashDamage);
                        if (hitPlayer) context.Player.ApplyKnockback(direction * context.Config.DashPushForce, 0.32f);
                    }
                }
                yield return new WaitForFixedUpdate();
            }

            context.Body.MovePosition(destination);
            context.Body.linearVelocity = Vector2.zero;
            context.Telegraphs.Release(guide);
        }
    }
}
