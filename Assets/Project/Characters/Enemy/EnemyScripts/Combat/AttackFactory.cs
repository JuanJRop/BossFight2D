using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class AttackFactory : MonoBehaviour
    {
        [SerializeField] private AttackConfiguration configuration;
        public AttackExecutorBase Create(AttackData data)
        {
            var attack = configuration.GetAttackById(data);
            return Instantiate(attack);
        }
    }
}
