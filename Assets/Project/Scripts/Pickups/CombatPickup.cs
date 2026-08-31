using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Scripts.Controller;
using Project.Scripts.Progression;
using UnityEngine;

namespace Project.Scripts.Pickups
{
    public enum CombatPickupType
    {
        Health,
        Mana,
        Experience
    }

    public sealed class CombatPickup : MonoBehaviour
    {
        [SerializeField] private CombatPickupType pickupType;
        [SerializeField, Min(0f)] private float amount = 50f;

        [Header("Floating Presentation")]
        [SerializeField, Min(0f)] private float bobHeight = 0.18f;
        [SerializeField, Min(0.1f)] private float bobSpeed = 2.6f;
        [SerializeField, Min(0f)] private float shadowOffset = 0.24f;
        [SerializeField, Min(0.1f)] private float promptHeight = 0.82f;

        private readonly HashSet<Collider2D> nearbyPlayerColliders = new();
        private static Texture2D solidTexture;
        private static Sprite solidSprite;

        private Vector3 origin;
        private float phase;
        private GameObject interactionPrompt;
        private Transform shadowTransform;
        private SpriteRenderer shadowRenderer;

        public static CombatPickup CreateRuntime(Vector2 position, CombatPickupType type,
            float pickupAmount, Transform parent = null)
        {
            GameObject pickupObject = new($"{type} Drop");
            if (parent != null) pickupObject.transform.SetParent(parent, false);
            pickupObject.transform.position = new Vector3(position.x, position.y, -0.25f);
            pickupObject.transform.localScale = Vector3.one * 0.46f;

            SpriteRenderer renderer = pickupObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSolidSprite();
            renderer.color = GetPickupColor(type);
            renderer.sortingOrder = 24;

            CircleCollider2D collider = pickupObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.62f;

            CombatPickup pickup = pickupObject.AddComponent<CombatPickup>();
            pickup.pickupType = type;
            pickup.amount = Mathf.Max(1f, pickupAmount);
            pickup.BuildRuntimeGlyph(type);
            return pickup;
        }

        private void Awake()
        {
            BuildShadow();
            BuildInteractionPrompt();
        }

        private void OnEnable()
        {
            origin = transform.position;
            phase = Random.Range(0f, Mathf.PI * 2f);
            transform.rotation = Quaternion.identity;
            nearbyPlayerColliders.Clear();
            SetPromptVisible(false);
            UpdatePresentation(0f);
        }

        private void OnDisable()
        {
            nearbyPlayerColliders.Clear();
        }

        private void Update()
        {
            float wave = (Mathf.Sin(Time.time * bobSpeed + phase) + 1f) * 0.5f;
            UpdatePresentation(wave);

            nearbyPlayerColliders.RemoveWhere(collider => collider == null || !collider.gameObject.activeInHierarchy);
            bool canInteract = nearbyPlayerColliders.Count > 0;
            SetPromptVisible(canInteract);

            if (!canInteract || !Input.GetKeyDown(KeyCode.E)) return;
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;

            Collider2D playerPart = GetClosestPlayerCollider();
            if (playerPart == null) return;

            bool consumed = pickupType switch
            {
                CombatPickupType.Health => TryRestoreHealth(playerPart),
                CombatPickupType.Mana => TryRestoreMana(playerPart),
                CombatPickupType.Experience => TryGrantExperience(),
                _ => false
            };
            if (consumed) Destroy(gameObject);
        }

        private bool TryGrantExperience()
        {
            RunSession.AwardExperience(Mathf.RoundToInt(amount));
            return true;
        }

