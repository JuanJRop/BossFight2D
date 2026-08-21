using System.Collections;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks
{
    public sealed class SpiralAttack : IMoleBossAttack
    {
        public MoleBossAttack Id => MoleBossAttack.Spiral;

        public IEnumerator Execute(MoleBossCombatContext context, int phase)
        {
            context.SetState(MoleBossState.Telegraphing);
            GameObject warning = context.Telegraphs.CreateCircle("Spiral warning", context.BossPosition, 1.5f,
                new Color(0.9f, 0.2f, 1f, 0.9f));
            yield return context.Wait(0.65f);
            context.Telegraphs.Release(warning);

            context.SetState(MoleBossState.Attacking);
            context.TriggerAttackAnimation();
            int steps = context.Config.SpiralSteps(phase);
            int arms = context.Config.SpiralArms(phase);
            for (int step = 0; step < steps; step++)
            {
                float angle = step * (phase == 2 ? 15f : 18f);
                for (int arm = 0; arm < arms; arm++)
                    context.Projectiles.Spawn(context.FirePosition,
                        MoleBossCombatContext.DirectionFromAngle(angle + arm * 360f / arms), 0.92f, 1f,
                        MoleProjectilePalette.Rose);
                yield return context.Wait(phase == 2 ? 0.105f : 0.14f);
            }
        }
    }
}
