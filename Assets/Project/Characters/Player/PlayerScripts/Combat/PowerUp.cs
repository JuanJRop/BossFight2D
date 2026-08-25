using System;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Combat
{
    public class PowerUp : MonoBehaviour
    {
        [Header("Charge")]
        [SerializeField, Min(1f)] private float maxMana = 100f;
        [SerializeField, Min(0.1f)] private float manaPerEnemyHit = 4f;
        [SerializeField] private KeyCode activationKey = KeyCode.Q;

        [Header("Charged Core Presentation")]
        [SerializeField] private Color outerAuraColor = new(0.08f, 0.95f, 1f, 0.9f);
        [SerializeField] private Color innerAuraColor = new(1f, 0.12f, 0.52f, 0.9f);
        [SerializeField, Min(0.1f)] private float auraRadius = 0.95f;
        [SerializeField, Min(0f)] private float auraPulse = 0.18f;
        [SerializeField] private Vector2 coreOffset = new(0f, 1.1f);

        private const int AuraSegments = 48;

        private float currentMana;
        private bool isActive;
        private Transform auraRoot;
        private LineRenderer outerAura;
        private LineRenderer innerAura;
        private LineRenderer chargeRing;
        private Transform chargeCore;
        private SpriteRenderer chargeCoreRenderer;
        private Material auraMaterial;
        private Texture2D coreTexture;
        private Sprite coreSprite;

        public event Action<bool> OnPowerUpStateChanged;
        public event Action<float> OnManaChanged;

        public bool IsActive => isActive;
        public bool IsFullyCharged => currentMana >= maxMana;
        public float CurrentMana => currentMana;
        public float MaxMana => maxMana;

        private void Awake()
        {
            maxMana = Mathf.Max(1f, maxMana);
            BuildAura();
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
            UpdateChargePresentation();
        }

        private void OnDestroy()
        {
            if (auraMaterial != null) Destroy(auraMaterial);
            if (coreSprite != null) Destroy(coreSprite);
            if (coreTexture != null) Destroy(coreTexture);
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
                if (auraRoot != null) auraRoot.gameObject.SetActive(active || currentMana > 0f);
                return;
            }

            isActive = active;
            if (auraRoot != null) auraRoot.gameObject.SetActive(isActive || currentMana > 0f);
            OnPowerUpStateChanged?.Invoke(isActive);
        }

        private void BuildAura()
        {
            GameObject root = new("Charged Shot Core");
            root.transform.SetParent(transform, false);
            auraRoot = root.transform;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            auraMaterial = shader != null ? new Material(shader) : null;

            outerAura = CreateAuraRing("Outer Charge Ring", 0.065f, outerAuraColor, 32, true);
            innerAura = CreateAuraRing("Inner Charge Ring", 0.045f, innerAuraColor, 33, true);
            chargeRing = CreateAuraRing("Charge Progress", 0.12f, Color.white, 34, false);
            BuildChargeCore();
            root.SetActive(false);
        }

        private LineRenderer CreateAuraRing(string objectName, float width, Color color, int sortingOrder, bool loop)
        {
            GameObject ringObject = new(objectName);
            ringObject.transform.SetParent(auraRoot, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = loop;
            ring.positionCount = loop ? AuraSegments : 2;
            ring.startWidth = width;
            ring.endWidth = width;
            ring.startColor = color;
            ring.endColor = color;
            ring.numCornerVertices = 2;
            ring.numCapVertices = 3;
            ring.sortingOrder = sortingOrder;
            ring.material = auraMaterial;
            if (loop) SetRingRadius(ring, auraRadius);
            return ring;
        }

        private void BuildChargeCore()
        {
            coreTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Charged Core Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            coreTexture.SetPixel(0, 0, Color.white);
            coreTexture.Apply();
            coreSprite = Sprite.Create(coreTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            coreSprite.hideFlags = HideFlags.HideAndDontSave;

            GameObject coreObject = new("Forming Powerful Bullet");
            coreObject.transform.SetParent(auraRoot, false);
            coreObject.transform.localPosition = coreOffset;
            chargeCore = coreObject.transform;
            chargeCoreRenderer = coreObject.AddComponent<SpriteRenderer>();
            chargeCoreRenderer.sprite = coreSprite;
            chargeCoreRenderer.sortingOrder = 36;
        }

        private void UpdateChargePresentation()
        {
            if (auraRoot == null) return;
            float progress = GetManaNormalized();
            bool visible = progress > 0f || isActive;
            if (auraRoot.gameObject.activeSelf != visible) auraRoot.gameObject.SetActive(visible);
            if (!visible) return;

            float pulse = (Mathf.Sin(Time.unscaledTime * (IsFullyCharged ? 13f : 6f)) + 1f) * 0.5f;
            float stagedRadius = Mathf.Lerp(auraRadius * 0.58f, auraRadius, progress);
            SetRingRadius(outerAura, stagedRadius + pulse * auraPulse * progress, 0.025f * progress);
            SetRingRadius(innerAura, stagedRadius * 0.64f + (1f - pulse) * auraPulse * 0.35f);
            SetProgressRing(chargeRing, progress, auraRadius * 1.13f);

            Color progressColor = Color.Lerp(outerAuraColor, GameLoadout.AbilityColor, progress);
            progressColor.a = Mathf.Lerp(0.3f, 1f, progress);
            chargeRing.startColor = progressColor;
            chargeRing.endColor = progressColor;

            outerAura.startColor = new Color(outerAuraColor.r, outerAuraColor.g, outerAuraColor.b,
                Mathf.Lerp(0.15f, outerAuraColor.a, progress));
            outerAura.endColor = outerAura.startColor;
            innerAura.startColor = new Color(innerAuraColor.r, innerAuraColor.g, innerAuraColor.b,
                Mathf.Lerp(0.08f, innerAuraColor.a, progress));
            innerAura.endColor = innerAura.startColor;

            if (chargeCore != null)
            {
                chargeCore.localPosition = coreOffset + Vector2.up * (Mathf.Sin(Time.unscaledTime * 4f) * 0.08f);
                float coreScale = Mathf.Lerp(0.08f, 0.42f, progress);
                if (IsFullyCharged) coreScale *= Mathf.Lerp(0.92f, 1.18f, pulse);
                chargeCore.localScale = Vector3.one * coreScale;
                chargeCore.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 120f);
            }

            if (chargeCoreRenderer != null)
            {
                chargeCoreRenderer.color = Color.Lerp(outerAuraColor, GameLoadout.AbilityColor, progress);
            }

            auraRoot.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 32f * progress);
        }

        private static void SetRingRadius(LineRenderer ring, float radius, float distortion = 0f)
        {
            if (ring == null) return;
            ring.loop = true;
            ring.positionCount = AuraSegments;
            for (int index = 0; index < AuraSegments; index++)
            {
                float angle = index / (float)AuraSegments * Mathf.PI * 2f;
                float shapedRadius = radius * (1f + Mathf.Sin(angle * 8f) * distortion);
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * shapedRadius);
            }
        }

        private static void SetProgressRing(LineRenderer ring, float progress, float radius)
        {
            if (ring == null) return;
            int points = Mathf.Max(2, Mathf.CeilToInt((AuraSegments - 1) * Mathf.Clamp01(progress)) + 1);
            ring.loop = progress >= 0.999f;
            ring.positionCount = points;
            for (int index = 0; index < points; index++)
            {
                float normalized = points <= 1 ? 0f : index / (float)(AuraSegments - 1);
                float angle = normalized * Mathf.PI * 2f + Mathf.PI * 0.5f;
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private void NotifyManaChanged()
        {
            OnManaChanged?.Invoke(GetManaNormalized());
            UpdateChargePresentation();
        }

        private void OnValidate()
        {
            maxMana = Mathf.Max(1f, maxMana);
            manaPerEnemyHit = Mathf.Max(0.1f, manaPerEnemyHit);
            auraRadius = Mathf.Max(0.1f, auraRadius);
            auraPulse = Mathf.Max(0f, auraPulse);
        }
    }
}
