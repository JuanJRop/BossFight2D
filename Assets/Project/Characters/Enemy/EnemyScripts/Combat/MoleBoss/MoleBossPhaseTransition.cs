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
                yield return context.Movement.BurrowToRandomSpot(
                    Mathf.Max(0.2f, context.Config.PhaseTransitionTime * 0.45f));

            GameObject energy = context.Telegraphs.CreatePrefab("Phase transition energy",
                context.Config.DashChargeFxPrefab, context.BossPosition, 2.15f);
            GameObject ring = context.Telegraphs.CreateCircle("Phase two shockwave", context.BossPosition, 0.5f,
                new Color(1f, 0.12f, 0.02f, 1f));
            LineRenderer line = ring != null ? ring.GetComponent<LineRenderer>() : null;
            float elapsed = 0f;
            while (elapsed < context.Config.PhaseTransitionTime)
            {
                if (!context.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / context.Config.PhaseTransitionTime);
                    float impactProgress = Mathf.SmoothStep(0f, 1f, progress);
                    context.ApplyPhasePresentation(2, impactProgress);
                    MoleBossTelegraphService.UpdateCircle(line, context.BossPosition,
                        Mathf.Lerp(0.5f, 4.25f, progress));
                    if (line != null)
                    {
                        Color color = Color.Lerp(new Color(1f, 0.78f, 0.08f, 1f),
                            context.Config.PhaseTwoBossColor, progress);
                        color.a = 1f - progress;
                        line.startColor = color;
                        line.endColor = color;
                    }
                }
                yield return null;
            }

            context.ApplyPhasePresentation(2);
            context.Telegraphs.Release(ring);
            context.Telegraphs.Release(energy);
            yield return radialBurst.Execute(context, 2);
        }
    }
}
