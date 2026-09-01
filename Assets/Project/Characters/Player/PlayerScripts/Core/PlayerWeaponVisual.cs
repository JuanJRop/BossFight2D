using Project.Scripts.Controller;
using Project.Scripts.Progression;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Controller
{
    public sealed class PlayerWeaponVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer weaponRenderer;
        [SerializeField] private Sprite swordSprite;
        [SerializeField] private Sprite bowSprite;
        [SerializeField] private Sprite mageStaffSprite;
        [SerializeField] private Sprite healerStaffSprite;
        [SerializeField] private Vector3 swordScale = new(0.42f, 0.42f, 1f);
        [SerializeField] private Vector3 bowScale = new(0.48f, 0.48f, 1f);
        [SerializeField] private Vector3 staffScale = new(0.5f, 0.5f, 1f);

        private void Awake()
        {
            if (weaponRenderer == null) weaponRenderer = GetComponent<SpriteRenderer>();
            ApplyCurrentWeapon();
        }

        private void OnEnable()
        {
            ApplyCurrentWeapon();
        }

        public void ApplyCurrentWeapon()
        {
            if (weaponRenderer == null) return;

            PlayerWeapon weapon = GameLoadout.Weapon;
            weaponRenderer.sprite = GetSprite(weapon);
            weaponRenderer.color = GameLoadout.WeaponColor;
            transform.localScale = GetScale(weapon);
            transform.localPosition = GetLocalPosition(weapon);
            transform.localRotation = Quaternion.Euler(0f, 0f, GetLocalRotation(weapon));
        }

        private Sprite GetSprite(PlayerWeapon weapon)
        {
            switch (weapon)
            {
                case PlayerWeapon.Bow: return bowSprite != null ? bowSprite : swordSprite;
                case PlayerWeapon.MageStaff: return mageStaffSprite != null ? mageStaffSprite : swordSprite;
                case PlayerWeapon.HealerStaff: return healerStaffSprite != null ? healerStaffSprite : mageStaffSprite;
                default: return swordSprite != null ? swordSprite : weaponRenderer.sprite;
            }
        }

        private Vector3 GetScale(PlayerWeapon weapon)
        {
            switch (weapon)
            {
                case PlayerWeapon.Bow: return bowScale;
                case PlayerWeapon.MageStaff:
                case PlayerWeapon.HealerStaff:
                    return staffScale;
                default:
                    return swordScale;
            }
        }

        private static Vector3 GetLocalPosition(PlayerWeapon weapon)
        {
            switch (weapon)
            {
                case PlayerWeapon.Bow: return new Vector3(0.03f, 0f, 0f);
                case PlayerWeapon.MageStaff:
                case PlayerWeapon.HealerStaff:
                    return new Vector3(0.03f, -0.015f, 0f);
                default:
                    return new Vector3(0.05f, -0.015f, 0f);
            }
        }

        private static float GetLocalRotation(PlayerWeapon weapon)
        {
            return weapon == PlayerWeapon.Bow ? -8f : -24f;
        }

        public static void ApplyAll(Transform playerRoot)
        {
            if (playerRoot == null) return;
            PlayerWeaponVisual[] visuals = playerRoot.GetComponentsInChildren<PlayerWeaponVisual>(true);
            foreach (PlayerWeaponVisual visual in visuals)
            {
                if (visual != null) visual.ApplyCurrentWeapon();
            }

            if (RunSession.SelectedClass == RunClassType.None)
                RunSession.SelectClass(GameLoadout.StartingClass);
        }
    }
}
