using System.Collections.Generic;
using UnityEngine;

namespace Project.Scripts.Controller
{
    public sealed class PlayerSkinController : MonoBehaviour
    {
        private const string CatalogPath = "Menu/MainCharacterSkins";

        private Animator animator;
        private SpriteRenderer playerRenderer;
        private GameObject catalogInstance;
        private CharacterSkinSet activeSkin;
        private readonly Dictionary<int, Sprite> normalizedSprites = new Dictionary<int, Sprite>();
        private float referenceWorldHeight;

        public static void Attach(GameObject player, Animator playerAnimator, SpriteRenderer renderer)
        {
            if (player == null || playerAnimator == null || renderer == null) return;
            PlayerSkinController controller = player.GetComponent<PlayerSkinController>();
            if (controller == null) controller = player.AddComponent<PlayerSkinController>();
            controller.Initialize(playerAnimator, renderer);
        }

        private void Initialize(Animator playerAnimator, SpriteRenderer renderer)
        {
            animator = playerAnimator;
            playerRenderer = renderer;
            if (playerRenderer.sprite != null)
            {
                referenceWorldHeight = playerRenderer.sprite.bounds.size.y;
            }
            LoadSelectedSkin();
        }

        private void LateUpdate()
        {
            if (animator == null || playerRenderer == null || activeSkin == null) return;
            Sprite current = playerRenderer.sprite;
            if (current == null) return;

            Sprite replacement = activeSkin.Resolve(current.name);
            if (replacement != null && replacement != current)
            {
                playerRenderer.sprite = GetNormalizedSprite(replacement);
            }
            playerRenderer.color = Color.white;
        }

        private Sprite GetNormalizedSprite(Sprite source)
        {
            if (source == null || referenceWorldHeight <= 0.001f) return source;
            int key = source.GetInstanceID();
            if (normalizedSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            source.texture.filterMode = FilterMode.Point;
            source.texture.wrapMode = TextureWrapMode.Clamp;
            source.texture.anisoLevel = 0;

            Rect rect = source.rect;
            Vector2 pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);
            float pixelsPerUnit = Mathf.Max(1f, rect.height / referenceWorldHeight);
            Sprite normalized = Sprite.Create(
                source.texture,
                rect,
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                source.border);
            normalized.name = source.name;
            normalizedSprites[key] = normalized;
            return normalized;
        }

        private void LoadSelectedSkin()
        {
            if (catalogInstance != null) Destroy(catalogInstance);
            GameObject catalogPrefab = Resources.Load<GameObject>(CatalogPath);
            if (catalogPrefab == null)
            {
                Debug.LogWarning("MainCharacterSkins prefab was not found in Resources.", this);
                return;
            }

            catalogInstance = Instantiate(catalogPrefab);
            catalogInstance.name = "Runtime Character Skin Catalog";
            catalogInstance.hideFlags = HideFlags.HideInHierarchy;
            catalogInstance.SetActive(false);

            CharacterSkinSet[] skins = catalogInstance.GetComponentsInChildren<CharacterSkinSet>(true);
            if (skins.Length == 0) return;
            int index = Mathf.Clamp((int)GameLoadout.Character, 0, skins.Length - 1);
            activeSkin = skins[index];
            activeSkin.PrepareForCrispRendering();
        }

        private void OnDestroy()
        {
            foreach (Sprite sprite in normalizedSprites.Values)
            {
                if (sprite != null) Destroy(sprite);
            }
            normalizedSprites.Clear();
            if (catalogInstance != null) Destroy(catalogInstance);
        }
    }
}
