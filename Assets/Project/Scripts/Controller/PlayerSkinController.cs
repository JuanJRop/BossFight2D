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
            LoadSelectedSkin();
        }

        private void LateUpdate()
        {
            if (animator == null || playerRenderer == null || activeSkin == null) return;
            Sprite current = playerRenderer.sprite;
            if (current == null) return;

            Sprite replacement = activeSkin.Resolve(current.name);
            if (replacement != null && replacement != current) playerRenderer.sprite = replacement;
            playerRenderer.color = Color.white;
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
            if (catalogInstance != null) Destroy(catalogInstance);
        }
    }
}
