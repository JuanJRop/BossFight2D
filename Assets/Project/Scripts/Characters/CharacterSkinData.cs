using UnityEngine;

namespace Project.Scripts.Characters
{
    [CreateAssetMenu(fileName = "CharacterSkin", menuName = "BossFight2D/Character Skin")]
    public class CharacterSkinData : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite portrait;
        [SerializeField] private Sprite defaultSprite;
        [SerializeField] private RuntimeAnimatorController animatorController;

        public string DisplayName => displayName;
        public Sprite Portrait => portrait;
        public Sprite DefaultSprite => defaultSprite;
        public RuntimeAnimatorController AnimatorController => animatorController;
    }
}
