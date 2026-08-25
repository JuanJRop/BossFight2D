using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Scripts.Controller
{
    public class MenuController : MonoBehaviour
    {
        private enum MenuPage
        {
            Home,
            Loadout,
            Settings
        }

        [SerializeField] private GameObject settings;

        private readonly Dictionary<string, TMP_Text> localizedLabels = new();
        private Canvas menuCanvas;
        private RectTransform pageHost;
        private GameObject homePage;
        private GameObject loadoutPage;
        private GameObject settingsPage;
        private TMP_Text characterValue;
        private TMP_Text weaponValue;
        private TMP_Text abilityValue;
        private TMP_Text volumeValue;
        private TMP_Text brightnessValue;
        private TMP_Text languageValue;
        private Image brightnessOverlay;
        private Slider volumeSlider;
        private Slider brightnessSlider;
        private bool menuBuilt;

        private static readonly Color Background = new(0.012f, 0.018f, 0.042f, 1f);
        private static readonly Color Panel = new(0.035f, 0.055f, 0.105f, 0.96f);
        private static readonly Color Cyan = new(0.08f, 0.92f, 1f, 1f);
        private static readonly Color Magenta = new(1f, 0.12f, 0.58f, 1f);
        private static readonly Color Muted = new(0.55f, 0.67f, 0.78f, 1f);

        private void Awake()
        {
            AudioListener.volume = GameLoadout.Volume;
            EnsureBrightnessOverlay();

            if (SceneManager.GetActiveScene().buildIndex != 0) return;
            DisableUnstableMenuAnimations();
            BuildMenu();
        }

        private void OnDestroy()
        {
            if (menuCanvas != null) menuCanvas.transform.DOKill();
        }

        public void SettingsMenu()
        {
            if (settings == null) return;
            bool isActive = settings.activeSelf;
            settings.SetActive(!isActive);

            if (UIManager.instance != null) UIManager.instance.IsPaused = !isActive;
            Time.timeScale = isActive ? 1f : 0f;
        }

        public void YouDieMenu(GameObject youDie)
        {
            if (youDie == null) return;
            bool isActive = youDie.activeSelf;
            youDie.SetActive(!isActive);
            if (UIManager.instance != null) UIManager.instance.IsPaused = !isActive;
            Time.timeScale = isActive ? 1f : 0f;
        }

        public void StartGame()
        {
            PlayerPrefs.Save();
            SceneManager.LoadScene(1);
            Time.timeScale = 1f;
        }

        public void BackToMenu()
        {
            SceneManager.LoadScene(0);
            Time.timeScale = 1f;
        }

        private void BuildMenu()
        {
            if (menuBuilt) return;
            menuBuilt = true;

            GameObject canvasObject = new("Redesigned Main Menu");
            menuCanvas = canvasObject.AddComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            menuCanvas.sortingOrder = 200;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            CreateImage("Dark Background", canvasRect, Vector2.zero, Vector2.one, Background);

            RectTransform accentTop = CreateImage("Top Neon Line", canvasRect,
                new Vector2(0f, 0.975f), new Vector2(1f, 0.982f), Cyan);
            AddGlow(accentTop, new Color(Cyan.r, Cyan.g, Cyan.b, 0.24f), 14f);

            TMP_Text title = CreateText("Title", canvasRect, new Vector2(0.045f, 0.84f),
                new Vector2(0.95f, 0.96f), "BOSSFIGHT // NEON PROTOCOL", 52, Cyan,
                TextAlignmentOptions.Left, FontStyles.Bold);
            title.characterSpacing = 4f;

            TMP_Text subtitle = CreateText("Subtitle", canvasRect, new Vector2(0.05f, 0.79f),
                new Vector2(0.92f, 0.85f), string.Empty, 19, Muted, TextAlignmentOptions.Left);
            localizedLabels["subtitle"] = subtitle;

            RectTransform navigation = CreatePanel("Navigation", canvasRect,
                new Vector2(0.045f, 0.13f), new Vector2(0.29f, 0.76f));
            VerticalLayoutGroup navLayout = navigation.gameObject.AddComponent<VerticalLayoutGroup>();
            navLayout.padding = new RectOffset(22, 22, 28, 28);
            navLayout.spacing = 16f;
            navLayout.childControlHeight = false;
            navLayout.childForceExpandHeight = false;

            CreateNavButton(navigation, "play", StartGame, Cyan);
            CreateNavButton(navigation, "loadout", () => ShowPage(MenuPage.Loadout), Magenta);
            CreateNavButton(navigation, "settings", () => ShowPage(MenuPage.Settings), Cyan);
            CreateNavButton(navigation, "exit", QuitGame, new Color(1f, 0.4f, 0.25f, 1f));

            pageHost = CreatePanel("Content", canvasRect,
                new Vector2(0.32f, 0.13f), new Vector2(0.955f, 0.76f));
            homePage = BuildHomePage(pageHost);
            loadoutPage = BuildLoadoutPage(pageHost);
            settingsPage = BuildSettingsPage(pageHost);

            TMP_Text footer = CreateText("Footer", canvasRect, new Vector2(0.05f, 0.045f),
                new Vector2(0.95f, 0.1f), string.Empty, 16, Muted, TextAlignmentOptions.Left);
            localizedLabels["footer"] = footer;

            RefreshLanguage();
            RefreshLoadout();
            ShowPage(MenuPage.Home, false);
            EnsureBrightnessOverlay();
        }

        private GameObject BuildHomePage(RectTransform host)
        {
            RectTransform page = CreatePage("Home Page", host);
            TMP_Text heading = CreateText("Home Heading", page, new Vector2(0.06f, 0.72f),
                new Vector2(0.94f, 0.92f), string.Empty, 42, Color.white,
                TextAlignmentOptions.Left, FontStyles.Bold);
            localizedLabels["homeHeading"] = heading;

            TMP_Text description = CreateText("Home Description", page, new Vector2(0.06f, 0.37f),
                new Vector2(0.8f, 0.72f), string.Empty, 23, Muted, TextAlignmentOptions.TopLeft);
            localizedLabels["homeDescription"] = description;

            CreateStatChip(page, new Vector2(0.06f, 0.15f), "01", "BOSS");
            CreateStatChip(page, new Vector2(0.32f, 0.15f), "03", "LOADOUT");
            CreateStatChip(page, new Vector2(0.58f, 0.15f), "100%", "DANGER");
            return page.gameObject;
        }

        private GameObject BuildLoadoutPage(RectTransform host)
        {
            RectTransform page = CreatePage("Loadout Page", host);
            TMP_Text heading = CreateText("Loadout Heading", page, new Vector2(0.06f, 0.82f),
                new Vector2(0.94f, 0.95f), string.Empty, 38, Magenta,
                TextAlignmentOptions.Left, FontStyles.Bold);
            localizedLabels["loadoutHeading"] = heading;

            characterValue = CreateSelector(page, 0.62f, "character", () =>
            {
                GameLoadout.CycleCharacter(-1);
                RefreshLoadout();
            }, () =>
            {
                GameLoadout.CycleCharacter(1);
                RefreshLoadout();
            });

            weaponValue = CreateSelector(page, 0.39f, "weapon", () =>
            {
                GameLoadout.CycleWeapon(-1);
                RefreshLoadout();
            }, () =>
            {
                GameLoadout.CycleWeapon(1);
                RefreshLoadout();
            });

            abilityValue = CreateSelector(page, 0.16f, "ability", () =>
            {
                GameLoadout.CycleAbility(-1);
                RefreshLoadout();
            }, () =>
            {
                GameLoadout.CycleAbility(1);
                RefreshLoadout();
            });
            return page.gameObject;
        }

        private GameObject BuildSettingsPage(RectTransform host)
        {
            RectTransform page = CreatePage("Settings Page", host);
            TMP_Text heading = CreateText("Settings Heading", page, new Vector2(0.06f, 0.82f),
                new Vector2(0.94f, 0.95f), string.Empty, 38, Cyan,
                TextAlignmentOptions.Left, FontStyles.Bold);
            localizedLabels["settingsHeading"] = heading;

            volumeSlider = CreateSettingSlider(page, 0.62f, "volume", GameLoadout.Volume, value =>
            {
                GameLoadout.Volume = value;
                if (volumeValue != null) volumeValue.text = $"{Mathf.RoundToInt(value * 100f)}%";
            }, out volumeValue);

            float normalizedBrightness = Mathf.InverseLerp(0.35f, 1f, GameLoadout.Brightness);
            brightnessSlider = CreateSettingSlider(page, 0.38f, "brightness", normalizedBrightness, value =>
            {
                GameLoadout.Brightness = Mathf.Lerp(0.35f, 1f, value);
                UpdateBrightnessOverlay();
                if (brightnessValue != null) brightnessValue.text = $"{Mathf.RoundToInt(value * 100f)}%";
            }, out brightnessValue);

            TMP_Text languageLabel = CreateText("Language Label", page, new Vector2(0.08f, 0.13f),
                new Vector2(0.4f, 0.26f), string.Empty, 21, Muted, TextAlignmentOptions.Left);
            localizedLabels["language"] = languageLabel;
            Button languageButton = CreateButton("Language Button", page, new Vector2(0.48f, 0.12f),
                new Vector2(0.88f, 0.27f), string.Empty, ToggleLanguage, Magenta);
            languageValue = languageButton.GetComponentInChildren<TMP_Text>();
            return page.gameObject;
        }

        private TMP_Text CreateSelector(RectTransform page, float verticalCenter, string localizationKey,
            Action previous, Action next)
        {
            TMP_Text label = CreateText(localizationKey + " Label", page,
                new Vector2(0.08f, verticalCenter + 0.07f), new Vector2(0.38f, verticalCenter + 0.18f),
                string.Empty, 19, Muted, TextAlignmentOptions.Left);
            localizedLabels[localizationKey] = label;

            CreateButton(localizationKey + " Previous", page,
                new Vector2(0.08f, verticalCenter - 0.08f), new Vector2(0.19f, verticalCenter + 0.06f),
                "<", previous, Cyan);

            TMP_Text value = CreateText(localizationKey + " Value", page,
                new Vector2(0.21f, verticalCenter - 0.08f), new Vector2(0.77f, verticalCenter + 0.06f),
                string.Empty, 24, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            AddOutline(value, new Color(0.08f, 0.8f, 1f, 0.45f));

            CreateButton(localizationKey + " Next", page,
                new Vector2(0.79f, verticalCenter - 0.08f), new Vector2(0.9f, verticalCenter + 0.06f),
                ">", next, Magenta);
            return value;
        }

        private Slider CreateSettingSlider(RectTransform page, float verticalCenter, string localizationKey,
            float initialValue, Action<float> changed, out TMP_Text valueText)
        {
            TMP_Text label = CreateText(localizationKey + " Label", page,
                new Vector2(0.08f, verticalCenter + 0.08f), new Vector2(0.55f, verticalCenter + 0.18f),
                string.Empty, 20, Muted, TextAlignmentOptions.Left);
            localizedLabels[localizationKey] = label;

            valueText = CreateText(localizationKey + " Value", page,
                new Vector2(0.76f, verticalCenter + 0.08f), new Vector2(0.9f, verticalCenter + 0.18f),
                $"{Mathf.RoundToInt(initialValue * 100f)}%", 19, Color.white, TextAlignmentOptions.Right);

            GameObject sliderObject = new(localizationKey + " Slider", typeof(RectTransform), typeof(Slider));
            RectTransform rect = sliderObject.GetComponent<RectTransform>();
            rect.SetParent(page, false);
            SetAnchors(rect, new Vector2(0.08f, verticalCenter - 0.04f),
                new Vector2(0.9f, verticalCenter + 0.05f));

            RectTransform background = CreateImage("Track", rect, new Vector2(0f, 0.38f),
                new Vector2(1f, 0.62f), new Color(0.09f, 0.15f, 0.23f, 1f));
            RectTransform fillArea = CreateRect("Fill Area", rect, new Vector2(0f, 0.2f), new Vector2(1f, 0.8f));
            RectTransform fill = CreateImage("Fill", fillArea, Vector2.zero, Vector2.one, Cyan);
            RectTransform handleArea = CreateRect("Handle Area", rect, Vector2.zero, Vector2.one);
            RectTransform handle = CreateImage("Handle", handleArea, new Vector2(0f, 0.14f),
                new Vector2(0.035f, 0.86f), Magenta);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(initialValue);
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(value => changed(value));
            return slider;
        }

        private void CreateNavButton(RectTransform parent, string key, Action action, Color accent)
        {
            Button button = CreateButton(key + " Button", parent, Vector2.zero, Vector2.one,
                string.Empty, action, accent);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 74f;
            localizedLabels[key] = button.GetComponentInChildren<TMP_Text>();
        }

        private void ShowPage(MenuPage page, bool animate = true)
        {
            if (homePage == null) return;
            homePage.SetActive(page == MenuPage.Home);
            loadoutPage.SetActive(page == MenuPage.Loadout);
            settingsPage.SetActive(page == MenuPage.Settings);

            GameObject active = page switch
            {
                MenuPage.Loadout => loadoutPage,
                MenuPage.Settings => settingsPage,
                _ => homePage
            };

            if (!animate || active == null) return;
            RectTransform rect = active.GetComponent<RectTransform>();
            CanvasGroup group = active.GetComponent<CanvasGroup>();
            rect.DOKill();
            group.DOKill();
            rect.anchoredPosition = new Vector2(42f, 0f);
            group.alpha = 0f;
            DOTween.Sequence()
                .SetUpdate(true)
                .Append(rect.DOAnchorPosX(0f, 0.28f).SetEase(Ease.OutCubic))
                .Join(group.DOFade(1f, 0.22f))
                .SetLink(active, LinkBehaviour.KillOnDestroy);
        }

        private void RefreshLoadout()
        {
            bool spanish = GameLoadout.IsSpanish;
            if (characterValue != null) characterValue.text = GameLoadout.CharacterName(spanish);
            if (weaponValue != null) weaponValue.text = GameLoadout.WeaponName(spanish);
            if (abilityValue != null) abilityValue.text = GameLoadout.AbilityName(spanish);
        }

        private void ToggleLanguage()
        {
            GameLoadout.IsSpanish = !GameLoadout.IsSpanish;
            RefreshLanguage();
            RefreshLoadout();
        }

        private void RefreshLanguage()
        {
            bool es = GameLoadout.IsSpanish;
            SetLocalized("subtitle", es ? "CONFIGURA TU COMBATIENTE. ENTRA A LA ARENA." :
                "CONFIGURE YOUR FIGHTER. ENTER THE ARENA.");
            SetLocalized("play", es ? "INICIAR COMBATE" : "START FIGHT");
            SetLocalized("loadout", es ? "EQUIPAMIENTO" : "LOADOUT");
            SetLocalized("settings", es ? "AJUSTES" : "SETTINGS");
            SetLocalized("exit", es ? "SALIR" : "EXIT");
            SetLocalized("homeHeading", es ? "EL JEFE YA ESTÁ DESPIERTO" : "THE BOSS IS ALREADY AWAKE");
            SetLocalized("homeDescription", es
                ? "Elige tu configuración, carga el núcleo golpeando al enemigo y libera una única bala especial con Q."
                : "Choose your setup, charge the core by hitting the enemy, then release one special round with Q.");
            SetLocalized("loadoutHeading", es ? "CONFIGURACIÓN DE COMBATE" : "COMBAT LOADOUT");
            SetLocalized("settingsHeading", es ? "SISTEMA" : "SYSTEM");
            SetLocalized("character", es ? "PERSONAJE" : "CHARACTER");
            SetLocalized("weapon", es ? "ARMA" : "WEAPON");
            SetLocalized("ability", es ? "HABILIDAD" : "ABILITY");
            SetLocalized("volume", es ? "VOLUMEN" : "VOLUME");
            SetLocalized("brightness", es ? "BRILLO" : "BRIGHTNESS");
            SetLocalized("language", es ? "IDIOMA" : "LANGUAGE");
            SetLocalized("footer", es
                ? "WASD MOVER  //  RATÓN DISPARAR  //  R RECARGAR  //  Q PODER"
                : "WASD MOVE  //  MOUSE FIRE  //  R RELOAD  //  Q POWER");
            if (languageValue != null) languageValue.text = es ? "ESPAÑOL" : "ENGLISH";
        }

        private void SetLocalized(string key, string value)
        {
            if (localizedLabels.TryGetValue(key, out TMP_Text label) && label != null) label.text = value;
        }

        private void DisableUnstableMenuAnimations()
        {
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour == null) continue;
                string typeName = behaviour.GetType().Name;
                if (typeName == "PlayerJump" || typeName == "MenuAnimation") behaviour.enabled = false;
            }
        }

        private void EnsureBrightnessOverlay()
        {
            GameObject existing = GameObject.Find("Global Brightness Overlay");
            if (existing != null)
            {
                brightnessOverlay = existing.GetComponentInChildren<Image>();
                UpdateBrightnessOverlay();
                return;
            }

            GameObject canvasObject = new("Global Brightness Overlay");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            canvasObject.AddComponent<CanvasScaler>();
            GameObject imageObject = new("Brightness Filter", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(canvasObject.transform, false);
            SetAnchors(rect, Vector2.zero, Vector2.one);
            brightnessOverlay = imageObject.GetComponent<Image>();
            brightnessOverlay.color = Color.black;
            brightnessOverlay.raycastTarget = false;
            UpdateBrightnessOverlay();
        }

        private void UpdateBrightnessOverlay()
        {
            if (brightnessOverlay == null) return;
            float darkness = Mathf.InverseLerp(1f, 0.35f, GameLoadout.Brightness) * 0.68f;
            brightnessOverlay.color = new Color(0f, 0f, 0f, darkness);
        }

        private static RectTransform CreatePage(string name, RectTransform parent)
        {
            RectTransform page = CreateRect(name, parent, Vector2.zero, Vector2.one);
            page.gameObject.AddComponent<CanvasGroup>();
            return page;
        }

        private static RectTransform CreatePanel(string name, RectTransform parent, Vector2 min, Vector2 max)
        {
            RectTransform panel = CreateImage(name, parent, min, max, Panel);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.75f, 1f, 0.28f);
            outline.effectDistance = new Vector2(2f, -2f);
            return panel;
        }

        private static RectTransform CreateImage(string name, RectTransform parent, Vector2 min, Vector2 max,
            Color color)
        {
            GameObject imageObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, min, max);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 min, Vector2 max)
        {
            GameObject rectObject = new(name, typeof(RectTransform));
            RectTransform rect = rectObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, min, max);
            return rect;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, Vector2 min, Vector2 max,
            string value, float size, Color color, TextAlignmentOptions alignment,
            FontStyles style = FontStyles.Normal)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, min, max);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.enableWordWrapping = true;
            return text;
        }

        private static Button CreateButton(string name, RectTransform parent, Vector2 min, Vector2 max,
            string value, Action action, Color accent)
        {
            RectTransform rect = CreateImage(name, parent, min, max, new Color(0.055f, 0.09f, 0.15f, 1f));
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.055f, 0.09f, 0.15f, 1f);
            colors.highlightedColor = new Color(accent.r * 0.32f, accent.g * 0.32f, accent.b * 0.32f, 1f);
            colors.pressedColor = new Color(accent.r * 0.5f, accent.g * 0.5f, accent.b * 0.5f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(() => action?.Invoke());

            RectTransform line = CreateImage("Accent", rect, new Vector2(0f, 0f), new Vector2(0.018f, 1f), accent);
            line.GetComponent<Image>().raycastTarget = false;
            TMP_Text label = CreateText("Label", rect, new Vector2(0.08f, 0.08f), new Vector2(0.94f, 0.92f),
                value, 21, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            label.raycastTarget = false;
            return button;
        }

        private static void CreateStatChip(RectTransform parent, Vector2 anchor, string value, string label)
        {
            RectTransform chip = CreateImage("Stat " + label, parent, anchor,
                anchor + new Vector2(0.2f, 0.13f), new Color(0.03f, 0.1f, 0.15f, 0.95f));
            CreateText("Value", chip, new Vector2(0.04f, 0.38f), new Vector2(0.96f, 0.92f),
                value, 25, Cyan, TextAlignmentOptions.Center, FontStyles.Bold);
            CreateText("Label", chip, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.4f),
                label, 13, Muted, TextAlignmentOptions.Center);
        }

        private static void AddOutline(TMP_Text text, Color color)
        {
            text.outlineColor = color;
            text.outlineWidth = 0.18f;
        }

        private static void AddGlow(RectTransform rect, Color color, float distance)
        {
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = new Vector2(0f, -distance);
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("Quit requested from main menu.");
#else
            Application.Quit();
#endif
        }
    }
}
