using System.Collections;
using TMPro;
using Project.Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Scripts.Progression
{
    public sealed class RunProgressionUI : MonoBehaviour
    {
        private static RunProgressionUI instance;

        private static readonly Color HudColor = new(0.045f, 0.018f, 0.025f, 0.86f);
        private static readonly Color FrameColor = new(0.6f, 0.25f, 0.14f, 0.88f);
        private static readonly Color CreamColor = new(1f, 0.9f, 0.7f, 1f);
        private static readonly Color MutedColor = new(0.78f, 0.61f, 0.5f, 1f);
        private static readonly Color AccentColor = new(0.22f, 0.92f, 1f, 1f);

        private Image experienceFill;
        private TMP_Text levelText;
        private TMP_Text experienceText;
        private TMP_Text classText;
        private TMP_Text skillText;
        private TMP_Text pointsText;
        private CanvasGroup levelUpGroup;
        private RectTransform levelUpPanel;
        private TMP_Text levelUpTitle;
        private TMP_Text levelUpDetail;
        private Coroutine levelUpRoutine;

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
            BuildInterface();
            RunSession.OnProgressionChanged += Refresh;
            RunSession.OnLevelUp += ShowLevelUp;
            Refresh();
        }

        private void OnDestroy()
        {
            RunSession.OnProgressionChanged -= Refresh;
            RunSession.OnLevelUp -= ShowLevelUp;
            if (instance == this) instance = null;
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new("Progression Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler));
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
                new Vector2(0.018f, 0.018f), new Vector2(0.35f, 0.11f), FrameColor, false);
            RectTransform hudRect = CreatePanel("Progression HUD Content", hud.transform as RectTransform,
                new Vector2(0.008f, 0.014f), new Vector2(0.992f, 0.986f), HudColor, false)
                .transform as RectTransform;

            levelText = CreateText("Level", hudRect, new Vector2(0.018f, 0.2f),
                new Vector2(0.17f, 0.86f), string.Empty, 16f, CreamColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            experienceText = CreateText("Experience", hudRect, new Vector2(0.185f, 0.63f),
                new Vector2(0.78f, 0.91f), string.Empty, 10f, MutedColor,
                TextAlignmentOptions.Left, FontStyles.Bold);

            GameObject experienceTrack = CreatePanel("Experience Track", hudRect,
                new Vector2(0.185f, 0.38f), new Vector2(0.78f, 0.56f),
                new Color(0.15f, 0.055f, 0.06f, 1f), false);
            GameObject fillObject = CreatePanel("Experience Fill", experienceTrack.transform as RectTransform,
                Vector2.zero, new Vector2(0f, 1f), AccentColor, false);
            experienceFill = fillObject.GetComponent<Image>();

            classText = CreateText("Active Class", hudRect, new Vector2(0.185f, 0.08f),
                new Vector2(0.51f, 0.31f), string.Empty, 10f, CreamColor,
                TextAlignmentOptions.Left, FontStyles.Bold);
            skillText = CreateText("Active Skills", hudRect, new Vector2(0.52f, 0.08f),
                new Vector2(0.78f, 0.31f), string.Empty, 9f, MutedColor,
                TextAlignmentOptions.Left);
            pointsText = CreateText("Skill Points", hudRect, new Vector2(0.8f, 0.16f),
                new Vector2(0.98f, 0.78f), string.Empty, 10f, AccentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject levelUpObject = CreatePanel("Level Up Feedback", canvasRect,
                new Vector2(0.38f, 0.84f), new Vector2(0.62f, 0.935f),
                new Color(0.07f, 0.025f, 0.03f, 0.94f), false);
            levelUpPanel = levelUpObject.transform as RectTransform;
            levelUpGroup = levelUpObject.AddComponent<CanvasGroup>();
            levelUpGroup.alpha = 0f;
            levelUpGroup.interactable = false;
            levelUpGroup.blocksRaycasts = false;
            levelUpTitle = CreateText("Level Up Title", levelUpPanel,
                new Vector2(0.04f, 0.48f), new Vector2(0.96f, 0.94f), string.Empty,
                18f, CreamColor, TextAlignmentOptions.Center, FontStyles.Bold);
            levelUpDetail = CreateText("Level Up Detail", levelUpPanel,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.46f), string.Empty,
                9f, AccentColor, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        private void Refresh()
        {
            if (levelText == null) return;
            bool spanish = GameLoadout.IsSpanish;
            RunClassType classType = RunSession.GetCombatClass();
            levelText.text = (spanish ? "NIVEL\n" : "LEVEL\n") + RunSession.Level;
            experienceText.text = (spanish ? "EXP  " : "XP  ") + RunSession.Experience + " / " +
                RunSession.ExperienceToNextLevel;
            classText.text = RunSession.GetClassName(classType, spanish);
            classText.color = RunSession.GetClassColor(classType);
            skillText.text = RunSession.AllocatedSkillPoints > 0
                ? (spanish ? "BUILD ACTIVA" : "ACTIVE BUILD")
                : (spanish ? "P  ABRIR ARBOL" : "P  OPEN TREE");
            pointsText.text = (spanish ? "PUNTOS\n" : "POINTS\n") + RunSession.AvailableSkillPoints;

            float normalized = RunSession.ExperienceToNextLevel > 0
                ? (float)RunSession.Experience / RunSession.ExperienceToNextLevel
                : 0f;
            RectTransform fillRect = experienceFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        private void ShowLevelUp(int level)
        {
            if (levelUpGroup == null) return;
            if (levelUpRoutine != null) StopCoroutine(levelUpRoutine);
            levelUpRoutine = StartCoroutine(PlayLevelUpFeedback(level));
        }

        private IEnumerator PlayLevelUpFeedback(int level)
        {
            bool spanish = GameLoadout.IsSpanish;
            levelUpTitle.text = spanish ? "SUBIDA DE NIVEL" : "LEVEL UP";
            levelUpDetail.text = spanish
                ? $"NIVEL {level}  |  PUNTO DE HABILIDAD +1"
                : $"LEVEL {level}  |  SKILL POINT +1";
            levelUpGroup.alpha = 1f;
            levelUpPanel.localScale = Vector3.one * 0.82f;

            float elapsed = 0f;
            while (elapsed < 0.24f)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / 0.24f);
                levelUpPanel.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f,
                    Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.85f);

            elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                levelUpGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.5f);
                yield return null;
            }

            levelUpGroup.alpha = 0f;
            levelUpRoutine = null;
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
            text.fontSizeMin = Mathf.Max(8f, fontSize * 0.56f);
            text.fontSizeMax = fontSize;
            text.enableAutoSizing = true;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
    }
}
