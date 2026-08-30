using Project.Characters.Enemy.EnemyScripts.Core;
using UnityEngine;

namespace Project.Scripts.Controller
{
    public sealed class FloatingHealthBar : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Vector2 offset = new(0f, 0.85f);
        [SerializeField] private Vector2 size = new(1.25f, 0.14f);
        [SerializeField] private Color backgroundColor = new(0.08f, 0.012f, 0.01f, 0.92f);
        [SerializeField] private Color fillColor = new(1f, 0.24f, 0.08f, 1f);
        [SerializeField] private bool showOnlyWhenDamaged;
        [SerializeField, Min(0.1f)] private float visibleDuration = 1.4f;

        private Transform barRoot;
        private SpriteRenderer backgroundRenderer;
        private SpriteRenderer fillRenderer;
        private float visibleTimer;

        private static Texture2D solidTexture;
        private static Sprite solidSprite;

        private void Awake()
        {
            ResolveHealth();
            BuildBar();
        }

        private void OnEnable()
        {
            ResolveHealth();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public void ConfigureRuntime(Health source, Vector2 barSize, Vector2 worldOffset,
            Color color, bool onlyWhenDamaged = false)
        {
            Unsubscribe();
            health = source;
            size = barSize;
            offset = worldOffset;
            fillColor = color;
            showOnlyWhenDamaged = onlyWhenDamaged;
            visibleTimer = 0f;
            BuildBar();
            Subscribe();
            Refresh();
        }

        private void ResolveHealth()
        {
            if (health == null) health = GetComponent<Health>();
        }

        private void Subscribe()
        {
            if (health == null) return;
            health.OnHealthChanged += HandleHealthChanged;
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }

        private void Unsubscribe()
        {
            if (health == null) return;
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }

        private void LateUpdate()
        {
            if (barRoot == null) return;

            barRoot.position = transform.TransformPoint(offset);
            barRoot.rotation = Quaternion.identity;
            Vector3 lossyScale = transform.lossyScale;
            barRoot.localScale = new Vector3(
                1f / Mathf.Max(0.001f, Mathf.Abs(lossyScale.x)),
                1f / Mathf.Max(0.001f, Mathf.Abs(lossyScale.y)),
                1f);

            if (!showOnlyWhenDamaged || !barRoot.gameObject.activeSelf) return;
            visibleTimer -= Time.unscaledDeltaTime;
            if (visibleTimer <= 0f) barRoot.gameObject.SetActive(false);
        }

        private void HandleHealthChanged(float current, float maximum)
        {
            UpdateFill(maximum > 0f ? current / maximum : 0f);
        }

        private void HandleDamaged(float damage)
        {
            if (!showOnlyWhenDamaged) return;
            visibleTimer = Mathf.Max(0.1f, visibleDuration);
            if (barRoot != null) barRoot.gameObject.SetActive(true);
        }

        private void HandleDied()
        {
            if (barRoot != null) barRoot.gameObject.SetActive(false);
        }

        private void Refresh()
        {
            if (barRoot == null) return;
            float normalized = health != null ? health.NormalizedHealth : 1f;
            UpdateFill(normalized);
            bool visible = health != null && health.IsAlive &&
                           (!showOnlyWhenDamaged || visibleTimer > 0f);
            barRoot.gameObject.SetActive(visible);
        }

        private void UpdateFill(float normalized)
        {
            if (fillRenderer == null || backgroundRenderer == null) return;

            float width = Mathf.Max(0.1f, size.x);
            float height = Mathf.Max(0.035f, size.y);
            float amount = Mathf.Clamp01(normalized);
            backgroundRenderer.color = backgroundColor;
            backgroundRenderer.transform.localScale = new Vector3(width, height, 1f);
            fillRenderer.color = fillColor;
            fillRenderer.transform.localPosition = new Vector3(
                -width * 0.5f + width * amount * 0.5f, 0f, 0f);
            fillRenderer.transform.localScale = new Vector3(width * amount, height * 0.72f, 1f);
        }

        private void BuildBar()
        {
            if (barRoot == null)
            {
                GameObject root = new("Floating Health Bar");
                root.transform.SetParent(transform, false);
                barRoot = root.transform;

                GameObject background = new("Health Bar Background");
                background.transform.SetParent(barRoot, false);
                backgroundRenderer = background.AddComponent<SpriteRenderer>();
                backgroundRenderer.sprite = GetSolidSprite();
                backgroundRenderer.sortingOrder = 30;

                GameObject fill = new("Health Bar Fill");
                fill.transform.SetParent(barRoot, false);
                fillRenderer = fill.AddComponent<SpriteRenderer>();
                fillRenderer.sprite = GetSolidSprite();
                fillRenderer.sortingOrder = 31;
            }

            if (barRoot != null) barRoot.localPosition = Vector3.zero;
        }

        private static Sprite GetSolidSprite()
        {
            if (solidSprite != null) return solidSprite;

            solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Health Bar Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            solidTexture.SetPixel(0, 0, Color.white);
            solidTexture.Apply(false, false);

            solidSprite = Sprite.Create(solidTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            solidSprite.name = "Runtime Health Bar Sprite";
            solidSprite.hideFlags = HideFlags.HideAndDontSave;
            return solidSprite;
        }

        private void OnValidate()
        {
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.035f, size.y);
            visibleDuration = Mathf.Max(0.1f, visibleDuration);
        }
    }
}
