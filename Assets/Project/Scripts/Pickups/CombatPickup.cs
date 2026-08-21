using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using UnityEngine;

namespace Project.Scripts.Pickups
{
    public enum CombatPickupType
    {
        Health,
        Mana
    }

    public sealed class CombatPickup : MonoBehaviour
    {
        [SerializeField] private CombatPickupType pickupType;
        [SerializeField, Min(0f)] private float amount = 50f;
        [SerializeField, Min(0f)] private float bobHeight = 0.18f;
        [SerializeField, Min(0.1f)] private float bobSpeed = 2.6f;
        [SerializeField] private float rotationSpeed = 24f;

        private Vector3 origin;
        private float phase;

        private void OnEnable()
        {
            origin = transform.position;
            phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            float time = Time.time * bobSpeed + phase;
            transform.position = origin + Vector3.up * (Mathf.Sin(time) * bobHeight);
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            bool consumed = pickupType == CombatPickupType.Health
                ? TryRestoreHealth(other)
                : TryRestoreMana(other);
            if (consumed) Destroy(gameObject);
        }

        private bool TryRestoreHealth(Component playerPart)
        {
            Health health = playerPart.GetComponentInParent<Health>();
            if (health == null || !health.IsAlive || health.CurrentHealth >= health.MaxHealth) return false;
            health.Heal(amount);
            return true;
        }

        private bool TryRestoreMana(Component playerPart)
        {
            PowerUp powerUp = playerPart.GetComponentInParent<PowerUp>();
            return powerUp != null && powerUp.TryAddMana(amount);
        }
    }
}
