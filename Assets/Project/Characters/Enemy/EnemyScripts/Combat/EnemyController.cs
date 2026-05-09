using System.Collections;
using Project.Characters.Player.PlayerScripts.Core;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class EnemyAttackController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AttackFactory factory;
        [SerializeField] private AttackConfiguration attackConfig;

        [Header("Context")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private Transform player;
        [SerializeField] private ObjectPool pool;
        [SerializeField] private Animator animator;

        [Header("Timing")]
        [SerializeField] private float minDelay = 1f;
        [SerializeField] private float maxDelay = 3f;

        private void Start()
        {
            StartCoroutine(AttackLoop());
        }
        
        private IEnumerator AttackLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));
                ExecuteRandomAttack();
            }
        }

        private void ExecuteRandomAttack()
        {
            // Obtener un executor aleatorio directamente del config
            AttackExecutorBase executor = attackConfig.GetRandomExecutor();
            if (executor == null) return;

            AttackContext ctx = new SimpleAttackContext()
            {
                firePoint = firePoint,
                player = player,
                pool = pool,
                data = executor.Data, // Tomar el Data del executor
                animator = animator
            };

            AttackExecutorBase instance = factory.Create(executor);
            instance.Execute(ctx);
        }
    }
}