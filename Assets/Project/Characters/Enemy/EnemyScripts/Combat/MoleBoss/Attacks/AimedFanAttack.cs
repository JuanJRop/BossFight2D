using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks
{
    public sealed class AimedFanAttack : IMoleBossAttack
    {
        public MoleBossAttack Id => MoleBossAttack.AimedFan;

        public IEnumerator Execute(MoleBossCombatContext context, int phase)
        {
            int volleys = context.Config.FanVolleys(phase);
            int count = context.Config.FanProjectiles(phase);
            float spread = context.Config.FanSpread(phase);

            for (int volley = 0; volley < volleys; volley++)
            {
                context.SetState(MoleBossState.Telegraphing);
                GameObject warning = context.Telegraphs.CreateLine("Aimed fan warning",
                    new Color(1f, 0.65f, 0.1f, 0.9f), 0.08f, context.FirePosition, context.Player.Position);
                yield return context.Wait(0.35f);
                context.Telegraphs.Release(warning);

                context.SetState(MoleBossState.Attacking);
                Vector2 direction = (context.Player.Position - context.FirePosition).normalized;
                float middle = (count - 1) * 0.5f;
                List<MoleProjectileShot> shots = new(count);
                for (int i = 0; i < count; i++)
                {
                    shots.Add(new MoleProjectileShot(
                        MoleBossCombatContext.Rotate(direction, (i - middle) * spread),
                        1f, 1f, MoleProjectilePalette.Cyan));
                }

                context.Projectiles.SpawnVolley(context.FirePosition, shots);
                context.TriggerAttackAnimation();
                yield return context.Wait(phase == 2 ? 0.32f : 0.48f);
            }
        }
    }
}
