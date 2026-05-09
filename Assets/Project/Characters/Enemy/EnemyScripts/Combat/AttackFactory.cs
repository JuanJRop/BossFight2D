using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class AttackFactory : MonoBehaviour
    {
        public AttackExecutorBase Create(AttackExecutorBase prefab)
        {
            return Instantiate(prefab);
        }
    }
}