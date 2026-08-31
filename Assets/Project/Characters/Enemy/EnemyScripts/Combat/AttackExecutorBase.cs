using Project.Characters.Player.PlayerScripts.Combat;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class AttackContext
    {
        public Transform player;
        public Transform firePoint;
        public ObjectPool pool;
        public AttackData data;
        public Animator animator;

        public AttackContext(Transform player, Transform firePoint, ObjectPool pool, AttackData data, Animator animator)
        {
            this.player = player;
            this.firePoint = firePoint;
            this.pool = pool;
            this.data = data;
            this.animator = animator;
        }
    }

    public abstract class AttackExecutorBase : MonoBehaviour
    {
        [SerializeField] private AttackData data;

        public AttackData Data => data;

        public abstract void Execute(AttackContext ctx);
    }
}
