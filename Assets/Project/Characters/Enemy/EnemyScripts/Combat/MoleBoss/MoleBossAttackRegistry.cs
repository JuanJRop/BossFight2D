using System.Collections.Generic;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public sealed class MoleBossAttackRegistry
    {
        private readonly Dictionary<MoleBossAttack, IMoleBossAttack> attacks = new();

        public MoleBossAttackRegistry(IEnumerable<IMoleBossAttack> registeredAttacks)
        {
            foreach (IMoleBossAttack attack in registeredAttacks) attacks[attack.Id] = attack;
        }

        public bool TryGet(MoleBossAttack id, out IMoleBossAttack attack) => attacks.TryGetValue(id, out attack);
    }
}
