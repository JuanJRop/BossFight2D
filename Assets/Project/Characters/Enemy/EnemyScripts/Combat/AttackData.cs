using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    [CreateAssetMenu(fileName = "AttackData", menuName = "BossFight2D/Combat/Attack Data")]
    public class AttackData : ScriptableObject
    {
        [SerializeField, Min(0f)] private float damage = 1f;
        [SerializeField, Min(0f)] private float cooldown = 1f;
        [SerializeField, Min(0f)] private float speed = 5f;
        [SerializeField, Min(0.1f)] private float lifeTime = 5f;
        [SerializeField] private GameObject bulletPrefab;

        public float Damage => damage;
        public float Cooldown => cooldown;
        public float Speed => speed;
        public float LifeTime => lifeTime;
        public GameObject BulletPrefab => bulletPrefab;

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            cooldown = Mathf.Max(0f, cooldown);
            speed = Mathf.Max(0f, speed);
            lifeTime = Mathf.Max(0.1f, lifeTime);
        }
    }
}
