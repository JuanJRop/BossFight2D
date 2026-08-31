using TMPro;
using Project.Scripts.Controller;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Project.Scripts.Progression
{
    public sealed class RunProgressionUI : MonoBehaviour
    {
        private static RunProgressionUI instance;

        private static readonly Color HudColor = new(0.07f, 0.02f, 0.018f, 0.94f);
        private static readonly Color FrameColor = new(0.64f, 0.28f, 0.12f, 1f);
        private static readonly Color ButtonColor = new(0.42f, 0.14f, 0.1f, 1f);
        private static readonly Color ButtonHoverColor = new(0.72f, 0.28f, 0.13f, 1f);
        private static readonly Color CreamColor = new(1f, 0.9f, 0.7f, 1f);
        private static readonly Color MutedColor = new(0.78f, 0.61f, 0.5f, 1f);
        private static readonly Color AccentColor = new(0.22f, 0.92f, 1f, 1f);

        private GameObject overlayRoot;
        private Image experienceFill;
        private TMP_Text levelText;
        private TMP_Text experienceText;
        private TMP_Text abilitiesText;
        private TMP_Text overlayTitle;
        private TMP_Text overlaySubtitle;
        private Button[] choiceButtons;

        public static RunProgressionUI Ensure()
        {
            if (instance != null) return instance;

            GameObject uiObject = new("Run Progression UI");
            return uiObject.AddComponent<RunProgressionUI>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            EnsureEventSystem();
            BuildInterface();
            RunSession.OnProgressionChanged += Refresh;
            RunSession.OnLevelUpChoicesChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            RunSession.OnProgressionChanged -= Refresh;
            RunSession.OnLevelUpChoicesChanged -= Refresh;
            if (instance == this) instance = null;
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new("Progression Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 340;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            GameObject hud = CreatePanel("Progression HUD", canvasRect,
                new Vector2(0.29f, 0.025f), new Vector2(0.71f, 0.125f), HudColor, false);
            RectTransform hudRect = hud.transform as RectTransform;

            levelText = CreateText("Level", hudRect, new Vector2(0.025f, 0.2f),
                new Vector2(0.22f, 0.84f), string.Empty, 24f, CreamColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            experienceText = CreateText("Experience", hudRect, new Vector2(0.235f, 0.65f),
                new Vector2(0.97f, 0.93f), string.Empty, 15f, MutedColor,
                TextAlignmentOptions.Left, FontStyles.Bold);

            GameObject experienceTrack = CreatePanel("Experience Track", hudRect,
                new Vector2(0.235f, 0.3f), new Vector2(0.97f, 0.58f),
                new Color(0.16f, 0.06f, 0.05f, 1f), false);
            GameObject fillObject = CreatePanel("Experience Fill", experienceTrack.transform as RectTransform,
                Vector2.zero, new Vector2(0f, 1f), AccentColor, false);
            experienceFill = fillObject.GetComponent<Image>();

            abilitiesText = CreateText("Abilities", hudRect, new Vector2(0.235f, 0.04f),
                new Vector2(0.97f, 0.25f), string.Empty, 13f, MutedColor,
                TextAlignmentOptions.Left);

            overlayRoot = CreatePanel("Level Up Overlay", canvasRect, Vector2.zero, Vector2.one,
                new Color(0.015f, 0.004f, 0.005f, 0.84f), true);
            GameObject frame = CreatePanel("Level Up Frame", overlayRoot.transform as RectTransform,
                new Vector2(0.14f, 0.13f), new Vector2(0.86f, 0.87f), FrameColor, true);
            GameObject card = CreatePanel("Level Up Panel", frame.transform as RectTransform,
                new Vector2(0.012f, 0.016f), new Vector2(0.988f, 0.984f), HudColor, true);
            RectTransform cardRect = card.transform as RectTransform;

            overlayTitle = CreateText("Level Up Title", cardRect, new Vector2(0.08f, 0.84f),
                new Vector2(0.92f, 0.96f), string.Empty, 42f, CreamColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            overlaySubtitle = CreateText("Level Up Subtitle", cardRect, new Vector2(0.1f, 0.75f),
                new Vector2(0.9f, 0.83f), string.Empty, 17f, MutedColor,
                TextAlignmentOptions.Center);

            choiceButtons = new Button[5];
            Vector2[] choiceMins =
            {
                new Vector2(0.08f, 0.54f), new Vector2(0.52f, 0.54f),
                new Vector2(0.08f, 0.35f), new Vector2(0.52f, 0.35f),
                new Vector2(0.3f, 0.16f)
            };
            Vector2[] choiceMaxs =
            {
                new Vector2(0.48f, 0.69f), new Vector2(0.92f, 0.69f),
                new Vector2(0.48f, 0.50f), new Vector2(0.92f, 0.50f),
                new Vector2(0.7f, 0.31f)
            };
            for (int index = 0; index < choiceButtons.Length; index++)
            {
                int capturedIndex = index;
                choiceButtons[index] = CreateButton($"Upgrade Choice {index + 1}", cardRect,
                    choiceMins[index], choiceMaxs[index],
                    string.Empty, () => SelectChoice(capturedIndex), true);
            }
            overlayRoot.SetActive(false);
        }

        private void Refresh()
        {
            if (levelText == null) return;
            bool spanish = GameLoadout.IsSpanish;

            levelText.text = spanish ? $"NIVEL {RunSession.Level}" : $"LEVEL {RunSession.Level}";
            experienceText.text = spanish
                ? $"EXP  {RunSession.Experience} / {RunSession.ExperienceToNextLevel}"
                : $"XP  {RunSession.Experience} / {RunSession.ExperienceToNextLevel}";
            abilitiesText.text = RunSession.GetAbilitySummary(spanish);

            float normalized = RunSession.ExperienceToNextLevel > 0
                ? (float)RunSession.Experience / RunSession.ExperienceToNextLevel
                : 0f;
            if (experienceFill != null)
            {
                RectTransform fillRect = experienceFill.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }

            if (RunSession.HasPendingLevelUp)
            {
                UpdateLevelUpChoices(spanish);
                if (!overlayRoot.activeSelf) OpenLevelUp();
            }
            else if (overlayRoot.activeSelf)
            {
                CloseLevelUp();
            }
        }

        private void UpdateLevelUpChoices(bool spanish)
        {
            overlayTitle.text = spanish ? "NUEVA HABILIDAD" : "NEW ABILITY";
            overlaySubtitle.text = spanish
                ? $"NIVEL {RunSession.Level}  |  ELIGE LIBREMENTE"
                : $"LEVEL {RunSession.Level}  |  CHOOSE FREELY";

            for (int index = 0; index < choiceButtons.Length; index++)
            {
                bool available = index < RunSession.CurrentAbilityChoices.Count;
                choiceButtons[index].gameObject.SetActive(available);
                if (!available) continue;

                RunAbilityType ability = RunSession.CurrentAbilityChoices[index];
                TMP_Text label = choiceButtons[index].GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    int currentRank = RunSession.GetAbilityRank(ability);
                    string rankText = currentRank >= RunSession.MaximumAbilityRank
                        ? (spanish ? $"NIVEL MAXIMO {currentRank}" : $"MAX RANK {currentRank}")
                        : currentRank > 0
                        ? (spanish ? $"NIVEL ACTUAL {currentRank}  >  {currentRank + 1}" :
                            $"CURRENT RANK {currentRank}  >  {currentRank + 1}")
                        : (spanish ? "NUEVA HABILIDAD  >  NIVEL 1" : "NEW ABILITY  >  RANK 1");
                    label.text = $"{RunSession.GetAbilityName(ability, spanish)}\n" +
                        $"{RunSession.GetAbilityDescription(ability, spanish)}\n{rankText}";
                }
            }
        }

        private void SelectChoice(int index)
        {
            if (index < 0 || index >= RunSession.CurrentAbilityChoices.Count) return;
            RunSession.SelectAbility(RunSession.CurrentAbilityChoices[index]);
        }

        private void OpenLevelUp()
        {
            overlayRoot.SetActive(true);
            if (UIManager.instance != null) UIManager.instance.IsPaused = true;
            Time.timeScale = 0f;

            if (EventSystem.current == null || choiceButtons == null || choiceButtons.Length == 0) return;
            Button first = choiceButtons[0];
            if (first == null || !first.gameObject.activeSelf) return;
            EventSystem.current.SetSelectedGameObject(first.gameObject);
            first.Select();
        }

        private void CloseLevelUp()
        {
            overlayRoot.SetActive(false);
            if (UIManager.instance != null) UIManager.instance.IsPaused = false;
            Time.timeScale = 1f;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventSystemObject = new("Progression EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject CreatePanel(string objectName, RectTransform parent,
            Vector2 anchorMinimum, Vector2 anchorMaximum, Color color, bool raycastTarget)
        {
            GameObject panel = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMinimum;
            rect.anchorMax = anchorMaximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return panel;
        }

        private static TMP_Text CreateText(string objectName, RectTransform parent,
            Vector2 anchorMinimum, Vector2 anchorMaximum, string value, float fontSize, Color color,
            TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
        {
            GameObject textObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMinimum;
            rect.anchorMax = anchorMaximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontSizeMin = Mathf.Max(9f, fontSize * 0.55f);
            text.fontSizeMax = fontSize;
            text.enableAutoSizing = true;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string objectName, RectTransform parent,
            Vector2 anchorMinimum, Vector2 anchorMaximum, string label, UnityEngine.Events.UnityAction action,
            bool wrapText)
        {
            GameObject buttonObject = CreatePanel(objectName, parent, anchorMinimum, anchorMaximum,
                ButtonColor, true);
            Image image = buttonObject.GetComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHoverColor;
            colors.selectedColor = ButtonHoverColor;
            colors.pressedColor = new Color(0.26f, 0.07f, 0.05f, 1f);
            colors.disabledColor = new Color(0.22f, 0.1f, 0.08f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            TMP_Text text = CreateText("Label", buttonObject.transform as RectTransform,
                new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.92f), label,
                wrapText ? 19f : 15f, Color.white, TextAlignmentOptions.Center,
                FontStyles.Bold);
            text.textWrappingMode = wrapText ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            return button;
        }
    }
}
