using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public abstract class EntityAttack
    {
        [SerializeField] private string id;

        public string ID => id;
        
    }
}
