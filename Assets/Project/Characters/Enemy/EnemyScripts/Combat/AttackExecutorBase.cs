using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public abstract class AttackExecutorBase : MonoBehaviour
    {
        [SerializeField] private AttackData data;
        public AttackData Data => data;
        public abstract void Execute(AttackContext ctx);
    }
}