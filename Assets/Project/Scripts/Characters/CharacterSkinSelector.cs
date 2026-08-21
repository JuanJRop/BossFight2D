using UnityEngine;

namespace Project.Scripts.Characters
{
    public class CharacterSkinSelector : MonoBehaviour
    {
        private const string SelectedSkinKey = "SelectedCharacterSkin";

        [SerializeField] private CharacterSkinData[] skins;
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Animator targetAnimator;

        public int SelectedIndex { get; private set; }

        private void Awake()
        {
            int skinCount = skins != null ? skins.Length : 0;
            SelectedIndex = Mathf.Clamp(PlayerPrefs.GetInt(SelectedSkinKey, 0), 0, Mathf.Max(0, skinCount - 1));
            ApplySelectedSkin();
        }

        public void SelectSkin(int index)
        {
            if (skins == null || skins.Length == 0) return;

            SelectedIndex = Mathf.Clamp(index, 0, skins.Length - 1);
            PlayerPrefs.SetInt(SelectedSkinKey, SelectedIndex);
            PlayerPrefs.Save();
            ApplySelectedSkin();
        }

        private void ApplySelectedSkin()
        {
            if (skins == null || skins.Length == 0) return;

            CharacterSkinData skin = skins[SelectedIndex];
            if (skin == null) return;

            if (targetRenderer != null && skin.DefaultSprite != null)
                targetRenderer.sprite = skin.DefaultSprite;

            if (targetAnimator != null && skin.AnimatorController != null)
                targetAnimator.runtimeAnimatorController = skin.AnimatorController;
        }
    }
}
