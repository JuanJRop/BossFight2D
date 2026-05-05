using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class AttackFactory
    {
        [SerializeField]private AttackExecutorBase[] attacks;
        private Dictionary<AttackData, AttackExecutorBase> idToAttack;

        private void Awake()
        {
            idToAttack = new Dictionary<AttackData, AttackExecutorBase>();

            foreach (var attack in attacks)
            {
                idToAttack.Add(attack.Data, attack);
            }
        }

        public AttackExecutorBase Create(AttackData data)
        {
            if (idToAttack.TryGetValue(data, out var attack))
            {
                return Object.Instantiate(attack);
            }
            return null;
        }
    }

    public partial class AttackConfiguration : ScriptableObject
    {
        
    }
}
