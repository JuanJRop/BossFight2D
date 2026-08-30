using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public sealed class MoleBossAttackSelector
    {
        private readonly Queue<MoleBossAttack> phaseOneIntroduction = new();
        private readonly Queue<MoleBossAttack> phaseTwoIntroduction = new();
        private MoleBossAttack? previous;

        public MoleBossAttackSelector()
        {
            Enqueue(phaseOneIntroduction, MoleBossAttack.AimedFan, MoleBossAttack.RadialBurst,
                MoleBossAttack.RockRain, MoleBossAttack.TwinMoleLaser, MoleBossAttack.MinionHorde,
                MoleBossAttack.ChargeDash);
            Enqueue(phaseTwoIntroduction, MoleBossAttack.Spiral, MoleBossAttack.RockRain,
                MoleBossAttack.TwinMoleLaser, MoleBossAttack.MinionHorde, MoleBossAttack.ChargeDash);
        }

        public MoleBossAttack Select(int phase, float playerDistance)
        {
            Queue<MoleBossAttack> introduction = phase == 2 ? phaseTwoIntroduction : phaseOneIntroduction;
            MoleBossAttack selected;
            if (introduction.Count > 0)
            {
                selected = introduction.Dequeue();
            }
            else
            {
                MoleBossAttack[] choices = phase == 2 ? PhaseTwoChoices : PhaseOneChoices;
                selected = choices[Random.Range(0, choices.Length)];
                for (int attempt = 0; attempt < 6 && previous.HasValue && selected == previous.Value; attempt++)
                    selected = choices[Random.Range(0, choices.Length)];

                if (playerDistance < 2.25f && previous != MoleBossAttack.RadialBurst &&
                    Random.value < (phase == 2 ? 0.55f : 0.4f))
                {
                    selected = MoleBossAttack.RadialBurst;
                }
                else if (playerDistance > 7f && previous != MoleBossAttack.ChargeDash &&
                         Random.value < (phase == 2 ? 0.35f : 0.2f))
                {
                    selected = MoleBossAttack.ChargeDash;
                }
            }

            previous = selected;
            return selected;
        }

        private static readonly MoleBossAttack[] PhaseOneChoices =
        {
            MoleBossAttack.AimedFan, MoleBossAttack.AimedFan,
            MoleBossAttack.RadialBurst, MoleBossAttack.RadialBurst,
            MoleBossAttack.RockRain, MoleBossAttack.RockRain,
            MoleBossAttack.TwinMoleLaser, MoleBossAttack.MinionHorde, MoleBossAttack.ChargeDash
        };

        private static readonly MoleBossAttack[] PhaseTwoChoices =
        {
            MoleBossAttack.Spiral, MoleBossAttack.Spiral,
            MoleBossAttack.RockRain, MoleBossAttack.RockRain, MoleBossAttack.RockRain,
            MoleBossAttack.ChargeDash, MoleBossAttack.ChargeDash,
            MoleBossAttack.RadialBurst, MoleBossAttack.TwinMoleLaser, MoleBossAttack.MinionHorde,
            MoleBossAttack.AimedFan
        };

        private static void Enqueue(Queue<MoleBossAttack> queue, params MoleBossAttack[] attacks)
        {
            foreach (MoleBossAttack attack in attacks) queue.Enqueue(attack);
        }
    }
}
