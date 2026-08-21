using System.Collections;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks
{
    public sealed class CrossfireAttack : IMoleBossAttack
    {
        public MoleBossAttack Id => MoleBossAttack.Crossfire;

        public IEnumerator Execute(MoleBossCombatContext context, int phase)
        {
            context.GetArenaBounds(out Vector2 minimum, out Vector2 maximum);
            float gap = context.Config.CrossfireGap(phase);
            int waves = context.Config.CrossfireWaves(phase);

            context.SetState(MoleBossState.Telegraphing);
            GameObject preview = context.Telegraphs.CreateLine("Crossfire sweep",
                new Color(0.15f, 0.9f, 1f, 0.9f), 0.1f,
                new Vector2(minimum.x, minimum.y + gap),
                new Vector2(maximum.x, maximum.y - gap));
            yield return context.Wait(0.8f);
            context.Telegraphs.Release(preview);

            context.SetState(MoleBossState.Attacking);
            context.TriggerAttackAnimation();
            for (int wave = 0; wave < waves; wave++)
            {
                float safeCenter = GetSafeCenter(wave, waves, minimum.y, maximum.y, gap, phase);
                GameObject lower = CreateGuide(context, minimum.x, maximum.x, safeCenter - gap * 0.5f);
                GameObject upper = CreateGuide(context, minimum.x, maximum.x, safeCenter + gap * 0.5f);
                yield return context.Wait(phase == 2 ? 0.09f : 0.13f);
                context.Telegraphs.Release(lower);
                context.Telegraphs.Release(upper);

                bool fromLeft = wave % 2 == 0;
                SpawnWall(context, phase, minimum, maximum, safeCenter, gap, fromLeft);
                if (phase == 2 && wave % 3 == 2)
                    SpawnWall(context, phase, minimum, maximum, safeCenter, gap, !fromLeft);

                yield return context.Wait(phase == 2 ? 0.3f : 0.42f);
            }
        }

        private static float GetSafeCenter(int wave, int waves, float bottom, float top, float gap, int phase)
        {
            float t = waves <= 1 ? 0f : wave / (float)(waves - 1);
            float normalized = phase == 2
                ? Mathf.Sin(t * Mathf.PI * 3f - Mathf.PI * 0.5f)
                : Mathf.Lerp(-1f, 1f, t);
            float center = (bottom + top) * 0.5f;
            float travel = Mathf.Max(0f, (top - bottom - gap) * 0.46f);
            return center + normalized * travel;
        }

        private static void SpawnWall(MoleBossCombatContext context, int phase, Vector2 minimum,
            Vector2 maximum, float safeCenter, float gap, bool fromLeft)
        {
            float x = fromLeft ? minimum.x - 0.35f : maximum.x + 0.35f;
            Vector2 direction = fromLeft ? Vector2.right : Vector2.left;
            for (float y = minimum.y; y <= maximum.y; y += context.Config.CrossfireSpacing(phase))
            {
                if (Mathf.Abs(y - safeCenter) >= gap * 0.5f)
                    context.Projectiles.Spawn(new Vector2(x, y), direction, phase == 2 ? 1.16f : 1.02f);
            }
        }

        private static GameObject CreateGuide(MoleBossCombatContext context, float left, float right, float y)
        {
            return context.Telegraphs.CreateLine("Crossfire safe guide", new Color(0.15f, 0.9f, 1f, 0.82f), 0.07f,
                new Vector2(left, y), new Vector2(right, y));
        }
    }
}
