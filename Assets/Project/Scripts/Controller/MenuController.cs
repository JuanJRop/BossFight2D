using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Scripts.Controller
{
    public class MenuController : MonoBehaviour
    {
        [SerializeField] private GameObject settings;

        private static readonly Color CavePanel = new(0.105f, 0.045f, 0.035f, 0.97f);
        private static readonly Color CaveBorder = new(0.43f, 0.16f, 0.11f, 1f);
        private static readonly Color CaveButton = new(0.48f, 0.17f, 0.12f, 1f);
        private static readonly Color CaveButtonHover = new(0.64f, 0.25f, 0.17f, 1f);
        private static readonly Color Cream = new(0.98f, 0.91f, 0.8f, 1f);
        private static readonly Color MutedCream = new(0.74f, 0.59f, 0.49f, 1f);

        private RectTransform menuRoot;
        private GameObject optionsPanel;
        private Image playerPreview;
        private Vector3 playerPreviewBaseScale;
        private Sprite buttonSprite;
        private TMP_FontAsset menuFont;
        private TMP_Text startLabel;
        private TMP_Text optionsLabel;
        private TMP_Text characterTitle;
        private TMP_Text characterValue;
        private TMP_Text characterDescription;
        private TMP_Text optionsTitle;
        private TMP_Text weaponTitle;
        private TMP_Text weaponValue;
        private TMP_Text abilityTitle;
        private TMP_Text abilityValue;
        private TMP_Text volumeTitle;
        private TMP_Text volumeValue;
        private TMP_Text brightnessTitle;
        private TMP_Text brightnessValue;
        private TMP_Text languageTitle;
        private TMP_Text languageValue;
        private Slider volumeSlider;
        private Slider brightnessSlider;
        private Image brightnessOverlay;
        private bool menuBuilt;

        private void Awake()
        {
            AudioListener.volume = GameLoadout.Volume;

            if (SceneManager.GetActiveScene().buildIndex == 0)
            {
                BuildCaveMenu();
                EnsureBrightnessOverlay();
            }
            else
            {
                EnsureBrightnessOverlay();
            }
        }

        private void OnDestroy()
        {
            if (menuRoot != null) menuRoot.DOKill();
        }

        public void SettingsMenu()
        {
            if (SceneManager.GetActiveScene().buildIndex == 0 && optionsPanel != null)
            {
                ToggleOptions();
                return;
            }

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

        private void BuildCaveMenu()
        {
            if (menuBuilt) return;
            menuBuilt = true;

            GameObject background = GameObject.Find("BG");
            GameObject canvasObject = GameObject.Find("Canvas");
            menuRoot = background != null
                ? background.GetComponent<RectTransform>()
                : canvasObject != null ? canvasObject.GetComponent<RectTransform>() : null;
            if (menuRoot == null)
            {
                Debug.LogError("The cave menu requires its existing Canvas or BG RectTransform.", this);
                return;
            }

            Button startButton = FindButton("StartButton");
            Button optionsButton = FindButton("OptionsButton");
            CaptureMenuStyle(startButton);

            startLabel = ConfigureExistingButton(startButton, StartGame);
            optionsLabel = ConfigureExistingButton(optionsButton, ToggleOptions);

            GameObject previewObject = GameObject.Find("Player");
            if (previewObject != null)
            {
                playerPreview = previewObject.GetComponent<Image>();
                playerPreviewBaseScale = previewObject.transform.localScale;
            }

            BuildCharacterSelector();
            BuildOptionsPanel();
            RefreshLanguage();
            RefreshLoadout();
            ApplyCharacterPreview(false);
        }

        private void BuildCharacterSelector()
        {
            RectTransform panel = CreateFramedPanel("Character Selector", menuRoot,
                new Vector2(0.27f, 0.035f), new Vector2(0.73f, 0.205f));

            characterTitle = CreateText("Character Title", panel, new Vector2(0.08f, 0.66f),
                new Vector2(0.92f, 0.94f), string.Empty, 19f, MutedCream, TextAlignmentOptions.Center);

            CreateButton("Previous Character", panel, new Vector2(0.045f, 0.17f),
                new Vector2(0.2f, 0.62f), "<", () => ChangeCharacter(-1));

            characterValue = CreateText("Character Value", panel, new Vector2(0.21f, 0.29f),
                new Vector2(0.79f, 0.65f), string.Empty, 25f, Cream,
                TextAlignmentOptions.Center, FontStyles.Bold);

            CreateButton("Next Character", panel, new Vector2(0.8f, 0.17f),
                new Vector2(0.955f, 0.62f), ">", () => ChangeCharacter(1));

            characterDescription = CreateText("Character Description", panel, new Vector2(0.2f, 0.02f),
                new Vector2(0.8f, 0.28f), string.Empty, 14f, MutedCream, TextAlignmentOptions.Center);
        }

        private void BuildOptionsPanel()
        {
            RectTransform panel = CreateFramedPanel("Cave Options Panel", menuRoot,
                new Vector2(0.27f, 0.17f), new Vector2(0.73f, 0.88f));
            optionsPanel = panel.gameObject;

            optionsTitle = CreateText("Options Title", panel, new Vector2(0.08f, 0.88f),
                new Vector2(0.8f, 0.98f), string.Empty, 28f, Cream,
                TextAlignmentOptions.Left, FontStyles.Bold);

            CreateButton("Close Options", panel, new Vector2(0.83f, 0.89f),
                new Vector2(0.96f, 0.97f), "X", ToggleOptions);

            weaponTitle = CreateText("Weapon Title", panel, new Vector2(0.09f, 0.77f),
                new Vector2(0.44f, 0.85f), string.Empty, 17f, MutedCream, TextAlignmentOptions.Left);
            weaponValue = CreateCompactSelector(panel, 0.67f, () => ChangeWeapon(-1), () => ChangeWeapon(1));

            abilityTitle = CreateText("Ability Title", panel, new Vector2(0.09f, 0.56f),
                new Vector2(0.44f, 0.64f), string.Empty, 17f, MutedCream, TextAlignmentOptions.Left);
            abilityValue = CreateCompactSelector(panel, 0.46f, () => ChangeAbility(-1), () => ChangeAbility(1));

            volumeSlider = CreateSettingSlider(panel, 0.32f, GameLoadout.Volume, value =>
            {
                GameLoadout.Volume = value;
                UpdateSettingValues();
            });
            volumeTitle = CreateText("Volume Title", panel, new Vector2(0.09f, 0.37f),
                new Vector2(0.48f, 0.44f), string.Empty, 16f, MutedCream, TextAlignmentOptions.Left);
            volumeValue = CreateText("Volume Value", panel, new Vector2(0.75f, 0.37f),
                new Vector2(0.91f, 0.44f), string.Empty, 16f, Cream, TextAlignmentOptions.Right);

            float normalizedBrightness = Mathf.InverseLerp(0.35f, 1f, GameLoadout.Brightness);
            brightnessSlider = CreateSettingSlider(panel, 0.18f, normalizedBrightness, value =>
            {
                GameLoadout.Brightness = Mathf.Lerp(0.35f, 1f, value);
                UpdateBrightnessOverlay();
                UpdateSettingValues();
            });
            brightnessTitle = CreateText("Brightness Title", panel, new Vector2(0.09f, 0.23f),
                new Vector2(0.48f, 0.3f), string.Empty, 16f, MutedCream, TextAlignmentOptions.Left);
            brightnessValue = CreateText("Brightness Value", panel, new Vector2(0.75f, 0.23f),
                new Vector2(0.91f, 0.3f), string.Empty, 16f, Cream, TextAlignmentOptions.Right);

            languageTitle = CreateText("Language Title", panel, new Vector2(0.09f, 0.055f),
                new Vector2(0.38f, 0.13f), string.Empty, 16f, MutedCream, TextAlignmentOptions.Left);
            Button languageButton = CreateButton("Language Button", panel, new Vector2(0.49f, 0.035f),
                new Vector2(0.91f, 0.135f), string.Empty, ToggleLanguage);
            languageValue = languageButton.GetComponentInChildren<TMP_Text>();

            optionsPanel.SetActive(false);
        }

        private TMP_Text CreateCompactSelector(RectTransform panel, float y, Action previous, Action next)
        {
            CreateButton("Previous", panel, new Vector2(0.09f, y), new Vector2(0.2f, y + 0.09f), "<", previous);
            TMP_Text value = CreateText("Selection", panel, new Vector2(0.22f, y),
                new Vector2(0.78f, y + 0.09f), string.Empty, 19f, Cream,
                TextAlignmentOptions.Center, FontStyles.Bold);
            CreateButton("Next", panel, new Vector2(0.8f, y), new Vector2(0.91f, y + 0.09f), ">", next);
            return value;
        }

        private Slider CreateSettingSlider(RectTransform panel, float y, float initial, Action<float> changed)
        {
            GameObject sliderObject = new("Cave Slider", typeof(RectTransform), typeof(Slider));
            RectTransform rect = sliderObject.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            SetAnchors(rect, new Vector2(0.09f, y), new Vector2(0.91f, y + 0.055f));

            RectTransform track = CreateImage("Track", rect, new Vector2(0f, 0.3f),
                new Vector2(1f, 0.7f), new Color(0.07f, 0.025f, 0.02f, 1f));
            RectTransform fillArea = CreateRect("Fill Area", rect, new Vector2(0f, 0.15f), new Vector2(1f, 0.85f));
            RectTransform fill = CreateImage("Fill", fillArea, Vector2.zero, Vector2.one, CaveBorder);
            RectTransform handleArea = CreateRect("Handle Area", rect, Vector2.zero, Vector2.one);
            RectTransform handle = CreateImage("Handle", handleArea,
                new Vector2(0f, 0.02f), new Vector2(0.045f, 0.98f), Cream);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(initial);
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.onValueChanged.AddListener(value => changed(value));
            return slider;
        }

        private void CaptureMenuStyle(Button template)
        {
            if (template == null) return;
            Image image = template.GetComponent<Image>();
            if (image != null) buttonSprite = image.sprite;
            TMP_Text text = template.GetComponentInChildren<TMP_Text>();
            if (text != null) menuFont = text.font;
        }

        private TMP_Text ConfigureExistingButton(Button button, Action action)
        {
            if (button == null) return null;
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                Punch(button.transform);
                action?.Invoke();
            });
            ConfigureButtonColors(button);

            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.color = Color.white;
                text.outlineColor = Color.black;
                text.outlineWidth = 0.22f;
            }
            return text;
        }

        private Button CreateButton(string name, RectTransform parent, Vector2 min, Vector2 max,
            string label, Action action)
        {
            RectTransform rect = CreateImage(name, parent, min, max, CaveButton);
            Image image = rect.GetComponent<Image>();
            if (buttonSprite != null)
            {
                image.sprite = buttonSprite;
                image.type = Image.Type.Sliced;
            }

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ConfigureButtonColors(button);
            button.onClick.AddListener(() =>
            {
                Punch(button.transform);
                action?.Invoke();
            });

            TMP_Text text = CreateText("Label", rect, new Vector2(0.05f, 0.06f),
                new Vector2(0.95f, 0.94f), label, 18f, Color.white,
                TextAlignmentOptions.Center, FontStyles.Bold);
            text.outlineColor = Color.black;
            text.outlineWidth = 0.2f;
            text.raycastTarget = false;
            return button;
        }

        private static void ConfigureButtonColors(Button button)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = CaveButtonHover;
            colors.pressedColor = new Color(0.32f, 0.09f, 0.065f, 1f);
            colors.selectedColor = CaveButtonHover;
            colors.disabledColor = new Color(0.2f, 0.12f, 0.1f, 0.55f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private RectTransform CreateFramedPanel(string name, RectTransform parent, Vector2 min, Vector2 max)
        {
            RectTransform border = CreateImage(name + " Border", parent, min, max, CaveBorder);
            RectTransform panel = CreateImage(name, border, new Vector2(0.012f, 0.018f),
                new Vector2(0.988f, 0.982f), CavePanel);
            return panel;
        }

        private TMP_Text CreateText(string name, RectTransform parent, Vector2 min, Vector2 max,
            string value, float size, Color color, TextAlignmentOptions alignment,
            FontStyles style = FontStyles.Normal)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, min, max);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (menuFont != null) text.font = menuFont;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
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

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Button FindButton(string objectName)
        {
            GameObject found = GameObject.Find(objectName);
            return found != null ? found.GetComponent<Button>() : null;
        }

        private void ChangeCharacter(int direction)
        {
            GameLoadout.CycleCharacter(direction);
            RefreshLoadout();
            ApplyCharacterPreview(true);
        }

        private void ChangeWeapon(int direction)
        {
            GameLoadout.CycleWeapon(direction);
            RefreshLoadout();
        }

        private void ChangeAbility(int direction)
        {
            GameLoadout.CycleAbility(direction);
            RefreshLoadout();
        }

        private void ApplyCharacterPreview(bool animate)
        {
            if (playerPreview == null) return;
            playerPreview.color = GameLoadout.CharacterColor;
            float size = GameLoadout.Character switch
            {
                PlayerCharacter.Striker => 0.94f,
                PlayerCharacter.Bulwark => 1.08f,
                _ => 1f
            };
            playerPreview.transform.localScale = playerPreviewBaseScale * size;
            if (!animate) return;
            playerPreview.transform.DOKill();
            playerPreview.transform
                .DOPunchScale(playerPreviewBaseScale * 0.12f, 0.28f, 6, 0.55f)
                .SetUpdate(true)
                .SetLink(playerPreview.gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void ToggleOptions()
        {
            if (optionsPanel == null) return;
            bool show = !optionsPanel.activeSelf;
            optionsPanel.SetActive(show);
            if (!show) return;

            RectTransform rect = optionsPanel.GetComponent<RectTransform>();
            CanvasGroup group = optionsPanel.GetComponent<CanvasGroup>();
            if (group == null) group = optionsPanel.AddComponent<CanvasGroup>();
            rect.DOKill();
            group.DOKill();
            rect.localScale = Vector3.one * 0.92f;
            group.alpha = 0f;
            DOTween.Sequence()
                .SetUpdate(true)
                .Append(rect.DOScale(1f, 0.2f).SetEase(Ease.OutBack))
                .Join(group.DOFade(1f, 0.14f))
                .SetLink(optionsPanel, LinkBehaviour.KillOnDestroy);
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
            if (startLabel != null) startLabel.text = es ? "Jugar" : "Start";
            if (optionsLabel != null) optionsLabel.text = es ? "Opciones" : "Options";
            if (characterTitle != null) characterTitle.text = es ? "CAMBIAR PERSONAJE" : "CHANGE CHARACTER";
            if (optionsTitle != null) optionsTitle.text = es ? "OPCIONES" : "OPTIONS";
            if (weaponTitle != null) weaponTitle.text = es ? "ARMA" : "WEAPON";
            if (abilityTitle != null) abilityTitle.text = es ? "HABILIDAD" : "ABILITY";
            if (volumeTitle != null) volumeTitle.text = es ? "VOLUMEN" : "VOLUME";
            if (brightnessTitle != null) brightnessTitle.text = es ? "BRILLO" : "BRIGHTNESS";
            if (languageTitle != null) languageTitle.text = es ? "IDIOMA" : "LANGUAGE";
            if (languageValue != null) languageValue.text = es ? "Español" : "English";
            UpdateSettingValues();
        }

        private void RefreshLoadout()
        {
            bool es = GameLoadout.IsSpanish;
            if (characterValue != null) characterValue.text = GameLoadout.CharacterName(es);
            if (weaponValue != null) weaponValue.text = GameLoadout.WeaponName(es);
            if (abilityValue != null) abilityValue.text = GameLoadout.AbilityName(es);
            if (characterDescription != null)
            {
                characterDescription.text = GameLoadout.Character switch
                {
                    PlayerCharacter.Striker => es ? "Más rápido · Menos vida" : "Faster · Less health",
                    PlayerCharacter.Bulwark => es ? "Más vida · Menos velocidad" : "More health · Less speed",
                    _ => es ? "Equilibrado" : "Balanced"
                };
            }
            UpdateSettingValues();
        }

        private void UpdateSettingValues()
        {
            if (volumeValue != null) volumeValue.text = $"{Mathf.RoundToInt(GameLoadout.Volume * 100f)}%";
            if (brightnessValue != null)
            {
                float normalized = Mathf.InverseLerp(0.35f, 1f, GameLoadout.Brightness);
                brightnessValue.text = $"{Mathf.RoundToInt(normalized * 100f)}%";
            }
        }

        private static void Punch(Transform target)
        {
            if (target == null) return;
            target.DOKill();
            target.DOPunchScale(Vector3.one * 0.08f, 0.16f, 4, 0.6f)
                .SetUpdate(true)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);
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

            GameObject canvasObject = new("Global Brightness Overlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            GameObject imageObject = new("Brightness Filter", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(canvasObject.transform, false);
            SetAnchors(rect, Vector2.zero, Vector2.one);
            brightnessOverlay = imageObject.GetComponent<Image>();
            brightnessOverlay.raycastTarget = false;
            UpdateBrightnessOverlay();
        }

        private void UpdateBrightnessOverlay()
        {
            if (brightnessOverlay == null) return;
            float darkness = Mathf.InverseLerp(1f, 0.35f, GameLoadout.Brightness) * 0.68f;
            brightnessOverlay.color = new Color(0f, 0f, 0f, darkness);
        }
    }
}
