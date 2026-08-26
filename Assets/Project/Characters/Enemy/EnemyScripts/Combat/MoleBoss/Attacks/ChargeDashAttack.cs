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
                context.Config.DashChargeFxPrefab, start, phase == 2 ? 1.65f : 1.35f);
            GameObject[] directionMarkers = CreateDirectionMarkers(context, start, destination, phase);

            float elapsed = 0f;
            while (elapsed < context.Config.DashChargeTime)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    AnimateDirectionMarkers(directionMarkers, elapsed, phase);
                }
                yield return null;
            }

            context.SetState(MoleBossState.Attacking);
            context.Telegraphs.Release(chargeFx);
            ReleaseDirectionMarkers(context, directionMarkers);
            context.TriggerAttackAnimation();

            bool hitPlayer = false;
            float speed = context.Config.DashSpeed * (phase == 2 ? 1.28f : 1f);
            while (Vector2.Distance(context.Body.position, destination) > 0.08f)
            {
                if (!context.IsPaused)
                {
                    Vector2 next = Vector2.MoveTowards(context.Body.position, destination,
                        speed * Time.fixedDeltaTime);
                    context.Body.MovePosition(next);
                    if (!hitPlayer && Vector2.Distance(next, context.Player.Position) <= context.Config.DashHitRadius)
                    {
                        hitPlayer = context.Player.TryDamage(context.Config.DashDamage * (phase == 2 ? 1.18f : 1f));
                        if (hitPlayer)
                            context.Player.ApplyKnockback(direction * context.Config.DashPushForce, 0.32f);
                    }
                }
                yield return new WaitForFixedUpdate();
            }

            context.Body.MovePosition(destination);
            context.Body.linearVelocity = Vector2.zero;
        }

        private static GameObject[] CreateDirectionMarkers(MoleBossCombatContext context, Vector2 start,
            Vector2 destination, int phase)
        {
            int count = phase == 2 ? 8 : 6;
            GameObject[] markers = new GameObject[count];
            Vector2 direction = (destination - start).normalized;
            Vector2 perpendicular = new(-direction.y, direction.x);
            Color color = phase == 2
                ? new Color(1f, 0.18f, 0.04f, 0.96f)
                : new Color(1f, 0.48f, 0.08f, 0.92f);

            for (int index = 0; index < count; index++)
            {
                float t = (index + 1f) / (count + 1f);
                Vector2 center = Vector2.Lerp(start, destination, t);
                Vector2 tip = center + direction * 0.28f;
                Vector2 back = center - direction * 0.2f;
                Vector2 upper = back + perpendicular * 0.22f;
                Vector2 lower = back - perpendicular * 0.22f;
                markers[index] = context.Telegraphs.CreateLine($"Dash direction {index + 1}", color,
                    phase == 2 ? 0.12f : 0.1f, upper, tip, lower);
            }

            return markers;
        }

        private static void AnimateDirectionMarkers(GameObject[] markers, float elapsed, int phase)
        {
            for (int index = 0; index < markers.Length; index++)
            {
                LineRenderer line = markers[index] != null ? markers[index].GetComponent<LineRenderer>() : null;
                if (line == null) continue;
                float wave = (Mathf.Sin(elapsed * (phase == 2 ? 16f : 12f) - index * 0.9f) + 1f) * 0.5f;
                float width = Mathf.Lerp(0.055f, phase == 2 ? 0.18f : 0.14f, wave);
                line.startWidth = width;
                line.endWidth = width;
                Color color = line.startColor;
                color.a = Mathf.Lerp(0.38f, 1f, wave);
                line.startColor = color;
                line.endColor = color;
            }
        }

        private static void ReleaseDirectionMarkers(MoleBossCombatContext context, GameObject[] markers)
        {
            foreach (GameObject marker in markers)
            {
                if (marker != null) context.Telegraphs.Release(marker);
            }
        }
    }
}
