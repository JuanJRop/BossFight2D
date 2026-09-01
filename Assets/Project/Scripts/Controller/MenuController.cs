using System;
using DG.Tweening;
using Project.Scripts.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Scripts.Controller
{
    public class MenuController : MonoBehaviour
    {
        [SerializeField] private GameObject settings;

        private static readonly Color Ink = new(0.055f, 0.022f, 0.018f, 0.96f);
        private static readonly Color Panel = new(0.105f, 0.045f, 0.035f, 0.90f);
        private static readonly Color Card = new(0.075f, 0.03f, 0.025f, 0.94f);
        private static readonly Color Border = new(0.44f, 0.17f, 0.11f, 1f);
        private static readonly Color Brick = new(0.52f, 0.19f, 0.13f, 1f);
        private static readonly Color BrickHover = new(0.68f, 0.28f, 0.18f, 1f);
        private static readonly Color Cream = new(0.98f, 0.92f, 0.82f, 1f);
        private static readonly Color Muted = new(0.72f, 0.58f, 0.48f, 1f);

        private RectTransform menuRoot;
        private GameObject optionsPanel;
        private GameObject skinCatalogInstance;
        private CharacterSkinSet[] skins;
        private Image characterPreview;
        private Sprite buttonSprite;
        private TMP_FontAsset menuFont;
        private TMP_Text titleText;
        private TMP_Text subtitleText;
        private TMP_Text characterLabel;
        private TMP_Text characterValue;
        private TMP_Text characterRole;
        private TMP_Text loadoutTitle;
        private TMP_Text weaponLabel;
        private TMP_Text weaponValue;
        private TMP_Text weaponDescription;
        private readonly Button[] weaponButtons = new Button[4];
        private TMP_Text startLabel;
        private TMP_Text optionsLabel;
        private TMP_Text optionsTitle;
        private TMP_Text volumeLabel;
        private TMP_Text volumeValue;
        private TMP_Text brightnessLabel;
        private TMP_Text brightnessValue;
        private TMP_Text languageLabel;
        private TMP_Text languageValue;
        private Image brightnessOverlay;
        private bool menuBuilt;

        private void Awake()
        {
            AudioListener.volume = GameLoadout.Volume;
            if (SceneManager.GetActiveScene().buildIndex == 0) BuildProfessionalMenu();
            EnsureBrightnessOverlay();
        }

        private void OnDestroy()
        {
            if (menuRoot != null) menuRoot.DOKill();
            if (skinCatalogInstance != null) Destroy(skinCatalogInstance);
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
            RunSession.BeginNewRun();
            RunSession.SelectClass(GameLoadout.StartingClass);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            SceneManager.LoadScene("WorldPath");
        }

        public void BackToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }

        private void BuildProfessionalMenu()
        {
            if (menuBuilt) return;
            menuBuilt = true;

            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject == null)
            {
                Debug.LogError("The menu scene requires its Canvas object.", this);
                return;
            }

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            Canvas sceneCanvas = canvasObject.GetComponent<Canvas>();
            if (sceneCanvas != null) sceneCanvas.pixelPerfect = true;
            PrepareLegacyArtForPixelRendering(canvasObject);

            CaptureLegacyStyle();
            HideLegacyMenuObjects();
            LoadSkinCatalog();

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            menuRoot = CreateRect("Professional Cave Menu", canvasRect, Vector2.zero, Vector2.one);
            menuRoot.SetAsLastSibling();
            Canvas menuCanvas = menuRoot.gameObject.AddComponent<Canvas>();
            menuCanvas.overrideSorting = true;
            menuCanvas.sortingOrder = 200;
            menuCanvas.pixelPerfect = true;
            menuRoot.gameObject.AddComponent<GraphicRaycaster>();

            Image dim = CreateImage("Backdrop Dim", menuRoot, Vector2.zero, Vector2.one,
                new Color(0.015f, 0.005f, 0.004f, 0.28f)).GetComponent<Image>();
            dim.raycastTarget = false;

            titleText = CreateText("Title", menuRoot, new Vector2(0.18f, 0.84f), new Vector2(0.82f, 0.96f),
                "KILL THE SPIKE", 58f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            titleText.characterSpacing = 2f;
            AddTextShadow(titleText, new Color(0f, 0f, 0f, 0.9f), new Vector2(4f, -4f));

            subtitleText = CreateText("Subtitle", menuRoot, new Vector2(0.25f, 0.79f), new Vector2(0.75f, 0.845f),
                string.Empty, 17f, Muted, TextAlignmentOptions.Center);

            RectTransform mainPanel = CreateFramedPanel("Main Menu Panel", menuRoot,
                new Vector2(0.17f, 0.205f), new Vector2(0.83f, 0.785f), Panel);
            BuildCharacterCard(mainPanel);
            BuildLoadoutCard(mainPanel);
            BuildActionBar();
            BuildOptionsPanel();
            RefreshLanguage();
            RefreshLoadout(false);

            CanvasGroup group = menuRoot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            menuRoot.localScale = Vector3.one * 0.985f;
            DOTween.Sequence()
                .SetUpdate(true)
                .Append(group.DOFade(1f, 0.22f))
                .Join(menuRoot.DOScale(1f, 0.3f).SetEase(Ease.OutCubic))
                .SetLink(menuRoot.gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void BuildCharacterCard(RectTransform mainPanel)
        {
            RectTransform card = CreateFramedPanel("Character Card", mainPanel,
                new Vector2(0.025f, 0.055f), new Vector2(0.475f, 0.945f), Card);

            characterLabel = CreateText("Character Label", card, new Vector2(0.08f, 0.86f),
                new Vector2(0.92f, 0.95f), string.Empty, 17f, Muted, TextAlignmentOptions.Center);

            RectTransform previewFrame = CreateFramedPanel("Character Preview", card,
                new Vector2(0.17f, 0.28f), new Vector2(0.83f, 0.84f),
                new Color(0.035f, 0.012f, 0.01f, 0.86f));

            RectTransform preview = CreateImage("Selected Character", previewFrame,
                new Vector2(0.24f, 0.12f), new Vector2(0.76f, 0.88f), Color.white);
            characterPreview = preview.GetComponent<Image>();
            characterPreview.preserveAspect = true;
            characterPreview.raycastTarget = false;
            AddImageShadow(characterPreview, new Color(0f, 0f, 0f, 0.75f), new Vector2(7f, -7f));

            CreateButton("Previous Character", card, new Vector2(0.08f, 0.115f),
                new Vector2(0.22f, 0.235f), "<", () => ChangeCharacter(-1));
            CreateButton("Next Character", card, new Vector2(0.78f, 0.115f),
                new Vector2(0.92f, 0.235f), ">", () => ChangeCharacter(1));

            characterValue = CreateText("Character Name", card, new Vector2(0.23f, 0.145f),
                new Vector2(0.77f, 0.245f), string.Empty, 27f, Cream,
                TextAlignmentOptions.Center, FontStyles.Bold);
            characterRole = CreateText("Character Role", card, new Vector2(0.15f, 0.045f),
                new Vector2(0.85f, 0.13f), string.Empty, 14f, Muted, TextAlignmentOptions.Center);
        }

        private void BuildLoadoutCard(RectTransform mainPanel)
        {
            RectTransform card = CreateFramedPanel("Loadout Card", mainPanel,
                new Vector2(0.525f, 0.055f), new Vector2(0.975f, 0.945f), Card);

            loadoutTitle = CreateText("Loadout Title", card, new Vector2(0.08f, 0.86f),
                new Vector2(0.92f, 0.95f), string.Empty, 22f, Cream,
                TextAlignmentOptions.Center, FontStyles.Bold);

            weaponLabel = CreateText("Weapon Label", card, new Vector2(0.1f, 0.73f),
                new Vector2(0.9f, 0.81f), string.Empty,
                15f, Muted, TextAlignmentOptions.Center);

            PlayerWeapon[] weapons =
            {
                PlayerWeapon.Sword,
                PlayerWeapon.Bow,
                PlayerWeapon.MageStaff,
                PlayerWeapon.HealerStaff
            };
            for (int index = 0; index < weapons.Length; index++)
            {
                int column = index % 2;
                int row = index / 2;
                float left = 0.1f + column * 0.405f;
                float bottom = 0.51f - row * 0.145f;
                PlayerWeapon weapon = weapons[index];
                weaponButtons[index] = CreateButton("Weapon " + weapon, card,
                    new Vector2(left, bottom), new Vector2(left + 0.37f, bottom + 0.105f),
                    string.Empty, () => SelectWeapon(weapon));
            }

            RectTransform divider = CreateImage("Growth Divider", card, new Vector2(0.1f, 0.29f),
                new Vector2(0.9f, 0.295f), new Color(Border.r, Border.g, Border.b, 0.75f));
            divider.GetComponent<Image>().raycastTarget = false;
            weaponValue = CreateText("Weapon Name", card, new Vector2(0.1f, 0.19f), new Vector2(0.9f, 0.27f),
                string.Empty, 22f, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            weaponDescription = CreateText("Weapon Description", card, new Vector2(0.11f, 0.075f),
                new Vector2(0.89f, 0.18f), string.Empty, 13f, Muted, TextAlignmentOptions.Center);
            weaponDescription.textWrappingMode = TextWrappingModes.Normal;
        }

        private void BuildActionBar()
        {
            RectTransform bar = CreateRect("Actions", menuRoot, new Vector2(0.32f, 0.055f), new Vector2(0.68f, 0.165f));
            Button start = CreateButton("Start Game", bar, new Vector2(0f, 0.08f), new Vector2(0.62f, 0.92f),
                string.Empty, StartGame);
            startLabel = start.GetComponentInChildren<TMP_Text>();
            Button options = CreateButton("Open Options", bar, new Vector2(0.66f, 0.08f), new Vector2(1f, 0.92f),
                string.Empty, ToggleOptions);
            optionsLabel = options.GetComponentInChildren<TMP_Text>();
        }

        private void BuildOptionsPanel()
        {
            RectTransform panel = CreateFramedPanel("Options Panel", menuRoot,
                new Vector2(0.31f, 0.215f), new Vector2(0.69f, 0.775f), Panel);
            optionsPanel = panel.parent.gameObject;

            optionsTitle = CreateText("Options Title", panel, new Vector2(0.09f, 0.84f),
                new Vector2(0.78f, 0.95f), string.Empty, 27f, Cream,
                TextAlignmentOptions.Left, FontStyles.Bold);
            CreateButton("Close", panel, new Vector2(0.82f, 0.855f), new Vector2(0.94f, 0.945f), "X", ToggleOptions);

            volumeLabel = CreateText("Volume Label", panel, new Vector2(0.1f, 0.68f),
                new Vector2(0.7f, 0.76f), string.Empty, 16f, Muted, TextAlignmentOptions.Left);
            volumeValue = CreateText("Volume Value", panel, new Vector2(0.72f, 0.68f),
                new Vector2(0.9f, 0.76f), string.Empty, 16f, Cream, TextAlignmentOptions.Right);
            CreateSlider(panel, 0.61f, GameLoadout.Volume, value =>
            {
                GameLoadout.Volume = value;
                UpdateSettingValues();
            });

            brightnessLabel = CreateText("Brightness Label", panel, new Vector2(0.1f, 0.45f),
                new Vector2(0.7f, 0.53f), string.Empty, 16f, Muted, TextAlignmentOptions.Left);
            brightnessValue = CreateText("Brightness Value", panel, new Vector2(0.72f, 0.45f),
                new Vector2(0.9f, 0.53f), string.Empty, 16f, Cream, TextAlignmentOptions.Right);
            CreateSlider(panel, 0.38f, Mathf.InverseLerp(0.35f, 1f, GameLoadout.Brightness), value =>
            {
                GameLoadout.Brightness = Mathf.Lerp(0.35f, 1f, value);
                UpdateBrightnessOverlay();
                UpdateSettingValues();
            });

            languageLabel = CreateText("Language Label", panel, new Vector2(0.1f, 0.18f),
                new Vector2(0.42f, 0.28f), string.Empty, 16f, Muted, TextAlignmentOptions.Left);
            Button language = CreateButton("Language", panel, new Vector2(0.48f, 0.16f),
                new Vector2(0.9f, 0.29f), string.Empty, ToggleLanguage);
            languageValue = language.GetComponentInChildren<TMP_Text>();
            optionsPanel.SetActive(false);
        }

        private Slider CreateSlider(RectTransform parent, float y, float initial, Action<float> changed)
        {
            GameObject sliderObject = new("Slider", typeof(RectTransform), typeof(Slider));
            RectTransform rect = sliderObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, new Vector2(0.1f, y), new Vector2(0.9f, y + 0.055f));

            CreateImage("Track", rect, new Vector2(0f, 0.32f), new Vector2(1f, 0.68f), Ink);
            RectTransform fillArea = CreateRect("Fill Area", rect, new Vector2(0f, 0.12f), new Vector2(1f, 0.88f));
            RectTransform fill = CreateImage("Fill", fillArea, Vector2.zero, Vector2.one, Border);
            RectTransform handleArea = CreateRect("Handle Area", rect, Vector2.zero, Vector2.one);
            RectTransform handle = CreateImage("Handle", handleArea,
                new Vector2(0f, 0f), new Vector2(0.04f, 1f), Cream);

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

        private void LoadSkinCatalog()
        {
            GameObject prefab = Resources.Load<GameObject>("Menu/MainCharacterSkins");
            if (prefab == null)
            {
                Debug.LogError("MainCharacterSkins prefab is missing from Resources/Menu.", this);
                skins = Array.Empty<CharacterSkinSet>();
                return;
            }

            skinCatalogInstance = Instantiate(prefab);
            skinCatalogInstance.name = "Menu Character Skin Catalog";
            skinCatalogInstance.hideFlags = HideFlags.HideInHierarchy;
            skinCatalogInstance.SetActive(false);
            skins = skinCatalogInstance.GetComponentsInChildren<CharacterSkinSet>(true);
            foreach (CharacterSkinSet skin in skins)
            {
                if (skin != null) skin.PrepareForCrispRendering();
            }
        }

        private void ChangeCharacter(int direction)
        {
            GameLoadout.CycleCharacter(direction);
            RefreshLoadout(true);
        }

        private void SelectWeapon(PlayerWeapon weapon)
        {
            GameLoadout.Weapon = weapon;
            RefreshLoadout(false);
        }

        private void RefreshLoadout(bool animateCharacter)
        {
            bool es = GameLoadout.IsSpanish;
            if (characterValue != null) characterValue.text = GameLoadout.CharacterName(es);
            if (characterRole != null) characterRole.text = GameLoadout.CharacterRole(es);
            if (weaponValue != null) weaponValue.text = GameLoadout.WeaponName(es);
            if (weaponDescription != null) weaponDescription.text = GameLoadout.WeaponDescription(es);
            RefreshWeaponButtons(es);

            if (characterPreview != null && skins != null && skins.Length > 0)
            {
                int index = Mathf.Clamp((int)GameLoadout.Character, 0, skins.Length - 1);
                CharacterSkinSet selectedSkin = skins[index];
                Sprite preview = selectedSkin.PreviewSprite;
                if (preview != null && preview.texture != null)
                {
                    preview.texture.filterMode = FilterMode.Point;
                    preview.texture.wrapMode = TextureWrapMode.Clamp;
                    preview.texture.anisoLevel = 0;
                }
                characterPreview.sprite = preview;
                characterPreview.color = Color.white;
                Vector3 previewScale = Vector3.one * selectedSkin.PreviewScale;
                characterPreview.transform.localScale = previewScale;
                if (animateCharacter)
                {
                    characterPreview.transform.DOKill();
                    characterPreview.transform.localScale = previewScale;
                    characterPreview.transform.DOPunchScale(Vector3.one * 0.12f, 0.28f, 6, 0.55f)
                        .SetUpdate(true)
                        .SetLink(characterPreview.gameObject, LinkBehaviour.KillOnDestroy);
                }
            }

            UpdateSettingValues();
        }

        private void ToggleOptions()
        {
            if (optionsPanel == null) return;
            bool show = !optionsPanel.activeSelf;
            optionsPanel.SetActive(show);
            if (!show) return;

            CanvasGroup group = optionsPanel.GetComponent<CanvasGroup>();
            if (group == null) group = optionsPanel.AddComponent<CanvasGroup>();
            RectTransform rect = optionsPanel.GetComponent<RectTransform>();
            rect.DOKill();
            group.DOKill();
            rect.localScale = Vector3.one * 0.94f;
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
            RefreshLoadout(false);
        }

        private void RefreshLanguage()
        {
            bool es = GameLoadout.IsSpanish;
            if (subtitleText != null) subtitleText.text = es ? "ELIGE TU CAZADOR Y PREPARA EL COMBATE" : "CHOOSE YOUR HUNTER AND PREPARE FOR BATTLE";
            if (characterLabel != null) characterLabel.text = es ? "PERSONAJE" : "CHARACTER";
            if (loadoutTitle != null) loadoutTitle.text = es ? "ESTILO DE COMBATE" : "COMBAT STYLE";
            if (weaponLabel != null) weaponLabel.text = es ? "ARMA INICIAL" : "STARTING WEAPON";
            if (startLabel != null) startLabel.text = es ? "JUGAR" : "PLAY";
            if (optionsLabel != null) optionsLabel.text = es ? "OPCIONES" : "OPTIONS";
            if (optionsTitle != null) optionsTitle.text = es ? "OPCIONES" : "OPTIONS";
            if (volumeLabel != null) volumeLabel.text = es ? "VOLUMEN" : "VOLUME";
            if (brightnessLabel != null) brightnessLabel.text = es ? "BRILLO" : "BRIGHTNESS";
            if (languageLabel != null) languageLabel.text = es ? "IDIOMA" : "LANGUAGE";
            if (languageValue != null) languageValue.text = es ? "ESPAÑOL" : "ENGLISH";
            UpdateSettingValues();
        }

        private void RefreshWeaponButtons(bool spanish)
        {
            PlayerWeapon[] weapons =
            {
                PlayerWeapon.Sword,
                PlayerWeapon.Bow,
                PlayerWeapon.MageStaff,
                PlayerWeapon.HealerStaff
            };

            for (int index = 0; index < weaponButtons.Length; index++)
            {
                Button button = weaponButtons[index];
                if (button == null) continue;

                PlayerWeapon weapon = weapons[index];
                TMP_Text text = button.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = WeaponShortName(weapon, spanish);

                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    Color selectedColor = GameLoadout.Weapon == weapon ? GameLoadout.WeaponColor : Brick;
                    image.color = selectedColor;
                }
            }
        }

        private static string WeaponShortName(PlayerWeapon weapon, bool spanish)
        {
            switch (weapon)
            {
                case PlayerWeapon.Bow: return spanish ? "ARCO" : "BOW";
                case PlayerWeapon.MageStaff: return spanish ? "MAGO" : "MAGE";
                case PlayerWeapon.HealerStaff: return "HEALER";
                default: return spanish ? "ESPADA" : "SWORD";
            }
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

        private void CaptureLegacyStyle()
        {
            Button start = FindButton("StartButton");
            if (start == null) return;
            Image image = start.GetComponent<Image>();
            if (image != null) buttonSprite = image.sprite;
            TMP_Text label = start.GetComponentInChildren<TMP_Text>();
            if (label != null) menuFont = label.font;
        }

        private static void HideLegacyMenuObjects()
        {
            HideObject("Buttons");
            HideObject("Player");
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            foreach (TMP_Text text in texts)
            {
                if (text != null && text.text.IndexOf("Kill The Spike", StringComparison.OrdinalIgnoreCase) >= 0)
                    text.gameObject.SetActive(false);
            }
        }

        private static void HideObject(string objectName)
        {
            GameObject found = GameObject.Find(objectName);
            if (found != null) found.SetActive(false);
        }

        private Button CreateButton(string name, RectTransform parent, Vector2 min, Vector2 max, string label, Action action)
        {
            RectTransform rect = CreateImage(name, parent, min, max, Brick);
            Image image = rect.GetComponent<Image>();
            if (buttonSprite != null)
            {
                image.sprite = buttonSprite;
                image.type = Image.Type.Sliced;
            }

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = BrickHover;
            colors.pressedColor = new Color(0.34f, 0.1f, 0.07f, 1f);
            colors.selectedColor = BrickHover;
            colors.disabledColor = new Color(0.18f, 0.1f, 0.08f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                Punch(button.transform);
                action?.Invoke();
            });

            TMP_Text text = CreateText("Label", rect, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f),
                label, 19f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            text.outlineColor = Color.black;
            text.outlineWidth = 0.2f;
            text.raycastTarget = false;
            return button;
        }

        private RectTransform CreateFramedPanel(string name, RectTransform parent, Vector2 min, Vector2 max, Color fill)
        {
            RectTransform border = CreateImage(name + " Border", parent, min, max, Border);
            AddImageShadow(border.GetComponent<Image>(), new Color(0f, 0f, 0f, 0.62f), new Vector2(7f, -7f));
            return CreateImage(name, border, new Vector2(0.006f, 0.009f), new Vector2(0.994f, 0.991f), fill);
        }

        private TMP_Text CreateText(string name, RectTransform parent, Vector2 min, Vector2 max, string value,
            float size, Color color, TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, min, max);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (menuFont != null) text.font = menuFont;
            text.text = value;
            text.fontSize = size;
            text.fontSizeMin = Mathf.Max(9f, size * 0.58f);
            text.fontSizeMax = size;
            text.enableAutoSizing = true;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static RectTransform CreateImage(string name, RectTransform parent, Vector2 min, Vector2 max, Color color)
        {
            GameObject imageObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, min, max);
            rect.GetComponent<Image>().color = color;
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

        private static void AddTextShadow(TMP_Text text, Color color, Vector2 distance)
        {
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }

        private static void AddImageShadow(Image image, Color color, Vector2 distance)
        {
            if (image == null) return;
            Shadow shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }

        private static void PrepareLegacyArtForPixelRendering(GameObject canvasObject)
        {
            if (canvasObject == null) return;
            foreach (Image image in canvasObject.GetComponentsInChildren<Image>(true))
            {
                ConfigurePixelTexture(image != null && image.sprite != null ? image.sprite.texture : null);
            }
            foreach (RawImage image in canvasObject.GetComponentsInChildren<RawImage>(true))
            {
                ConfigurePixelTexture(image != null ? image.texture as Texture2D : null);
            }
        }

        private static void ConfigurePixelTexture(Texture2D texture)
        {
            if (texture == null) return;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = 0;
        }

        private static Button FindButton(string objectName)
        {
            GameObject found = GameObject.Find(objectName);
            return found != null ? found.GetComponent<Button>() : null;
        }

        private static void Punch(Transform target)
        {
            if (target == null) return;
            target.DOKill();
            target.DOPunchScale(Vector3.one * 0.075f, 0.16f, 4, 0.6f)
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

            GameObject canvasObject = new("Global Brightness Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            RectTransform imageRect = CreateImage("Brightness Filter", canvasObject.GetComponent<RectTransform>(),
                Vector2.zero, Vector2.one, Color.clear);
            brightnessOverlay = imageRect.GetComponent<Image>();
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
