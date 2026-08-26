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
        private Image[] abilityOrbs;
        private Sprite radialSprite;
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
        private TMP_Text abilityLabel;
        private TMP_Text abilityValue;
        private TMP_Text abilityDescription;
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
            if (radialSprite != null)
            {
                Texture2D texture = radialSprite.texture;
                Destroy(radialSprite);
                if (texture != null) Destroy(texture);
            }
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
            Time.timeScale = 1f;
            SceneManager.LoadScene(1);
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

            abilityOrbs = new Image[3];
            for (int i = 0; i < abilityOrbs.Length; i++)
            {
                RectTransform orb = CreateImage("Ability Orb " + (i + 1), previewFrame,
                    new Vector2(0.68f, 0.56f), new Vector2(0.84f, 0.72f), Color.white);
                abilityOrbs[i] = orb.GetComponent<Image>();
                abilityOrbs[i].sprite = GetRadialSprite();
                abilityOrbs[i].preserveAspect = true;
                abilityOrbs[i].raycastTarget = false;
            }

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

            weaponLabel = CreateText("Weapon Label", card, new Vector2(0.1f, 0.72f),
                new Vector2(0.9f, 0.80f), string.Empty, 15f, Muted, TextAlignmentOptions.Center);
            weaponValue = CreateSelector(card, 0.56f, () => ChangeWeapon(-1), () => ChangeWeapon(1));

            RectTransform divider = CreateImage("Divider", card, new Vector2(0.1f, 0.49f),
                new Vector2(0.9f, 0.495f), new Color(Border.r, Border.g, Border.b, 0.75f));
            divider.GetComponent<Image>().raycastTarget = false;

            abilityLabel = CreateText("Ability Label", card, new Vector2(0.1f, 0.38f),
                new Vector2(0.9f, 0.46f), string.Empty, 15f, Muted, TextAlignmentOptions.Center);
            abilityValue = CreateSelector(card, 0.22f, () => ChangeAbility(-1), () => ChangeAbility(1));
            abilityDescription = CreateText("Ability Description", card, new Vector2(0.12f, 0.065f),
                new Vector2(0.88f, 0.19f), string.Empty, 14f, Muted, TextAlignmentOptions.Center);
            abilityDescription.textWrappingMode = TextWrappingModes.Normal;
        }

        private TMP_Text CreateSelector(RectTransform parent, float y, Action previous, Action next)
        {
            CreateButton("Previous", parent, new Vector2(0.1f, y), new Vector2(0.245f, y + 0.12f), "<", previous);
            RectTransform valueFrame = CreateFramedPanel("Selected Value", parent,
                new Vector2(0.27f, y), new Vector2(0.73f, y + 0.12f), Ink);
            TMP_Text value = CreateText("Value", valueFrame, new Vector2(0.045f, 0.08f),
                new Vector2(0.955f, 0.92f), string.Empty, 19f, Cream,
                TextAlignmentOptions.Center, FontStyles.Bold);
            CreateButton("Next", parent, new Vector2(0.755f, y), new Vector2(0.9f, y + 0.12f), ">", next);
            return value;
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

        private void ChangeWeapon(int direction)
        {
            GameLoadout.CycleWeapon(direction);
            RefreshLoadout(false);
            Punch(weaponValue.transform);
        }

        private void ChangeAbility(int direction)
        {
            GameLoadout.CycleAbility(direction);
            RefreshLoadout(false);
            Punch(abilityValue.transform);
        }

        private void RefreshLoadout(bool animateCharacter)
        {
            bool es = GameLoadout.IsSpanish;
            if (characterValue != null) characterValue.text = GameLoadout.CharacterName(es);
            if (characterRole != null) characterRole.text = GameLoadout.CharacterRole(es);
            if (weaponValue != null) weaponValue.text = GameLoadout.WeaponName(es);
            if (abilityValue != null) abilityValue.text = GameLoadout.AbilityName(es);
            if (abilityDescription != null) abilityDescription.text = GameLoadout.AbilityDescription(es);

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

            ApplyAbilityPreview();
            UpdateSettingValues();
        }

        private void ApplyAbilityPreview()
        {
            if (abilityOrbs == null) return;
            Color color = GameLoadout.AbilityColor;
            int count = Mathf.Clamp(GameLoadout.AbilityProjectileCount, 1, abilityOrbs.Length);
            Vector2[] positions = GameLoadout.Ability switch
            {
                PlayerAbility.PrismBurst => new[] { new Vector2(0.18f, 0.62f), new Vector2(0.74f, 0.68f), new Vector2(0.71f, 0.24f) },
                PlayerAbility.SeekerCore => new[] { new Vector2(0.18f, 0.58f), new Vector2(0.73f, 0.56f), new Vector2(0.73f, 0.56f) },
                _ => new[] { new Vector2(0.72f, 0.58f), new Vector2(0.72f, 0.58f), new Vector2(0.72f, 0.58f) }
            };

            for (int i = 0; i < abilityOrbs.Length; i++)
            {
                Image orb = abilityOrbs[i];
                orb.rectTransform.DOKill();
                bool active = i < count;
                orb.gameObject.SetActive(active);
                if (!active) continue;
                orb.color = new Color(color.r, color.g, color.b, i == 0 ? 0.95f : 0.78f);
                RectTransform rect = orb.rectTransform;
                Vector2 size = GameLoadout.Ability == PlayerAbility.ChargedRound
                    ? new Vector2(0.19f, 0.19f)
                    : new Vector2(0.14f, 0.14f);
                rect.anchorMin = positions[i];
                rect.anchorMax = positions[i] + size;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.DOScale(1.16f, 0.62f + i * 0.08f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true)
                    .SetLink(orb.gameObject, LinkBehaviour.KillOnDestroy);
            }
        }

        private Sprite GetRadialSprite()
        {
            if (radialSprite != null) return radialSprite;
            const int size = 64;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Ability Preview Glow"
            };
            Color[] pixels = new Color[size * size];
            Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            radialSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 100f);
            return radialSprite;
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
            if (loadoutTitle != null) loadoutTitle.text = es ? "EQUIPAMIENTO" : "LOADOUT";
            if (weaponLabel != null) weaponLabel.text = es ? "ARMA" : "WEAPON";
            if (abilityLabel != null) abilityLabel.text = es ? "HABILIDAD" : "ABILITY";
            if (startLabel != null) startLabel.text = es ? "JUGAR" : "PLAY";
            if (optionsLabel != null) optionsLabel.text = es ? "OPCIONES" : "OPTIONS";
            if (optionsTitle != null) optionsTitle.text = es ? "OPCIONES" : "OPTIONS";
            if (volumeLabel != null) volumeLabel.text = es ? "VOLUMEN" : "VOLUME";
            if (brightnessLabel != null) brightnessLabel.text = es ? "BRILLO" : "BRIGHTNESS";
            if (languageLabel != null) languageLabel.text = es ? "IDIOMA" : "LANGUAGE";
            if (languageValue != null) languageValue.text = es ? "ESPAÑOL" : "ENGLISH";
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
