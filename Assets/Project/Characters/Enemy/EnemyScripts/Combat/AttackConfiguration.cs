using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    [CreateAssetMenu(fileName = "AttackConfiguration", menuName = "BossFight2D/Combat/Attack Configuration")]
    public class AttackConfiguration : ScriptableObject
    {
        [SerializeField] private GameObject[] attacks;

        private Dictionary<AttackData, AttackExecutorBase> map;

        private void OnEnable()
        {
            BuildDictionary();
        }

        private void BuildDictionary()
        {
            map = new Dictionary<AttackData, AttackExecutorBase>();
            if (attacks == null) return;

            foreach (GameObject attackObject in attacks)
            {
                AttackExecutorBase executor = GetExecutor(attackObject);
                if (executor == null || executor.Data == null) continue;
                map[executor.Data] = executor;
            }
        }

        public AttackExecutorBase GetRandomExecutor()
        {
            if (attacks == null || attacks.Length == 0) return null;

            int startIndex = Random.Range(0, attacks.Length);
            for (int offset = 0; offset < attacks.Length; offset++)
            {
                AttackExecutorBase executor = GetExecutor(attacks[(startIndex + offset) % attacks.Length]);
                if (executor != null && executor.Data != null) return executor;
            }

            return null;
        }

        public AttackExecutorBase GetAttackById(AttackData data)
        {
            if (data == null) return null;
            if (map == null || map.Count == 0) BuildDictionary();
            map.TryGetValue(data, out AttackExecutorBase result);
            return result;
        }

        private static AttackExecutorBase GetExecutor(GameObject attackObject)
        {
            return attackObject != null ? attackObject.GetComponentInChildren<AttackExecutorBase>(true) : null;
        }
    }
}
