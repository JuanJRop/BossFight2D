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
            float safeCenter = Mathf.Clamp(context.Player.Position.x, minimum.x + safeWidth, maximum.x - safeWidth);

            context.SetState(MoleBossState.Telegraphing);
            GameObject left = CreateGuide(context, safeCenter - safeWidth * 0.5f, minimum.y, maximum.y, "Corridor left");
            GameObject right = CreateGuide(context, safeCenter + safeWidth * 0.5f, minimum.y, maximum.y, "Corridor right");
            yield return context.Wait(0.9f);
            context.Telegraphs.Release(left);
            context.Telegraphs.Release(right);

            context.SetState(MoleBossState.Attacking);
            context.TriggerAttackAnimation();
            for (int wave = 0; wave < context.Config.CorridorWaves(phase); wave++)
            {
                float offset = Mathf.Sin(wave * 0.72f) * (phase == 2 ? 1.3f : 0.75f);
                float waveCenter = Mathf.Clamp(safeCenter + offset, minimum.x + safeWidth, maximum.x - safeWidth);
                bool fromTop = phase == 1 || wave % 2 == 0;
                float y = fromTop ? maximum.y + 0.35f : minimum.y - 0.35f;
                Vector2 direction = fromTop ? Vector2.down : Vector2.up;
                for (float x = minimum.x; x <= maximum.x; x += context.Config.CorridorSpacing(phase))
                {
                    if (Mathf.Abs(x - waveCenter) >= safeWidth * 0.5f)
                        context.Projectiles.Spawn(new Vector2(x, y), direction, phase == 2 ? 1.18f : 1f);
                }
                yield return context.Wait(phase == 2 ? 0.34f : 0.48f);
            }
        }

        private static GameObject CreateGuide(MoleBossCombatContext context, float x, float bottom, float top, string name)
        {
            return context.Telegraphs.CreateLine(name, new Color(0.2f, 1f, 0.45f, 0.85f), 0.07f,
                new Vector2(x, bottom), new Vector2(x, top));
        }
    }
}
