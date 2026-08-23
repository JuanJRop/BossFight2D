using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Scripts.Controller;
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

        private readonly HashSet<Collider2D> nearbyPlayerColliders = new();
        private Vector3 origin;
        private float phase;
        private GameObject interactionPrompt;

        private void Awake()
        {
            BuildInteractionPrompt();
        }

        private void OnEnable()
        {
            origin = transform.position;
            phase = Random.Range(0f, Mathf.PI * 2f);
            nearbyPlayerColliders.Clear();
            SetPromptVisible(false);
        }

        private void OnDisable()
        {
            nearbyPlayerColliders.Clear();
        }

        private void Update()
        {
            float time = Time.time * bobSpeed + phase;
            transform.position = origin + Vector3.up * (Mathf.Sin(time) * bobHeight);
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

            nearbyPlayerColliders.RemoveWhere(collider => collider == null || !collider.gameObject.activeInHierarchy);
            bool canInteract = nearbyPlayerColliders.Count > 0;
            SetPromptVisible(canInteract);

            if (!canInteract || !Input.GetKeyDown(KeyCode.E)) return;
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;

            Collider2D playerPart = GetClosestPlayerCollider();
            if (playerPart == null) return;

            bool consumed = pickupType == CombatPickupType.Health
                ? TryRestoreHealth(playerPart)
                : TryRestoreMana(playerPart);
            if (consumed) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsPlayerCollider(other)) nearbyPlayerColliders.Add(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (IsPlayerCollider(other)) nearbyPlayerColliders.Add(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            nearbyPlayerColliders.Remove(other);
        }

        private static bool IsPlayerCollider(Collider2D other)
        {
            if (other == null) return false;
            if (other.CompareTag("Player")) return true;
            Transform root = other.transform.root;
            return root != null && root.CompareTag("Player");
        }

        private Collider2D GetClosestPlayerCollider()
        {
            Collider2D closest = null;
            float closestDistance = float.PositiveInfinity;
            foreach (Collider2D playerCollider in nearbyPlayerColliders)
            {
                if (playerCollider == null) continue;
                float distance = (playerCollider.transform.position - transform.position).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closest = playerCollider;
                closestDistance = distance;
            }

            return closest;
        }

        private void BuildInteractionPrompt()
        {
            interactionPrompt = new GameObject("Press E Prompt");
            interactionPrompt.transform.SetParent(transform, false);
            interactionPrompt.transform.localPosition = Vector3.up * 0.9f;

            TextMesh promptText = interactionPrompt.AddComponent<TextMesh>();
            promptText.text = "E";
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.fontSize = 64;
            promptText.characterSize = 0.075f;
            promptText.color = pickupType == CombatPickupType.Health
                ? new Color(0.45f, 1f, 0.48f, 1f)
                : new Color(0.15f, 0.9f, 1f, 1f);

            MeshRenderer renderer = interactionPrompt.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 40;
            interactionPrompt.SetActive(false);
        }

        private void SetPromptVisible(bool visible)
        {
            if (interactionPrompt != null && interactionPrompt.activeSelf != visible)
                interactionPrompt.SetActive(visible);
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
