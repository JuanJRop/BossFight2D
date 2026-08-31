using System;
using System.Collections.Generic;
using Project.Characters.Player.PlayerScripts.Combat;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public abstract class AttackFactory : MonoBehaviour
    {
        [SerializeField] private EntityAttack[] attacks;
        private Dictionary<string, EntityAttack> idToAttack;

        private void Awake()
        {
            idToAttack = new Dictionary<string, EntityAttack>();
            foreach (var attack in attacks)
            {
                idToAttack.Add(attack.ID, attack);
                
            }
        }

        public  EntityAttack CreateAttack(string id)
        {
            if (!idToAttack.TryGetValue(id, out  var attack))
            {
                
            }

            return attack;
        }

    }
}
