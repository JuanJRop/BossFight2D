using System;
using System.Collections.Generic;
using TMPro;
using Project.Scripts.Controller;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Project.Scripts.Progression
{
    public sealed class SkillTreeMenu : MonoBehaviour
    {
        private static readonly RunClassType[] Classes =
        {
            RunClassType.Warrior,
            RunClassType.Archer,
            RunClassType.Mage,
            RunClassType.Healer
        };

        private static SkillTreeMenu instance;

        private readonly Color backgroundColor = new(0.012f, 0.006f, 0.008f, 0.94f);
        private readonly Color panelColor = new(0.055f, 0.025f, 0.03f, 0.98f);
        private readonly Color panelAltColor = new(0.09f, 0.04f, 0.045f, 0.98f);
        private readonly Color creamColor = new(1f, 0.93f, 0.8f, 1f);
        private readonly Color mutedColor = new(0.76f, 0.62f, 0.53f, 1f);
        private readonly Color accentColor = new(0.2f, 0.92f, 1f, 1f);
        private readonly Color lockedColor = new(0.24f, 0.16f, 0.16f, 1f);

        private readonly Button[] classButtons = new Button[Classes.Length];
        private readonly TMP_Text[] classButtonLabels = new TMP_Text[Classes.Length];
        private readonly Button[,] skillButtons = new Button[Classes.Length, 4];
        private readonly TMP_Text[,] skillLabels = new TMP_Text[Classes.Length, 4];

        private GameObject menuRoot;
        private Button closeButton;
        private Image portraitImage;
        private TMP_Text characterText;
        private TMP_Text classText;
        private TMP_Text classDescriptionText;
        private TMP_Text pointsText;
        private TMP_Text combatText;
        private TMP_Text buildText;
        private bool isOpen;

        public static bool IsOpen => instance != null && instance.isOpen;

        public static void Toggle()
        {
            RunSession.EnsureRunStarted();
            if (instance == null)
            {
                GameObject menuObject = new("Skill Tree Menu");
                instance = menuObject.AddComponent<SkillTreeMenu>();
            }

            instance.SetOpen(!instance.isOpen);
        }

        public static void Close()
        {
            instance?.SetOpen(false);
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
            Refresh();
        }

        private void OnDestroy()
        {
            RunSession.OnProgressionChanged -= Refresh;
            if (instance == this) instance = null;
            if (!isOpen) return;
            if (UIManager.instance != null) UIManager.instance.IsPaused = false;
            Time.timeScale = 1f;
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new("Skill Tree Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.transform as RectTransform;
            menuRoot = CreatePanel("Skill Tree Overlay", canvasRect, Vector2.zero, Vector2.one,
                backgroundColor, true);
            RectTransform frame = CreatePanel("Skill Tree Frame", menuRoot.transform as RectTransform,
                new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f),
                new Color(0.63f, 0.24f, 0.12f, 1f), true).GetComponent<RectTransform>();
            RectTransform content = CreatePanel("Skill Tree Content", frame,
                new Vector2(0.008f, 0.012f), new Vector2(0.992f, 0.988f), panelColor, true)
                .GetComponent<RectTransform>();

            CreateText("Skill Tree Title", content, new Vector2(0.035f, 0.91f), new Vector2(0.72f, 0.985f),
                "ARBOL DE HABILIDADES", 38f, creamColor, TextAlignmentOptions.Left, FontStyles.Bold);
            CreateText("Skill Tree Subtitle", content, new Vector2(0.037f, 0.865f), new Vector2(0.74f, 0.92f),
                GameLoadout.IsSpanish ? "Elige una clase y construye tu estilo dentro de la partida." :
                    "Choose a class and build your style during the run.",
                15f, mutedColor, TextAlignmentOptions.Left);
            closeButton = CreateButton("Close Skill Tree", content, new Vector2(0.91f, 0.91f),
                new Vector2(0.97f, 0.975f), "X", Close, accentColor, 22f, false);

            RectTransform profile = CreatePanel("Skill Tree Profile", content,
                new Vector2(0.035f, 0.12f), new Vector2(0.285f, 0.84f), panelAltColor, false)
                .GetComponent<RectTransform>();
            portraitImage = CreateImage("Skill Tree Portrait", profile, new Vector2(0.19f, 0.56f),
                new Vector2(0.81f, 0.9f), new Color(0.02f, 0.045f, 0.06f, 1f));
            portraitImage.preserveAspect = true;
            characterText = CreateText("Skill Tree Character", profile, new Vector2(0.06f, 0.43f),
                new Vector2(0.94f, 0.55f), string.Empty, 21f, creamColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            classText = CreateText("Active Class", profile, new Vector2(0.06f, 0.31f),
                new Vector2(0.94f, 0.43f), string.Empty, 17f, accentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            classDescriptionText = CreateText("Class Description", profile, new Vector2(0.08f, 0.17f),
                new Vector2(0.92f, 0.3f), string.Empty, 13f, mutedColor,
                TextAlignmentOptions.Center);
            classDescriptionText.textWrappingMode = TextWrappingModes.Normal;
            pointsText = CreateText("Skill Points", profile, new Vector2(0.08f, 0.06f),
                new Vector2(0.92f, 0.16f), string.Empty, 14f, creamColor,
                TextAlignmentOptions.Center, FontStyles.Bold);

            RectTransform status = CreatePanel("Build Status", content,
                new Vector2(0.035f, 0.035f), new Vector2(0.285f, 0.105f), panelAltColor, false)
                .GetComponent<RectTransform>();
            combatText = CreateText("Combat Multipliers", status, new Vector2(0.04f, 0.05f),
                new Vector2(0.96f, 0.95f), string.Empty, 12f, mutedColor,
                TextAlignmentOptions.Left);
            combatText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform tree = CreatePanel("Class Skill Trees", content,
                new Vector2(0.31f, 0.12f), new Vector2(0.965f, 0.84f), panelAltColor, false)
                .GetComponent<RectTransform>();
            BuildClassTrees(tree);

            RectTransform build = CreatePanel("Current Build", content,
                new Vector2(0.31f, 0.035f), new Vector2(0.965f, 0.105f), panelAltColor, false)
                .GetComponent<RectTransform>();
            buildText = CreateText("Current Build Text", build, new Vector2(0.025f, 0.06f),
                new Vector2(0.975f, 0.94f), string.Empty, 12f, mutedColor,
                TextAlignmentOptions.Left);
            buildText.textWrappingMode = TextWrappingModes.NoWrap;

            CreateText("Skill Tree Hint", content, new Vector2(0.035f, 0.005f), new Vector2(0.965f, 0.03f),
                GameLoadout.IsSpanish ? "P  ARBOL   |   ESC  CERRAR   |   Pulsa un nodo para comprar y - para devolver." :
                    "P  TREE   |   ESC  CLOSE   |   Click a node to buy and - to refund.",
                12f, mutedColor, TextAlignmentOptions.Center);
            menuRoot.SetActive(false);
        }

        private void BuildClassTrees(RectTransform tree)
        {
            Vector2[] minimums =
            {
                new Vector2(0.018f, 0.51f),
                new Vector2(0.51f, 0.51f),
                new Vector2(0.018f, 0.018f),
                new Vector2(0.51f, 0.018f)
            };
            Vector2[] maximums =
            {
                new Vector2(0.49f, 0.982f),
                new Vector2(0.982f, 0.982f),
                new Vector2(0.49f, 0.482f),
                new Vector2(0.982f, 0.482f)
            };

            for (int classIndex = 0; classIndex < Classes.Length; classIndex++)
            {
                RunClassType classType = Classes[classIndex];
                Color classColor = RunSession.GetClassColor(classType);
                RectTransform panel = CreatePanel("Class Panel " + classType, tree,
                    minimums[classIndex], maximums[classIndex],
                    new Color(0.05f, 0.022f, 0.026f, 0.98f), false).GetComponent<RectTransform>();
                TMP_Text title = CreateText("Class Title " + classType, panel,
                    new Vector2(0.035f, 0.81f), new Vector2(0.65f, 0.96f),
                    RunSession.GetClassName(classType, GameLoadout.IsSpanish), 18f, classColor,
                    TextAlignmentOptions.Left, FontStyles.Bold);
                title.textWrappingMode = TextWrappingModes.NoWrap;
                classButtons[classIndex] = CreateButton("Select " + classType, panel,
                    new Vector2(0.67f, 0.82f), new Vector2(0.96f, 0.96f), string.Empty,
                    () => SelectClass(classType), classColor, 11f, false);
                classButtonLabels[classIndex] = classButtons[classIndex].GetComponentInChildren<TMP_Text>(true);

                IReadOnlyList<RunSkillType> skills = RunSession.GetClassSkills(classType);
                for (int skillIndex = 0; skillIndex < skills.Count; skillIndex++)
                {
                    RunSkillType skill = skills[skillIndex];
                    float top = 0.78f - skillIndex * 0.19f;
                    float bottom = top - 0.145f;
                    if (skillIndex > 0)
                    {
                        CreatePanel("Skill Link " + classType + " " + skillIndex, panel,
                            new Vector2(0.49f, bottom + 0.145f), new Vector2(0.51f, top + 0.015f),
                            new Color(classColor.r, classColor.g, classColor.b, 0.6f), false);
                    }

                    int capturedClassIndex = classIndex;
                    int capturedSkillIndex = skillIndex;
                    skillButtons[classIndex, skillIndex] = CreateButton("Skill " + skill,
                        panel, new Vector2(0.045f, bottom), new Vector2(0.8f, top),
                        string.Empty, () => PurchaseSkill(capturedClassIndex, capturedSkillIndex),
                        classColor, skillIndex == 0 ? 13f : 12f, true);
                    skillLabels[classIndex, skillIndex] =
                        skillButtons[classIndex, skillIndex].GetComponentInChildren<TMP_Text>(true);
                    skillLabels[classIndex, skillIndex].textWrappingMode = TextWrappingModes.Normal;
                    CreateButton("Refund " + skill, panel, new Vector2(0.83f, bottom),
                        new Vector2(0.955f, top), "-", () => RefundSkill(capturedClassIndex, capturedSkillIndex),
                        new Color(0.65f, 0.23f, 0.13f, 1f), 18f, false);
                }
            }
        }

        private void SelectClass(RunClassType classType)
        {
            RunSession.SelectClass(classType);
        }

        private void PurchaseSkill(int classIndex, int skillIndex)
        {
            IReadOnlyList<RunSkillType> skills = RunSession.GetClassSkills(Classes[classIndex]);
            if (skillIndex < 0 || skillIndex >= skills.Count) return;
            RunSession.PurchaseSkill(skills[skillIndex]);
        }

        private void RefundSkill(int classIndex, int skillIndex)
        {
            IReadOnlyList<RunSkillType> skills = RunSession.GetClassSkills(Classes[classIndex]);
            if (skillIndex < 0 || skillIndex >= skills.Count) return;
            RunSession.RefundSkill(skills[skillIndex]);
        }

        private void SetOpen(bool open)
        {
            if (open && EscapeMenuController.IsOpen) EscapeMenuController.Close();
            isOpen = open;
            if (menuRoot != null) menuRoot.SetActive(open);
            if (UIManager.instance != null) UIManager.instance.IsPaused = open;
            Time.timeScale = open ? 0f : 1f;
            if (!open || EventSystem.current == null) return;

            EventSystem.current.SetSelectedGameObject(closeButton != null ? closeButton.gameObject : null);
            closeButton?.Select();
            Refresh();
        }

        private void Refresh()
        {
            if (menuRoot == null) return;
            bool spanish = GameLoadout.IsSpanish;
            RunClassType activeClass = RunSession.GetCombatClass();

            characterText.text = GameLoadout.CharacterName(spanish) + "\n<size=58%>" +
                GameLoadout.CharacterRole(spanish) + "</size>";
            classText.text = (spanish ? "CLASE ACTIVA\n" : "ACTIVE CLASS\n") +
                RunSession.GetClassName(activeClass, spanish);
            classText.color = RunSession.GetClassColor(activeClass);
            classDescriptionText.text = RunSession.GetClassDescription(activeClass, spanish);
            pointsText.text = (spanish ? "PUNTOS LIBRES  " : "FREE POINTS  ") + RunSession.AvailableSkillPoints +
                "\n<size=78%>" + (spanish ? "INVERTIDOS  " : "SPENT  ") +
                RunSession.AllocatedSkillPoints + "</size>";
            combatText.text = (spanish ? "DANO x" : "DAMAGE x") + RunSession.DamageMultiplier.ToString("0.00") +
                "\n" + (spanish ? "CADENCIA x" : "FIRE RATE x") +
                (1f / Mathf.Max(0.01f, RunSession.AttackCooldownMultiplier)).ToString("0.00") +
                "\n" + (spanish ? "VIDA x" : "HEALTH x") +
                RunSession.PlayerHealthMultiplier.ToString("0.00");
            buildText.text = RunSession.GetSkillSummary(spanish);

            RefreshPortrait();
            for (int classIndex = 0; classIndex < Classes.Length; classIndex++)
            {
                RunClassType classType = Classes[classIndex];
                bool selected = classType == activeClass;
                classButtonLabels[classIndex].text = selected
                    ? (spanish ? "ACTIVA" : "ACTIVE")
                    : (spanish ? "ELEGIR" : "SELECT");
                StyleButton(classButtons[classIndex], RunSession.GetClassColor(classType), true, selected);

                IReadOnlyList<RunSkillType> skills = RunSession.GetClassSkills(classType);
                for (int skillIndex = 0; skillIndex < skills.Count; skillIndex++)
                {
                    RunSkillType skill = skills[skillIndex];
                    int rank = RunSession.GetSkillRank(skill);
                    int maxRank = RunSession.GetSkillMaxRank(skill);
                    bool unlocked = RunSession.IsSkillUnlocked(skill);
                    bool canBuy = RunSession.CanPurchaseSkill(skill);
                    skillLabels[classIndex, skillIndex].text =
                        RunSession.GetSkillName(skill, spanish) + "\n<size=74%>" +
                        (unlocked ? "RANGO " + rank + "/" + maxRank :
                            (spanish ? "BLOQUEADA" : "LOCKED")) + "</size>";
                    StyleButton(skillButtons[classIndex, skillIndex],
                        unlocked ? RunSession.GetClassColor(classType) : lockedColor, canBuy, canBuy);
                }
            }
        }

        private void RefreshPortrait()
        {
            if (portraitImage == null) return;
            Transform player = FindPlayerTransform();
            if (player == null) return;

            SpriteRenderer bestRenderer = null;
            float bestArea = 0f;
            foreach (SpriteRenderer candidate in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (candidate == null || candidate.sprite == null) continue;
                string candidateName = candidate.name;
                if (candidateName.IndexOf("gun", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    candidateName.IndexOf("weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    candidateName.IndexOf("bullet", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                float area = candidate.bounds.size.x * candidate.bounds.size.y;
                if (area <= bestArea) continue;
                bestArea = area;
                bestRenderer = candidate;
            }

            if (bestRenderer == null) return;
            portraitImage.sprite = bestRenderer.sprite;
            portraitImage.color = bestRenderer.color;
        }

        private static Transform FindPlayerTransform()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform.root : null;
        }

        private static void StyleButton(Button button, Color color, bool interactable, bool selected)
        {
            if (button == null) return;
            Color normal = interactable ? (selected ? Color.Lerp(color, Color.white, 0.16f) : color) :
                new Color(color.r * 0.42f, color.g * 0.42f, color.b * 0.42f, 0.8f);
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, Color.white, 0.28f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(normal, Color.black, 0.32f);
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.42f);
            button.colors = colors;
            button.interactable = interactable;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventSystemObject = new("Skill Tree EventSystem");
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

        private static Image CreateImage(string objectName, RectTransform parent,
            Vector2 anchorMinimum, Vector2 anchorMaximum, Color color)
        {
            return CreatePanel(objectName, parent, anchorMinimum, anchorMaximum, color, false).GetComponent<Image>();
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

        private static Button CreateButton(string objectName, RectTransform parent,
            Vector2 anchorMinimum, Vector2 anchorMaximum, string label,
            UnityEngine.Events.UnityAction action, Color color, float fontSize, bool wrapText)
        {
            GameObject buttonObject = CreatePanel(objectName, parent, anchorMinimum, anchorMaximum, color, true);
            Image image = buttonObject.GetComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.28f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(color, Color.black, 0.32f);
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.42f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            TMP_Text text = CreateText("Label", buttonObject.transform as RectTransform,
                new Vector2(0.035f, 0.06f), new Vector2(0.965f, 0.94f), label,
                fontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            text.textWrappingMode = wrapText ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            return button;
        }
    }
}
