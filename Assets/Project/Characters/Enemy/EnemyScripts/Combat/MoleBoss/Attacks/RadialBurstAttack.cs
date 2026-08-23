using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks
{
    public sealed class RadialBurstAttack : IMoleBossAttack
    {
        public MoleBossAttack Id => MoleBossAttack.RadialBurst;

        public IEnumerator Execute(MoleBossCombatContext context, int phase)
        {
            int rings = context.Config.RadialRings(phase);
            int count = context.Config.RadialCount(phase);
            context.SetState(MoleBossState.Telegraphing);
            GameObject warning = context.Telegraphs.CreateCircle("Radial warning", context.BossPosition, 2f,
                new Color(1f, 0.45f, 0.05f, 0.9f));
            yield return context.Wait(0.7f);
            context.Telegraphs.Release(warning);

            context.SetState(MoleBossState.Attacking);
            for (int ring = 0; ring < rings; ring++)
            {
                float offset = ring * (180f / count);
                List<MoleProjectileShot> shots = new(count);
                for (int i = 0; i < count; i++)
                {
                    shots.Add(new MoleProjectileShot(
                        MoleBossCombatContext.DirectionFromAngle(offset + i * 360f / count),
                        phase == 2 ? 1.12f : 1f, 1f, MoleProjectilePalette.Violet));
                }

                context.Projectiles.SpawnVolley(context.FirePosition, shots);
                context.TriggerAttackAnimation();
                yield return context.Wait(phase == 2 ? 0.42f : 0.6f);
            }
        }
    }
}
