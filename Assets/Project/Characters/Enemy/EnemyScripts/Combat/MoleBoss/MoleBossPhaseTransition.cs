using System.Collections;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public sealed class MoleBossPhaseTransition
    {
        private readonly IMoleBossAttack radialBurst;

        public MoleBossPhaseTransition(IMoleBossAttack radialBurst)
        {
            this.radialBurst = radialBurst;
        }

        public IEnumerator Execute(MoleBossCombatContext context)
        {
            context.SetState(MoleBossState.PhaseTransition);
            if (context.Movement != null && context.Movement.HasValidSpots)
                yield return context.Movement.BurrowToRandomSpot(Mathf.Max(0.2f, context.Config.PhaseTransitionTime * 0.45f));

            GameObject ring = context.Telegraphs.CreateCircle("Phase two shockwave", context.BossPosition, 0.5f,
                new Color(1f, 0.15f, 0.05f, 1f));
            LineRenderer line = ring != null ? ring.GetComponent<LineRenderer>() : null;
            float elapsed = 0f;
            while (elapsed < context.Config.PhaseTransitionTime)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / context.Config.PhaseTransitionTime);
                    MoleBossTelegraphService.UpdateCircle(line, context.BossPosition, Mathf.Lerp(0.5f, 3.5f, progress));
                    if (line != null)
                    {
                        Color color = line.startColor;
                        color.a = 1f - progress;
                        line.startColor = color;
                        line.endColor = color;
                    }
                }
                yield return null;
            }
            context.Telegraphs.Release(ring);
            yield return radialBurst.Execute(context, 2);
        }
    }
}
