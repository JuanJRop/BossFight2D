using UnityEngine;

namespace Project.Scripts.Controller
{
    public enum PlayerCharacter
    {
        Alex,
        Lyria,
        Manu,
        Tori
    }

    public enum PlayerWeapon
    {
        ArcRifle,
        PulseSmg,
        RailCannon
    }

    public enum PlayerAbility
    {
        ChargedRound,
        PrismBurst,
        SeekerCore
    }

    public static class GameLoadout
    {
        private const string CharacterKey = "loadout.character";
        private const string WeaponKey = "loadout.weapon";
        private const string AbilityKey = "loadout.ability";
        private const string LanguageKey = "settings.language";
        private const string VolumeKey = "settings.volume";
        private const string BrightnessKey = "settings.brightness";

        public static PlayerCharacter Character
        {
            get => (PlayerCharacter)Mathf.Clamp(PlayerPrefs.GetInt(CharacterKey, 0), 0, 3);
            set => Save(CharacterKey, (int)value);
        }

        public static PlayerWeapon Weapon
        {
            get => (PlayerWeapon)Mathf.Clamp(PlayerPrefs.GetInt(WeaponKey, 0), 0, 2);
            set => Save(WeaponKey, (int)value);
        }

        public static PlayerAbility Ability
        {
            get => (PlayerAbility)Mathf.Clamp(PlayerPrefs.GetInt(AbilityKey, 0), 0, 2);
            set => Save(AbilityKey, (int)value);
        }

        public static bool IsSpanish
        {
            get => PlayerPrefs.GetInt(LanguageKey, 0) == 0;
            set => Save(LanguageKey, value ? 0 : 1);
        }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            set
            {
                float clamped = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, clamped);
                PlayerPrefs.Save();
                AudioListener.volume = clamped;
            }
        }

        public static float Brightness
        {
            get => PlayerPrefs.GetFloat(BrightnessKey, 0.82f);
            set
            {
                PlayerPrefs.SetFloat(BrightnessKey, Mathf.Clamp(value, 0.35f, 1f));
                PlayerPrefs.Save();
            }
        }

        public static float HealthMultiplier => Character switch
        {
            PlayerCharacter.Lyria => 0.88f,
            PlayerCharacter.Manu => 1.25f,
            PlayerCharacter.Tori => 1.05f,
            _ => 1f
        };

        public static float MoveSpeedMultiplier => Character switch
        {
            PlayerCharacter.Lyria => 1.15f,
            PlayerCharacter.Manu => 0.9f,
            PlayerCharacter.Tori => 1.04f,
            _ => 1f
        };

        public static Color CharacterColor => Color.white;

        public static float WeaponDamageMultiplier => Weapon switch
        {
            PlayerWeapon.PulseSmg => 0.72f,
            PlayerWeapon.RailCannon => 1.75f,
            _ => 1f
        };

        public static float FireRateMultiplier => Weapon switch
        {
            PlayerWeapon.PulseSmg => 0.62f,
            PlayerWeapon.RailCannon => 1.85f,
            _ => 1f
        };

        public static float MagazineMultiplier => Weapon switch
        {
            PlayerWeapon.PulseSmg => 1.45f,
            PlayerWeapon.RailCannon => 0.58f,
            _ => 1f
        };

        public static float ReloadMultiplier => Weapon switch
        {
            PlayerWeapon.PulseSmg => 0.88f,
            PlayerWeapon.RailCannon => 1.18f,
            _ => 1f
        };

        public static Color WeaponColor => Weapon switch
        {
            PlayerWeapon.PulseSmg => new Color(0.15f, 0.95f, 1f, 1f),
            PlayerWeapon.RailCannon => new Color(1f, 0.24f, 0.62f, 1f),
            _ => new Color(0.72f, 0.48f, 1f, 1f)
        };

        public static int AbilityProjectileCount => Ability switch
        {
            PlayerAbility.PrismBurst => 3,
            PlayerAbility.SeekerCore => 2,
            _ => 1
        };

        public static float AbilitySpread => Ability switch
        {
            PlayerAbility.PrismBurst => 28f,
            PlayerAbility.SeekerCore => 10f,
            _ => 0f
        };

        public static float AbilityDamageMultiplier => Ability switch
        {
            PlayerAbility.ChargedRound => 2.45f,
            PlayerAbility.PrismBurst => 0.82f,
            PlayerAbility.SeekerCore => 1.08f,
            _ => 1f
        };

        public static float AbilityVisualScale => Ability switch
        {
            PlayerAbility.ChargedRound => 2f,
            PlayerAbility.PrismBurst => 1.35f,
            PlayerAbility.SeekerCore => 1.55f,
            _ => 1f
        };

        public static bool AbilityHoming => Ability == PlayerAbility.SeekerCore;

        public static Color AbilityColor => Ability switch
        {
            PlayerAbility.PrismBurst => new Color(1f, 0.2f, 0.72f, 1f),
            PlayerAbility.SeekerCore => new Color(0.1f, 1f, 0.82f, 1f),
            _ => new Color(0.35f, 0.78f, 1f, 1f)
        };

        public static string CharacterName(bool spanish) => Character switch
        {
            PlayerCharacter.Lyria => "LYRIA",
            PlayerCharacter.Manu => "MANU",
            PlayerCharacter.Tori => "TORI",
            _ => "ALEX"
        };

        public static string CharacterRole(bool spanish) => Character switch
        {
            PlayerCharacter.Lyria => spanish ? "Ágil · Menos vida" : "Agile · Less health",
            PlayerCharacter.Manu => spanish ? "Resistente · Más lento" : "Tough · Slower",
            PlayerCharacter.Tori => spanish ? "Táctica · Versátil" : "Tactical · Versatile",
            _ => spanish ? "Equilibrado" : "Balanced"
        };

        public static string WeaponName(bool spanish) => Weapon switch
        {
            PlayerWeapon.PulseSmg => spanish ? "SUBFUSIL DE PULSO" : "PULSE SMG",
            PlayerWeapon.RailCannon => spanish ? "CAÑÓN DE RIEL" : "RAIL CANNON",
            _ => spanish ? "RIFLE DE ARCO" : "ARC RIFLE"
        };

        public static string AbilityName(bool spanish) => Ability switch
        {
            PlayerAbility.PrismBurst => spanish ? "RÁFAGA PRISMA" : "PRISM BURST",
            PlayerAbility.SeekerCore => spanish ? "NÚCLEO RASTREADOR" : "SEEKER CORE",
            _ => spanish ? "BALA SOBRECARGADA" : "CHARGED ROUND"
        };

        public static string AbilityDescription(bool spanish) => Ability switch
        {
            PlayerAbility.PrismBurst => spanish ? "Tres proyectiles de energía en abanico" : "Three energy projectiles in a fan",
            PlayerAbility.SeekerCore => spanish ? "Dos núcleos que persiguen al objetivo" : "Two cores that seek the target",
            _ => spanish ? "Una bala concentrada de alto impacto" : "One concentrated high-impact round"
        };

        public static void CycleCharacter(int direction)
        {
            Character = (PlayerCharacter)Cycle((int)Character, direction, 4);
        }

        public static void CycleWeapon(int direction)
        {
            Weapon = (PlayerWeapon)Cycle((int)Weapon, direction, 3);
        }

        public static void CycleAbility(int direction)
        {
            Ability = (PlayerAbility)Cycle((int)Ability, direction, 3);
        }

        private static int Cycle(int current, int direction, int count)
        {
            return (current + (direction >= 0 ? 1 : -1) + count) % count;
        }

        private static void Save(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }
}
