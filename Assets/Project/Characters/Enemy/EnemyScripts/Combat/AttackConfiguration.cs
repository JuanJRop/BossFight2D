using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class AttackConfiguration : ScriptableObject
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

        public AttackExecutorBase GetAttackById(AttackData data)
        {
            if (!idToAttack.TryGetValue(data, out var attack))
            {
                throw new Exception("No Attack");
            }
            return attack;
        }
    }
}