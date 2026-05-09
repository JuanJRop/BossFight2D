using UnityEngine;
namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    [CreateAssetMenu]
    public class AttackData : ScriptableObject
    {
        public float damage;
        public float cooldown;
        public float speed;
        public GameObject bulletPrefab;
    }
}