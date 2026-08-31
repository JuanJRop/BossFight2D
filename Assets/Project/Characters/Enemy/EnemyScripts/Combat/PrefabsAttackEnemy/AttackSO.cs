using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.PrefabsAttackEnemy
{
    [CreateAssetMenu(fileName = "AttackSO", menuName = "Scriptable Objects/AttackSO")]
    public class AttackSo : ScriptableObject
    {
        public float damage;
        public float speed;
        public Transform target;
        public GameObject attackPrefab;
    }
}
