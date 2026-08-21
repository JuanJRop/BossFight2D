using System.Collections;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public interface IMoleBossAttack
    {
        MoleBossAttack Id { get; }
        IEnumerator Execute(MoleBossCombatContext context, int phase);
    }
}
