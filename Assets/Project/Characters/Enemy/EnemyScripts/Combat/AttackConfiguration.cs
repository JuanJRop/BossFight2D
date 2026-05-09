using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    [CreateAssetMenu(fileName = "AttackConfiguration", menuName = "Combat/Attack Configuration")]
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
    
            foreach (var obj in attacks)
            {
                if (obj == null) continue;
    
                var executor = obj.GetComponent<AttackExecutorBase>();
                if (executor == null) continue;
    
                if (executor.Data == null)
                {
                    Debug.LogError($"Executor {obj.name} sin AttackData");
                    continue;
                }
    
                map[executor.Data] = executor;
            }
        }
        
        public AttackExecutorBase GetRandomExecutor()
        {
            if (attacks == null || attacks.Length == 0) return null;
            
            var randomObj = attacks[Random.Range(0, attacks.Length)];
            if (randomObj == null) return null;
            
            return randomObj.GetComponent<AttackExecutorBase>();
        }
    
        public AttackExecutorBase GetAttackById(AttackData data)
        {
            if (data == null) return null;
    
            if (map == null || map.Count == 0)
                BuildDictionary();
    
            map.TryGetValue(data, out var result);
            return result;
        }
    }
}