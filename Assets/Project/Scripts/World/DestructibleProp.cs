using System;
using System.Collections;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Scripts.Controller;
using Project.Scripts.Pickups;
using UnityEngine;

namespace Project.Scripts.World
{
    public enum DestructiblePropType
    {
        Crate,
        Boulder
    }

    public enum DestructibleRewardType
    {
        Health,
        Mana,
        Experience
    }

    public sealed class DestructibleProp : MonoBehaviour
    {
        private static readonly Color PixelShade = new(0.52f, 0.52f, 0.52f, 1f);

        private static Sprite crateSprite;
        private static Sprite boulderSprite;
        private static Texture2D crateTexture;
        private static Texture2D boulderTexture;

        private Health health;
        private Collider2D propCollider;
        private SpriteRenderer propRenderer;
        private Vector3 baseScale;
        private Color baseColor;
        private Coroutine hitRoutine;
        private bool breaking;
        private DestructibleRewardType rewardType;
        private float rewardAmount;
        private Action<GameObject> rewardCreated;

        public bool IsBroken => breaking;

        public static DestructibleProp CreateRuntime(string objectName, Vector2 position, Vector2 size,
            Color color, DestructiblePropType type, float maximumHealth, Transform parent = null,
            float rewardMana = 8f)
        {
            return CreateRuntime(objectName, position, size, color, type, maximumHealth, parent,
                DestructibleRewardType.Mana, rewardMana);
        }

        public static DestructibleProp CreateRuntime(string objectName, Vector2 position, Vector2 size,
            Color color, DestructiblePropType type, float maximumHealth, Transform parent,
            DestructibleRewardType reward, float amount, Action<GameObject> rewardCallback = null)
        {
            GameObject prop = new(objectName);
            if (parent != null) prop.transform.SetParent(parent, false);
            prop.transform.position = position;
            prop.transform.localScale = size;

            SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPropSprite(type);
            renderer.color = color;
            renderer.sortingOrder = 8;

            BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            Health propHealth = prop.AddComponent<Health>();
            propHealth.ConfigureRuntime(maximumHealth);

            DestructibleProp behaviour = prop.AddComponent<DestructibleProp>();
            behaviour.Configure(propHealth, collider, renderer, reward, amount, rewardCallback);
            return behaviour;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            propCollider = GetComponent<Collider2D>();
            propRenderer = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
            baseColor = propRenderer != null ? propRenderer.color : Color.white;
        }

        private void OnDestroy()
        {
            if (health == null) return;
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }

        private void Configure(Health source, Collider2D collider, SpriteRenderer renderer,
            DestructibleRewardType reward, float amount, Action<GameObject> rewardCallback)
        {
            health = source;
            propCollider = collider;
            propRenderer = renderer;
            baseScale = transform.localScale;
            baseColor = propRenderer != null ? propRenderer.color : Color.white;
            rewardType = reward;
            rewardAmount = Mathf.Max(0f, amount);
            rewardCreated = rewardCallback;

            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;

            FloatingHealthBar healthBar = gameObject.AddComponent<FloatingHealthBar>();
            healthBar.ConfigureRuntime(health, new Vector2(1.05f, 0.11f), new Vector2(0f, 0.72f),
                new Color(1f, 0.48f, 0.1f, 1f), true);
        }

        private void HandleDamaged(float damage)
        {
            if (breaking || !isActiveAndEnabled) return;
            if (hitRoutine != null) StopCoroutine(hitRoutine);
            hitRoutine = StartCoroutine(HitPulse());
        }

        private IEnumerator HitPulse()
        {
            float elapsed = 0f;
            const float duration = 0.12f;
            while (elapsed < duration && !breaking)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float pulse = 1f + Mathf.Sin(progress * Mathf.PI) * 0.12f;
                transform.localScale = baseScale * pulse;
                yield return null;
            }

            if (!breaking) transform.localScale = baseScale;
            hitRoutine = null;
        }

        private void HandleDied()
        {
            if (breaking) return;
            breaking = true;
            if (propCollider != null) propCollider.enabled = false;
            if (health != null) health.SetExternalInvulnerable(true);
            SpawnReward();
            StartCoroutine(BreakRoutine());
        }

        private IEnumerator BreakRoutine()
        {
            const float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                transform.localScale = baseScale * Mathf.Lerp(1f, 1.22f, eased) * (1f - eased * 0.88f);
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 18f, eased));
                if (propRenderer != null)
                {
                    Color color = baseColor;
                    color.a = 1f - eased;
                    propRenderer.color = color;
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        private void SpawnReward()
        {
            if (rewardAmount <= 0f) return;

            CombatPickupType pickupType = rewardType switch
            {
                DestructibleRewardType.Health => CombatPickupType.Health,
                DestructibleRewardType.Experience => CombatPickupType.Experience,
                _ => CombatPickupType.Mana
            };
            CombatPickup pickup = CombatPickup.CreateRuntime(transform.position, pickupType,
                rewardAmount, transform.parent);
            rewardCreated?.Invoke(pickup != null ? pickup.gameObject : null);
        }

        private static Sprite GetPropSprite(DestructiblePropType type)
        {
            if (type == DestructiblePropType.Boulder)
            {
                if (boulderSprite == null) boulderSprite = BuildBoulderSprite();
                return boulderSprite;
            }

            if (crateSprite == null) crateSprite = BuildCrateSprite();
            return crateSprite;
        }

        private static Sprite BuildCrateSprite()
        {
            const int size = 16;
            crateTexture = CreateTexture("Runtime Destructible Crate Texture", size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool border = x <= 1 || x >= size - 2 || y <= 1 || y >= size - 2;
                    bool brace = x == y || x == size - 1 - y;
                    crateTexture.SetPixel(x, y, border || brace ? PixelShade : Color.white);
                }
            }
            return CreateSprite(crateTexture, "Runtime Destructible Crate Sprite");
        }

        private static Sprite BuildBoulderSprite()
        {
            const int size = 18;
            boulderTexture = CreateTexture("Runtime Destructible Boulder Texture", size, size);
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance > size * 0.46f) continue;
                    bool edge = distance > size * 0.37f || (x + y) % 7 == 0;
                    boulderTexture.SetPixel(x, y, edge ? PixelShade : Color.white);
                }
            }
            return CreateSprite(boulderTexture, "Runtime Destructible Boulder Sprite");
        }

        private static Texture2D CreateTexture(string textureName, int width, int height)
        {
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) texture.SetPixel(x, y, Color.clear);
            }
            return texture;
        }

        private static Sprite CreateSprite(Texture2D texture, string spriteName)
        {
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), texture.width);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