        private void UpdatePresentation(float normalizedLift)
        {
            float lift = Mathf.Clamp01(normalizedLift) * bobHeight;
            transform.SetPositionAndRotation(origin + Vector3.up * lift, Quaternion.identity);

            if (shadowTransform != null)
            {
                shadowTransform.position = origin + Vector3.down * shadowOffset;
                float width = Mathf.Lerp(1.08f, 0.82f, normalizedLift);
                shadowTransform.localScale = new Vector3(width, 0.27f, 1f);
            }

            if (shadowRenderer != null)
            {
                Color shadowColor = shadowRenderer.color;
                shadowColor.a = Mathf.Lerp(0.38f, 0.2f, normalizedLift);
                shadowRenderer.color = shadowColor;
            }

            if (interactionPrompt != null)
            {
                interactionPrompt.transform.position = transform.position + Vector3.up * promptHeight;
                CounterParentScale(interactionPrompt.transform);
            }
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

        private void BuildShadow()
        {
            SpriteRenderer sourceRenderer = GetComponent<SpriteRenderer>();
            if (sourceRenderer == null || sourceRenderer.sprite == null) return;

            GameObject shadow = new("Pickup Shadow");
            shadow.transform.SetParent(transform, false);
            shadowTransform = shadow.transform;
            shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = sourceRenderer.sprite;
            shadowRenderer.material = sourceRenderer.sharedMaterial;
            shadowRenderer.color = new Color(0f, 0f, 0f, 0.34f);
            shadowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            shadowRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        }

        private void BuildInteractionPrompt()
        {
            interactionPrompt = new GameObject("Keyboard E Prompt");
            interactionPrompt.transform.SetParent(transform, false);
            CounterParentScale(interactionPrompt.transform);

            CreateKeyLayer(
                "Key Shadow",
                new Vector3(0f, -0.045f, 0f),
                new Vector3(0.46f, 0.36f, 1f),
                new Color(0.025f, 0.03f, 0.045f, 0.95f),
                40);
            CreateKeyLayer(
                "Key Border",
                Vector3.zero,
                new Vector3(0.44f, 0.34f, 1f),
                new Color(0.86f, 0.9f, 0.96f, 1f),
                41);
            CreateKeyLayer(
                "Key Face",
                new Vector3(0f, 0.012f, 0f),
                new Vector3(0.36f, 0.26f, 1f),
                new Color(0.08f, 0.1f, 0.14f, 1f),
                42);

            GameObject textObject = new("E");
            textObject.transform.SetParent(interactionPrompt.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.015f, 0f);

            TextMesh promptText = textObject.AddComponent<TextMesh>();
            promptText.text = "E";
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.fontSize = 48;
            promptText.characterSize = 0.05f;
            promptText.color = Color.white;

            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            if (textRenderer != null) textRenderer.sortingOrder = 43;
            interactionPrompt.SetActive(false);
        }

        private void CreateKeyLayer(
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            int sortingOrder)
        {
            GameObject layer = new(objectName);
            layer.transform.SetParent(interactionPrompt.transform, false);
            layer.transform.localPosition = localPosition;
            layer.transform.localScale = localScale;

            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSolidSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static Sprite GetSolidSprite()
        {
            if (solidSprite != null) return solidSprite;

            solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Pickup Key Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            solidTexture.SetPixel(0, 0, Color.white);
            solidTexture.Apply();

            solidSprite = Sprite.Create(
                solidTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            solidSprite.name = "Runtime Pickup Key Sprite";
            solidSprite.hideFlags = HideFlags.HideAndDontSave;
            return solidSprite;
        }

        private void CounterParentScale(Transform target)
        {
            if (target == null) return;
            Vector3 parentScale = transform.lossyScale;
            target.localScale = new Vector3(
                1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
                1f);
            target.rotation = Quaternion.identity;
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

        private void BuildRuntimeGlyph(CombatPickupType type)
        {
            GameObject glyphObject = new("Reward Glyph");
            glyphObject.transform.SetParent(transform, false);
            glyphObject.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            glyphObject.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            glyphObject.transform.localScale = Vector3.one * 0.62f;

            SpriteRenderer glyph = glyphObject.AddComponent<SpriteRenderer>();
            glyph.sprite = GetSolidSprite();
            glyph.color = Color.Lerp(GetPickupColor(type), Color.white, 0.28f);
            glyph.sortingOrder = 25;

            GameObject labelObject = new("Reward Label");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = type switch
            {
                CombatPickupType.Health => "HP",
                CombatPickupType.Mana => "MP",
                _ => "XP"
            };
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 24;
            label.characterSize = 0.045f;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.08f, 0.04f, 0.03f, 1f);
            MeshRenderer labelRenderer = labelObject.GetComponent<MeshRenderer>();
            if (labelRenderer != null) labelRenderer.sortingOrder = 26;
        }

        private static Color GetPickupColor(CombatPickupType type)
        {
            return type switch
            {
                CombatPickupType.Health => new Color(1f, 0.25f, 0.3f, 1f),
                CombatPickupType.Mana => new Color(0.18f, 0.64f, 1f, 1f),
                _ => new Color(0.35f, 1f, 0.48f, 1f)
            };
        }

        private void OnValidate()
        {
            bobHeight = Mathf.Max(0f, bobHeight);
            bobSpeed = Mathf.Max(0.1f, bobSpeed);
            shadowOffset = Mathf.Max(0f, shadowOffset);
            promptHeight = Mathf.Max(0.1f, promptHeight);
        }
    }
}
