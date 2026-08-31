using Project.Scripts.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Scripts.Controller
{
    public sealed class EscapeMenuController : MonoBehaviour
    {
        private static EscapeMenuController instance;

        private readonly Color panelColor = new(0.09f, 0.035f, 0.028f, 0.97f);
        private readonly Color borderColor = new(0.56f, 0.24f, 0.14f, 1f);
        private readonly Color buttonColor = new(0.49f, 0.17f, 0.11f, 1f);
        private readonly Color buttonHoverColor = new(0.72f, 0.31f, 0.17f, 1f);
        private readonly Color creamColor = new(0.98f, 0.92f, 0.82f, 1f);
        private readonly Color mutedColor = new(0.74f, 0.61f, 0.51f, 1f);

        private GameObject menuRoot;
        private Button firstButton;
        private bool isOpen;

        public static bool IsOpen => instance != null && instance.isOpen;

        public static void Close()
        {
            instance?.SetOpen(false);
        }

        public static void Toggle()
        {
            if (RunSession.HasPendingAbilityChoice) return;

            if (instance == null)
            {
                GameObject menuObject = new("Escape Pause Menu");
                instance = menuObject.AddComponent<EscapeMenuController>();
            }

            instance.SetOpen(!instance.isOpen);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            BuildMenu();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void BuildMenu()
        {
            EnsureEventSystem();

            GameObject canvasObject = new("Pause Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 450;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            menuRoot = CreatePanel("Pause Overlay", canvasObject.transform as RectTransform,
                Vector2.zero, Vector2.one, new Color(0.015f, 0.006f, 0.005f, 0.78f), true);
            RectTransform panel = CreatePanel("Pause Panel", menuRoot.transform as RectTransform,
                new Vector2(0.34f, 0.19f), new Vector2(0.66f, 0.81f), borderColor, false)
                .GetComponent<RectTransform>();
            RectTransform content = CreatePanel("Pause Panel Content", panel,
                new Vector2(0.012f, 0.016f), new Vector2(0.988f, 0.984f), panelColor, false)
                .GetComponent<RectTransform>();

            CreateText("Pause Title", content, new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.92f),
                "PAUSA", 42f, creamColor, TextAlignmentOptions.Center, FontStyles.Bold);
            CreateText("Pause Subtitle", content, new Vector2(0.1f, 0.67f), new Vector2(0.9f, 0.75f),
                GameLoadout.IsSpanish ? "La partida esta en pausa" : "The run is paused",
                16f, mutedColor, TextAlignmentOptions.Center);

            CreateButton("Character Status", content, new Vector2(0.16f, 0.50f),
                new Vector2(0.84f, 0.61f), GameLoadout.IsSpanish ? "PERSONAJE (P)" : "CHARACTER (P)",
                CharacterStatusMenu.Toggle);
            firstButton = CreateButton("Continue", content, new Vector2(0.16f, 0.35f),
                new Vector2(0.84f, 0.46f), GameLoadout.IsSpanish ? "CONTINUAR" : "RESUME", Resume);
            CreateButton("Restart Run", content, new Vector2(0.16f, 0.20f),
                new Vector2(0.84f, 0.31f), GameLoadout.IsSpanish ? "REINICIAR PARTIDA" : "RESTART RUN", RestartRun);
            CreateButton("Back To Menu", content, new Vector2(0.16f, 0.05f),
                new Vector2(0.84f, 0.16f), GameLoadout.IsSpanish ? "SALIR AL MENU" : "BACK TO MENU", BackToMenu);

            CreateText("Pause Hint", content, new Vector2(0.12f, 0.005f), new Vector2(0.88f, 0.045f),
                GameLoadout.IsSpanish ? "P  PERSONAJE  |  ESC  CERRAR" : "P  CHARACTER  |  ESC  CLOSE",
                13f, mutedColor, TextAlignmentOptions.Center);
            menuRoot.SetActive(false);
        }

        private void SetOpen(bool open)
        {
            isOpen = open;
            if (menuRoot != null) menuRoot.SetActive(open);
            if (UIManager.instance != null) UIManager.instance.IsPaused = open;
            Time.timeScale = open ? 0f : 1f;

            if (!open || EventSystem.current == null || firstButton == null) return;
            EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
            firstButton.Select();
        }

        private void Resume()
        {
            SetOpen(false);
        }

        private void RestartRun()
        {
            Time.timeScale = 1f;
            if (UIManager.instance != null) UIManager.instance.IsPaused = false;
            RunSession.BeginNewRun();
            PlayerPrefs.Save();
            SceneManager.LoadScene("WorldPath");
        }

        private void BackToMenu()
        {
            Time.timeScale = 1f;
            if (UIManager.instance != null) UIManager.instance.IsPaused = false;
            SceneManager.LoadScene(0);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            GameObject eventSystemObject = new("Pause Menu EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private Button CreateButton(string objectName, RectTransform parent, Vector2 anchorMinimum,
            Vector2 anchorMaximum, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = CreatePanel(objectName, parent, anchorMinimum, anchorMaximum, buttonColor, true);
            Image image = buttonObject.GetComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = buttonHoverColor;
            colors.selectedColor = buttonHoverColor;
            colors.pressedColor = new Color(0.3f, 0.08f, 0.05f, 1f);
            colors.disabledColor = new Color(0.2f, 0.1f, 0.08f, 0.65f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);
            CreateText("Label", buttonObject.transform as RectTransform, new Vector2(0.04f, 0.06f),
                new Vector2(0.96f, 0.94f), label, 19f, Color.white, TextAlignmentOptions.Center,
                FontStyles.Bold);
            return button;
        }

        private static GameObject CreatePanel(string objectName, RectTransform parent, Vector2 anchorMinimum,
            Vector2 anchorMaximum, Color color, bool raycastTarget)
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

        private static TMP_Text CreateText(string objectName, RectTransform parent, Vector2 anchorMinimum,
            Vector2 anchorMaximum, string value, float fontSize, Color color,
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
            text.fontSizeMin = Mathf.Max(9f, fontSize * 0.58f);
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
