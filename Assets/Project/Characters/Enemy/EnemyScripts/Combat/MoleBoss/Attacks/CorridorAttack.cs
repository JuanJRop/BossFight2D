using System.Collections;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks
{
    public sealed class CorridorAttack : IMoleBossAttack
    {
        public MoleBossAttack Id => MoleBossAttack.Corridor;

        public IEnumerator Execute(MoleBossCombatContext context, int phase)
        {
            context.GetArenaBounds(out Vector2 minimum, out Vector2 maximum);
            float safeWidth = context.Config.CorridorWidth(phase);
            int waves = context.Config.CorridorWaves(phase);
            int pattern = Random.Range(0, 3);
            float firstCenter = GetWaveCenter(0, waves, pattern, minimum.x, maximum.x, safeWidth);

            context.SetState(MoleBossState.Telegraphing);
            GameObject left = CreateGuide(context, firstCenter - safeWidth * 0.5f, minimum.y, maximum.y, "Corridor left");
            GameObject right = CreateGuide(context, firstCenter + safeWidth * 0.5f, minimum.y, maximum.y, "Corridor right");
            GameObject sweep = context.Telegraphs.CreateLine("Corridor full-arena route",
                new Color(0.2f, 1f, 0.45f, 0.72f), 0.09f,
                new Vector2(minimum.x + safeWidth * 0.5f, (minimum.y + maximum.y) * 0.5f),
                new Vector2(maximum.x - safeWidth * 0.5f, (minimum.y + maximum.y) * 0.5f));
            yield return context.Wait(0.85f);
            context.Telegraphs.Release(left);
            context.Telegraphs.Release(right);
            context.Telegraphs.Release(sweep);

            context.SetState(MoleBossState.Attacking);
            context.TriggerAttackAnimation();
            for (int wave = 0; wave < waves; wave++)
            {
                float waveCenter = GetWaveCenter(wave, waves, pattern, minimum.x, maximum.x, safeWidth);
                GameObject nextLeft = CreateGuide(context, waveCenter - safeWidth * 0.5f, minimum.y, maximum.y,
                    "Moving corridor left");
                GameObject nextRight = CreateGuide(context, waveCenter + safeWidth * 0.5f, minimum.y, maximum.y,
                    "Moving corridor right");
                yield return context.Wait(phase == 2 ? 0.08f : 0.12f);
                context.Telegraphs.Release(nextLeft);
                context.Telegraphs.Release(nextRight);

                bool fromTop = phase == 1 || wave % 2 == 0;
                float y = fromTop ? maximum.y + 0.35f : minimum.y - 0.35f;
                Vector2 direction = fromTop ? Vector2.down : Vector2.up;
                for (float x = minimum.x; x <= maximum.x; x += context.Config.CorridorSpacing(phase))
                {
                    if (Mathf.Abs(x - waveCenter) >= safeWidth * 0.5f)
                        context.Projectiles.Spawn(new Vector2(x, y), direction, phase == 2 ? 1.18f : 1f);
                }
                yield return context.Wait(phase == 2 ? 0.26f : 0.38f);
            }
        }

        private static float GetWaveCenter(int wave, int waves, int pattern, float left, float right, float safeWidth)
        {
            float t = waves <= 1 ? 0f : wave / (float)(waves - 1);
            float normalized;
            switch (pattern)
            {
                case 0:
                    normalized = Mathf.PingPong(t * 2f, 1f) * 2f - 1f;
                    break;
                case 1:
                    normalized = wave % 4 switch
                    {
                        0 => -1f,
                        1 => 0.25f,
                        2 => 1f,
                        _ => -0.35f
                    };
                    break;
                default:
                    normalized = Mathf.Sin(t * Mathf.PI * 3f - Mathf.PI * 0.5f);
                    break;
            }

            float center = (left + right) * 0.5f;
            float travel = Mathf.Max(0f, (right - left - safeWidth) * 0.46f);
            return center + normalized * travel;
        }

        private static GameObject CreateGuide(MoleBossCombatContext context, float x, float bottom, float top, string name)
        {
            return context.Telegraphs.CreateLine(name, new Color(0.2f, 1f, 0.45f, 0.85f), 0.07f,
                new Vector2(x, bottom), new Vector2(x, top));
        }
    }
}
