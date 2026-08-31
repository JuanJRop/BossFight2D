using System;
using Project.Scripts.Controller;
using Project.Scripts.Progression;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Combat
{
    public class PowerUp : MonoBehaviour
    {
        [Header("Charge")]
        [SerializeField, Min(1f)] private float maxMana = 100f;
        [SerializeField, Min(0.1f)] private float manaPerEnemyHit = 4f;
        [SerializeField] private KeyCode activationKey = KeyCode.Q;

        [Header("Ready Presentation")]
        [SerializeField] private Color readyColor = new(0.1f, 0.95f, 1f, 1f);
        [SerializeField] private Color activeColor = new(1f, 0.18f, 0.55f, 1f);
        [SerializeField] private Vector2 indicatorOffset = new(0f, 1.25f);
        [SerializeField, Min(0.1f)] private float indicatorScale = 0.58f;

        private float currentMana;
        private bool isActive;
        private bool wasFullyCharged;
        private float readyBurst;
        private Transform indicatorRoot;
        private Transform centerShard;
        private Transform leftShard;
        private Transform rightShard;
        private SpriteRenderer centerRenderer;
        private SpriteRenderer leftRenderer;
        private SpriteRenderer rightRenderer;
        private Texture2D shardTexture;
        private Sprite shardSprite;

        public event Action<bool> OnPowerUpStateChanged;
        public event Action<float> OnManaChanged;

        public bool IsActive => isActive;
        public bool IsFullyCharged => currentMana >= maxMana;
        public float CurrentMana => currentMana;
        public float MaxMana => maxMana;

        private void Awake()
        {
            maxMana = Mathf.Max(1f, maxMana);
            BuildReadyIndicator();
        }

        private void Start()
        {
            currentMana = 0f;
            SetActive(false);
            NotifyManaChanged();
        }

        private void Update()
        {
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;
            if (Input.GetKeyDown(activationKey) && IsFullyCharged && !isActive) SetActive(true);
            UpdateReadyPresentation();
        }

        private void OnDestroy()
        {
            if (shardSprite != null) Destroy(shardSprite);
            if (shardTexture != null) Destroy(shardTexture);
        }

        public void RegisterEnemyHit(float dealtDamage)
        {
            if (dealtDamage <= 0f || isActive || IsFullyCharged) return;
            currentMana = Mathf.Min(maxMana, currentMana + Mathf.Max(0.1f, manaPerEnemyHit));
            NotifyManaChanged();
        }

        public bool ConsumeCharge()
        {
            if (!isActive) return false;
            currentMana = 0f;
            SetActive(false);
            NotifyManaChanged();
            return true;
        }

        public void RestoreMana(float value)
        {
            SetActive(false);
            currentMana = Mathf.Clamp(value, 0f, maxMana);
            NotifyManaChanged();
        }

        public bool TryAddMana(float amount)
        {
            if (amount <= 0f || isActive || currentMana >= maxMana) return false;
            currentMana = Mathf.Min(maxMana, currentMana + amount);
            NotifyManaChanged();
            return true;
        }

        public float GetManaNormalized()
        {
            return maxMana > 0f ? currentMana / maxMana : 0f;
        }

        private void SetActive(bool active)
        {
            if (isActive == active)
            {
                RefreshIndicatorVisibility();
                return;
            }

            isActive = active;
            if (active) readyBurst = 1f;
            RefreshIndicatorVisibility();
            OnPowerUpStateChanged?.Invoke(isActive);
        }

        private void BuildReadyIndicator()
        {
            GameObject root = new("Power Up Ready Shards");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = indicatorOffset;
            indicatorRoot = root.transform;

            shardTexture = BuildShardTexture();
            shardSprite = Sprite.Create(shardTexture, new Rect(0f, 0f, shardTexture.width, shardTexture.height),
                new Vector2(0.5f, 0.5f), 12f, 0, SpriteMeshType.FullRect);
            shardSprite.name = "Power Up Crystal Shard";

            centerShard = CreateShard("Ready core", Vector2.zero, 42, out centerRenderer);
            leftShard = CreateShard("Left ready shard", new Vector2(-0.48f, -0.08f), 41, out leftRenderer);
            rightShard = CreateShard("Right ready shard", new Vector2(0.48f, -0.08f), 41, out rightRenderer);
            leftShard.localRotation = Quaternion.Euler(0f, 0f, 18f);
            rightShard.localRotation = Quaternion.Euler(0f, 0f, -18f);
            root.SetActive(false);
        }

        private Transform CreateShard(string objectName, Vector2 position, int sortingOrder,
            out SpriteRenderer renderer)
        {
            GameObject shard = new(objectName);
            shard.transform.SetParent(indicatorRoot, false);
            shard.transform.localPosition = position;
            renderer = shard.AddComponent<SpriteRenderer>();
            renderer.sprite = shardSprite;
            renderer.sortingOrder = sortingOrder;
            return shard.transform;
        }

        private static Texture2D BuildShardTexture()
        {
            const int width = 9;
            const int height = 13;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                name = "Power Up Crystal Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color clear = Color.clear;
            Color edge = new(0.08f, 0.38f, 0.62f, 1f);
            Color fill = new(0.18f, 0.92f, 1f, 1f);
            Color shine = Color.white;
            for (int y = 0; y < height; y++)
            {
                int distanceFromTip = Mathf.Min(y, height - 1 - y);
                int radius = Mathf.Min(3, Mathf.CeilToInt(distanceFromTip * 0.65f));
                for (int x = 0; x < width; x++)
                {
                    int dx = Mathf.Abs(x - width / 2);
                    Color pixel = clear;
                    if (dx <= radius) pixel = dx == radius ? edge : fill;
                    if (dx == 0 && y >= 3 && y <= 8) pixel = shine;
                    texture.SetPixel(x, y, pixel);
                }
            }
            texture.Apply(false, false);
            return texture;
        }

        private void UpdateReadyPresentation()
        {
            if (indicatorRoot == null || !indicatorRoot.gameObject.activeSelf) return;
            readyBurst = Mathf.MoveTowards(readyBurst, 0f, Time.unscaledDeltaTime * 2.4f);
            float time = Time.unscaledTime;
            float pulse = 1f + Mathf.Sin(time * (isActive ? 11f : 7f)) * (isActive ? 0.12f : 0.07f);
            float entrance = 1f + readyBurst * 0.32f;
            indicatorRoot.localPosition = indicatorOffset + Vector2.up * (Mathf.Sin(time * 3.4f) * 0.08f);
            indicatorRoot.localScale = Vector3.one * indicatorScale * pulse * entrance;

            centerShard.localPosition = Vector2.up * (0.06f + Mathf.Sin(time * 4.2f) * 0.05f);
            leftShard.localPosition = new Vector2(-0.48f - readyBurst * 0.12f,
                -0.08f + Mathf.Sin(time * 4.2f + 1.8f) * 0.04f);
            rightShard.localPosition = new Vector2(0.48f + readyBurst * 0.12f,
                -0.08f + Mathf.Sin(time * 4.2f + 3.6f) * 0.04f);

            Color color = isActive ? activeColor : Color.Lerp(readyColor, RunSession.BasicProjectileColor, 0.38f);
            centerRenderer.color = color;
            leftRenderer.color = Color.Lerp(color, Color.white, 0.18f);
            rightRenderer.color = Color.Lerp(color, Color.white, 0.18f);
        }

        private void RefreshIndicatorVisibility()
        {
            if (indicatorRoot != null) indicatorRoot.gameObject.SetActive(IsFullyCharged || isActive);
        }

        private void NotifyManaChanged()
        {
            bool fullyCharged = IsFullyCharged;
            if (fullyCharged && !wasFullyCharged) readyBurst = 1f;
            wasFullyCharged = fullyCharged;
            RefreshIndicatorVisibility();
            OnManaChanged?.Invoke(GetManaNormalized());
            UpdateReadyPresentation();
        }

        private void OnValidate()
        {
            maxMana = Mathf.Max(1f, maxMana);
            manaPerEnemyHit = Mathf.Max(0.1f, manaPerEnemyHit);
            indicatorScale = Mathf.Max(0.1f, indicatorScale);
        }
    }
}
