using System;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Combat
{
    public class PowerUp : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private float maxMana = 5f;
        [SerializeField] private float drainSpeed = 1f;
        [SerializeField] private float regenValue = 0.5f;
        [SerializeField] private float regenTime = 0.1f;
        [SerializeField] private KeyCode activationKey = KeyCode.Q;

        [Header("Overdrive Presentation")]
        [SerializeField] private Color outerAuraColor = new(0.08f, 0.95f, 1f, 0.9f);
        [SerializeField] private Color innerAuraColor = new(1f, 0.12f, 0.52f, 0.9f);
        [SerializeField, Min(0.1f)] private float auraRadius = 0.95f;
        [SerializeField, Min(0f)] private float auraPulse = 0.18f;

        private const int AuraSegments = 48;

        private float currentMana;
        private float regenTimer;
        private bool isActive;
        private Transform auraRoot;
        private LineRenderer outerAura;
        private LineRenderer innerAura;
        private Material auraMaterial;

        public event Action<bool> OnPowerUpStateChanged;
        public event Action<float> OnManaChanged;

        public bool IsActive => isActive;
        public float CurrentMana => currentMana;
        public float MaxMana => maxMana;

        private void Awake()
        {
            BuildAura();
        }

        private void Start()
        {
            maxMana = Mathf.Max(0.01f, maxMana);
            currentMana = maxMana;
            SetActive(false);
            NotifyManaChanged();
        }

        private void Update()
        {
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;

            HandleInput();
            if (isActive)
            {
                ConsumeMana();
                AnimateAura();
            }
            else
            {
                RegenerateMana();
            }
        }

        private void OnDestroy()
        {
            if (auraMaterial != null) Destroy(auraMaterial);
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(activationKey) && currentMana >= maxMana && !isActive)
            {
                SetActive(true);
            }
        }

        private void ConsumeMana()
        {
            currentMana = Mathf.Max(0f, currentMana - Mathf.Max(0f, drainSpeed) * Time.deltaTime);
            NotifyManaChanged();

            if (currentMana <= 0f)
            {
                SetActive(false);
            }
        }

        private void RegenerateMana()
        {
            if (currentMana >= maxMana) return;

            regenTimer += Time.deltaTime;
            float interval = Mathf.Max(0.01f, regenTime);
            if (regenTimer < interval) return;

            regenTimer -= interval;
            currentMana = Mathf.Min(maxMana, currentMana + Mathf.Max(0f, regenValue));
            NotifyManaChanged();
        }

        private void SetActive(bool active)
        {
            if (isActive == active)
            {
                if (auraRoot != null) auraRoot.gameObject.SetActive(active);
                return;
            }

            isActive = active;
            regenTimer = 0f;
            if (auraRoot != null) auraRoot.gameObject.SetActive(isActive);
            OnPowerUpStateChanged?.Invoke(isActive);
        }

        private void BuildAura()
        {
            GameObject root = new("Overdrive Aura");
            root.transform.SetParent(transform, false);
            auraRoot = root.transform;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            auraMaterial = shader != null ? new Material(shader) : null;

            outerAura = CreateAuraRing("Outer Energy Ring", 0.09f, outerAuraColor, 32);
            innerAura = CreateAuraRing("Inner Energy Ring", 0.055f, innerAuraColor, 33);
            root.SetActive(false);
        }

        private LineRenderer CreateAuraRing(string objectName, float width, Color color, int sortingOrder)
        {
            GameObject ringObject = new(objectName);
            ringObject.transform.SetParent(auraRoot, false);

            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = AuraSegments;
            ring.startWidth = width;
            ring.endWidth = width;
            ring.startColor = color;
            ring.endColor = color;
            ring.numCornerVertices = 2;
            ring.sortingOrder = sortingOrder;
            ring.material = auraMaterial;
            SetRingRadius(ring, auraRadius);
            return ring;
        }

        private void AnimateAura()
        {
            if (auraRoot == null || outerAura == null || innerAura == null) return;

            float pulse = (Mathf.Sin(Time.time * 11f) + 1f) * 0.5f;
            float outerRadius = auraRadius + pulse * auraPulse;
            float innerRadius = auraRadius * 0.63f + (1f - pulse) * auraPulse * 0.6f;

            SetRingRadius(outerAura, outerRadius, 0.025f);
            SetRingRadius(innerAura, innerRadius, 0.12f);
            auraRoot.localRotation = Quaternion.Euler(0f, 0f, Time.time * 95f);
            innerAura.transform.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 210f);

            Color outer = outerAuraColor;
            outer.a *= Mathf.Lerp(0.55f, 1f, pulse);
            outerAura.startColor = outer;
            outerAura.endColor = outer;

            Color inner = innerAuraColor;
            inner.a *= Mathf.Lerp(1f, 0.5f, pulse);
            innerAura.startColor = inner;
            innerAura.endColor = inner;
        }

        private static void SetRingRadius(LineRenderer ring, float radius, float distortion = 0f)
        {
            if (ring == null) return;
            for (int index = 0; index < AuraSegments; index++)
            {
                float angle = index / (float)AuraSegments * Mathf.PI * 2f;
                float shapedRadius = radius * (1f + Mathf.Sin(angle * 8f) * distortion);
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * shapedRadius);
            }
        }

        private void NotifyManaChanged()
        {
            OnManaChanged?.Invoke(GetManaNormalized());
        }

        public void RestoreMana(float value)
        {
            SetActive(false);
            currentMana = Mathf.Clamp(value, 0f, maxMana);
            regenTimer = 0f;
            NotifyManaChanged();
        }

        public bool TryAddMana(float amount)
        {
            if (amount <= 0f || currentMana >= maxMana) return false;
            currentMana = Mathf.Min(maxMana, currentMana + amount);
            regenTimer = 0f;
            NotifyManaChanged();
            return true;
        }

        public float GetManaNormalized()
        {
            return maxMana > 0f ? currentMana / maxMana : 0f;
        }

        private void OnValidate()
        {
            maxMana = Mathf.Max(0.01f, maxMana);
            drainSpeed = Mathf.Max(0f, drainSpeed);
            regenValue = Mathf.Max(0f, regenValue);
            regenTime = Mathf.Max(0.01f, regenTime);
            auraRadius = Mathf.Max(0.1f, auraRadius);
            auraPulse = Mathf.Max(0f, auraPulse);
        }
    }
}
