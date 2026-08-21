using System.Collections;
using Project.Characters.Player.PlayerScripts.Core;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class EnemyAttackController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AttackConfiguration attackConfig;

        [Header("Context")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private Transform player;
        [SerializeField] private ObjectPool pool;
        [SerializeField] private Animator animator;

        [Header("Timing")]
        [SerializeField] private float minDelay = 1f;
        [SerializeField] private float maxDelay = 3f;

        private Coroutine attackRoutine;

        private void OnEnable()
        {
            RestartAttacks();
        }

        private void OnDisable()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
        }

        public void RestartAttacks()
        {
            if (!isActiveAndEnabled) return;

            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = StartCoroutine(AttackLoop());
        }

        private IEnumerator AttackLoop()
        {
            while (enabled)
            {
                float delay = Random.Range(Mathf.Max(0f, minDelay), Mathf.Max(minDelay, maxDelay));
                yield return new WaitForSeconds(delay);

                AttackExecutorBase executor = attackConfig != null ? attackConfig.GetRandomExecutor() : null;
                if (executor == null) continue;

                ExecuteAttack(executor);
                if (executor.Data != null && executor.Data.cooldown > 0f)
                {
                    yield return new WaitForSeconds(executor.Data.cooldown);
                }
            }
        }

        private void ExecuteAttack(AttackExecutorBase executor)
        {
            AttackContext context = new SimpleAttackContext
            {
                firePoint = firePoint,
                player = player,
                pool = pool,
                data = executor.Data,
                animator = animator
            };

            executor.Execute(context);
        }
    }
}
