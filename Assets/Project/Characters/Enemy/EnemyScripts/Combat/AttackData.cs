using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    [CreateAssetMenu(fileName = "AttackData", menuName = "BossFight2D/Combat/Attack Data")]
    public class AttackData : ScriptableObject
    {
        [Min(0f)] public float damage = 1f;
        [Min(0f)] public float cooldown = 1f;
        [Min(0f)] public float speed = 5f;
        [Min(0.1f)] public float lifeTime = 5f;
        public GameObject bulletPrefab;

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            cooldown = Mathf.Max(0f, cooldown);
            speed = Mathf.Max(0f, speed);
            lifeTime = Mathf.Max(0.1f, lifeTime);
        }
    }
}
