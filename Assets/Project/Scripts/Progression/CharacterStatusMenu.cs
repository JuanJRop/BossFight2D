using System;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Movement;
using Project.Scripts.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Project.Scripts.Progression
{
    public sealed class CharacterStatusMenu : MonoBehaviour
    {
        private static readonly PlayerStatType[] Stats =
        {
            PlayerStatType.Speed,
            PlayerStatType.Strength,
            PlayerStatType.Cadence,
            PlayerStatType.Dexterity,
            PlayerStatType.Stamina
        };

        private static readonly RunAbilityType[] Abilities =
        {
            RunAbilityType.BouncingOrb,
            RunAbilityType.AutoBullets,
            RunAbilityType.ChainLaser,
            RunAbilityType.VoidNova,
            RunAbilityType.Overclock
        };

        private static CharacterStatusMenu instance;

        private readonly Color backgroundColor = new(0.015f, 0.006f, 0.005f, 0.9f);
        private readonly Color panelColor = new(0.07f, 0.022f, 0.02f, 0.98f);
        private readonly Color panelAltColor = new(0.12f, 0.04f, 0.028f, 0.98f);
        private readonly Color borderColor = new(0.72f, 0.3f, 0.12f, 1f);
        private readonly Color accentColor = new(0.2f, 0.92f, 1f, 1f);
        private readonly Color warmColor = new(1f, 0.57f, 0.18f, 1f);
        private readonly Color creamColor = new(1f, 0.92f, 0.8f, 1f);
        private readonly Color mutedColor = new(0.76f, 0.62f, 0.52f, 1f);

        private GameObject menuRoot;
        private Button closeButton;
        private readonly Button[] plusButtons = new Button[Stats.Length];
        private readonly Button[] minusButtons = new Button[Stats.Length];
        private readonly TMP_Text[] statTexts = new TMP_Text[Stats.Length];
        private TMP_Text characterText;
        private TMP_Text loadoutText;
        private TMP_Text summaryText;
        private TMP_Text abilitiesText;
        private Image portraitImage;
        private bool isOpen;

        public static bool IsOpen => instance != null && instance.isOpen;

        public static void Toggle()
        {
            if (RunSession.HasPendingAbilityChoice) return;
            RunSession.EnsureRunStarted();

            if (instance == null)
            {
                GameObject menuObject = new("Character Status Menu");
                instance = menuObject.AddComponent<CharacterStatusMenu>();
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
            if (isOpen)
            {
                if (UIManager.instance != null) UIManager.instance.IsPaused = false;
                Time.timeScale = 1f;
            }
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new("Character Status Canvas", typeof(RectTransform), typeof(Canvas),
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
            menuRoot = CreatePanel("Character Status Overlay", canvasRect, Vector2.zero, Vector2.one,
                backgroundColor, true);
            RectTransform frame = CreatePanel("Character Status Frame", menuRoot.transform as RectTransform,
                new Vector2(0.035f, 0.04f), new Vector2(0.965f, 0.96f), borderColor, true)
                .GetComponent<RectTransform>();
            RectTransform content = CreatePanel("Character Status Content", frame,
                new Vector2(0.008f, 0.012f), new Vector2(0.992f, 0.988f), panelColor, true)
                .GetComponent<RectTransform>();

            CreateText("Character Status Title", content, new Vector2(0.04f, 0.91f),
                new Vector2(0.72f, 0.985f), "PERSONAJE", 40f, creamColor,
                TextAlignmentOptions.Left, FontStyles.Bold);
            CreateText("Character Status Subtitle", content, new Vector2(0.04f, 0.865f),
                new Vector2(0.72f, 0.92f), GameLoadout.IsSpanish
                    ? "CONFIGURA tu build durante la partida"
                    : "CONFIGURE your build during the run", 16f, mutedColor,
                TextAlignmentOptions.Left);
            closeButton = CreateButton("Close Character Status", content, new Vector2(0.86f, 0.91f),
                new Vector2(0.96f, 0.97f), "X", Close, warmColor, 21f);

            RectTransform profile = CreatePanel("Character Profile", content,
                new Vector2(0.035f, 0.12f), new Vector2(0.31f, 0.84f), panelAltColor, false)
                .GetComponent<RectTransform>();
            portraitImage = CreateImage("Character Portrait", profile, new Vector2(0.18f, 0.49f),
                new Vector2(0.82f, 0.84f), new Color(0.025f, 0.045f, 0.06f, 1f));
            portraitImage.preserveAspect = true;
            characterText = CreateText("Character Identity", profile, new Vector2(0.08f, 0.37f),
                new Vector2(0.92f, 0.48f), string.Empty, 24f, creamColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            loadoutText = CreateText("Character Loadout", profile, new Vector2(0.08f, 0.05f),
                new Vector2(0.92f, 0.34f), string.Empty, 15f, mutedColor,
                TextAlignmentOptions.Center);
            loadoutText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform statsPanel = CreatePanel("Attribute Allocation", content,
                new Vector2(0.33f, 0.47f), new Vector2(0.965f, 0.84f), panelAltColor, false)
                .GetComponent<RectTransform>();
            CreateText("Attribute Title", statsPanel, new Vector2(0.035f, 0.83f), new Vector2(0.7f, 0.98f),
                GameLoadout.IsSpanish ? "ATRIBUTOS" : "ATTRIBUTES", 22f, creamColor,
                TextAlignmentOptions.Left, FontStyles.Bold);
            CreateText("Attribute Points", statsPanel, new Vector2(0.7f, 0.83f), new Vector2(0.965f, 0.98f),
                GameLoadout.IsSpanish ? "PUNTOS LIBRES" : "FREE POINTS", 14f, accentColor,
                TextAlignmentOptions.Right, FontStyles.Bold);

            for (int index = 0; index < Stats.Length; index++)
            {
                int capturedIndex = index;
                float bottom = 0.035f + index * 0.15f;
                RectTransform row = CreatePanel($"Attribute Row {index + 1}", statsPanel,
                    new Vector2(0.025f, bottom), new Vector2(0.975f, bottom + 0.125f),
                    new Color(0.17f, 0.055f, 0.035f, 0.96f), false).GetComponent<RectTransform>();
                minusButtons[index] = CreateButton($"Refund {Stats[index]}", row,
                    new Vector2(0.01f, 0.12f), new Vector2(0.095f, 0.88f), "-",
                    () => RefundStat(capturedIndex), warmColor, 22f);
                statTexts[index] = CreateText($"Attribute Text {index + 1}", row,
                    new Vector2(0.11f, 0.08f), new Vector2(0.89f, 0.92f), string.Empty,
                    16f, creamColor, TextAlignmentOptions.Center, FontStyles.Bold);
                plusButtons[index] = CreateButton($"Spend {Stats[index]}", row,
                    new Vector2(0.905f, 0.12f), new Vector2(0.99f, 0.88f), "+",
                    () => SpendStat(capturedIndex), accentColor, 22f);
            }

            RectTransform summaryPanel = CreatePanel("Run Summary", content,
                new Vector2(0.035f, 0.12f), new Vector2(0.31f, 0.44f), panelAltColor, false)
                .GetComponent<RectTransform>();
            CreateText("Run Summary Title", summaryPanel, new Vector2(0.07f, 0.82f), new Vector2(0.93f, 0.98f),
                GameLoadout.IsSpanish ? "ESTADO DE LA RUN" : "RUN STATUS", 18f, creamColor,
                TextAlignmentOptions.Left, FontStyles.Bold);
            summaryText = CreateText("Run Summary Text", summaryPanel, new Vector2(0.07f, 0.06f),
                new Vector2(0.93f, 0.81f), string.Empty, 14f, mutedColor,
                TextAlignmentOptions.Left);
            summaryText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform abilitiesPanel = CreatePanel("Power Ups", content,
                new Vector2(0.33f, 0.12f), new Vector2(0.965f, 0.44f), panelAltColor, false)
                .GetComponent<RectTransform>();
            CreateText("Power Ups Title", abilitiesPanel, new Vector2(0.035f, 0.82f), new Vector2(0.96f, 0.98f),
                GameLoadout.IsSpanish ? "POWER-UPS Y HABILIDADES" : "POWER-UPS AND ABILITIES", 18f,
                creamColor, TextAlignmentOptions.Left, FontStyles.Bold);
            abilitiesText = CreateText("Power Ups Text", abilitiesPanel, new Vector2(0.035f, 0.045f),
                new Vector2(0.965f, 0.8f), string.Empty, 13f, mutedColor,
                TextAlignmentOptions.Left);
            abilitiesText.textWrappingMode = TextWrappingModes.Normal;

            CreateText("Character Status Hint", content, new Vector2(0.035f, 0.035f),
                new Vector2(0.965f, 0.09f), GameLoadout.IsSpanish
                    ? "P  PERSONAJE   |   ESC  CERRAR   |   Los puntos se pueden reasignar"
                    : "P  CHARACTER   |   ESC  CLOSE   |   Points can be reassigned",
                13f, mutedColor, TextAlignmentOptions.Center);

            menuRoot.SetActive(false);
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

        private void SpendStat(int index)
        {
            if (index < 0 || index >= Stats.Length) return;
            RunSession.SpendStatPoint(Stats[index]);
        }

        private void RefundStat(int index)
        {
            if (index < 0 || index >= Stats.Length) return;
            RunSession.RefundStatPoint(Stats[index]);
        }

        private void Refresh()
        {
            if (summaryText == null) return;
            bool spanish = GameLoadout.IsSpanish;
            Health health = FindPlayerComponent<Health>();
            PowerUp powerUp = FindPlayerComponent<PowerUp>();
            PlayerDodge dodge = FindPlayerComponent<PlayerDodge>();

            string healthValue = health != null
                ? $"{health.CurrentHealth:0} / {health.MaxHealth:0}"
                : "-";
            string manaValue = powerUp != null
                ? $"{powerUp.CurrentMana:0} / {powerUp.MaxMana:0}"
                : "-";
            string dashValue = dodge != null
                ? $"{dodge.DashCharges} / {dodge.MaxDashCharges}"
                : "-";

            characterText.text = $"{GameLoadout.CharacterName(spanish)}\n<size=58%>{GameLoadout.CharacterRole(spanish)}</size>";
            loadoutText.text = spanish
                ? $"ARMA\n{GameLoadout.WeaponName(true)}\n\nHABILIDAD\n{GameLoadout.AbilityName(true)}\n{GameLoadout.AbilityDescription(true)}"
                : $"WEAPON\n{GameLoadout.WeaponName(false)}\n\nABILITY\n{GameLoadout.AbilityName(false)}\n{GameLoadout.AbilityDescription(false)}";

            summaryText.text = spanish
                ? $"NIVEL  {RunSession.Level}\nEXP  {RunSession.Experience} / {RunSession.ExperienceToNextLevel}\nPUNTOS LIBRES  {RunSession.AvailableStatPoints}\nASIGNADOS  {RunSession.AllocatedStatPoints}\n\nVIDA  {healthValue}\nMANA  {manaValue}\nDASH  {dashValue}\nORO  {PlayerEconomy.Gold}\nMUERTES  {RunSession.PlayerDeaths}\n\nMOVIMIENTO  x{RunSession.MoveSpeedMultiplier:0.00}\nDANO  x{RunSession.DamageMultiplier:0.00}\nCADENCIA  x{1f / Mathf.Max(0.01f, RunSession.AttackCooldownMultiplier):0.00}\nPROYECTIL  x{RunSession.ProjectileSpeedMultiplier:0.00}"
                : $"LEVEL  {RunSession.Level}\nXP  {RunSession.Experience} / {RunSession.ExperienceToNextLevel}\nFREE POINTS  {RunSession.AvailableStatPoints}\nALLOCATED  {RunSession.AllocatedStatPoints}\n\nHEALTH  {healthValue}\nMANA  {manaValue}\nDASH  {dashValue}\nGOLD  {PlayerEconomy.Gold}\nDEATHS  {RunSession.PlayerDeaths}\n\nMOVE  x{RunSession.MoveSpeedMultiplier:0.00}\nDAMAGE  x{RunSession.DamageMultiplier:0.00}\nFIRE RATE  x{1f / Mathf.Max(0.01f, RunSession.AttackCooldownMultiplier):0.00}\nPROJECTILE  x{RunSession.ProjectileSpeedMultiplier:0.00}";

            for (int index = 0; index < Stats.Length; index++)
            {
                PlayerStatType stat = Stats[index];
                statTexts[index].text = $"{RunSession.GetStatName(stat, spanish)}  {RunSession.GetStatValue(stat)}\n<size=70%>{RunSession.GetStatDescription(stat, spanish)}</size>";
                plusButtons[index].interactable = RunSession.AvailableStatPoints > 0;
                minusButtons[index].interactable = RunSession.GetStatValue(stat) > 0;
            }

            abilitiesText.text = string.Empty;
            foreach (RunAbilityType ability in Abilities)
            {
                int rank = RunSession.GetAbilityRank(ability);
                string rankText = spanish
                    ? $"RANGO {rank}/{RunSession.MaximumAbilityRank}"
                    : $"RANK {rank}/{RunSession.MaximumAbilityRank}";
                abilitiesText.text += $"{RunSession.GetAbilityName(ability, spanish)}  {rankText}\n<size=78%>{RunSession.GetAbilityDescription(ability, spanish)}</size>\n\n";
            }

            RefreshPortrait();
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

        private static T FindPlayerComponent<T>() where T : Component
        {
            Transform player = FindPlayerTransform();
            if (player == null) return null;
            T component = player.GetComponent<T>();
            if (component == null) component = player.GetComponentInChildren<T>(true);
            if (component == null) component = player.GetComponentInParent<T>();
            return component;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventSystemObject = new("Character Menu EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private Button CreateButton(string objectName, RectTransform parent, Vector2 anchorMinimum,
            Vector2 anchorMaximum, string label, UnityEngine.Events.UnityAction action, Color color,
            float fontSize)
        {
            GameObject buttonObject = CreatePanel(objectName, parent, anchorMinimum, anchorMaximum, color, true);
            Image image = buttonObject.GetComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.35f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(color, Color.black, 0.35f);
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.32f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);
            CreateText("Label", buttonObject.transform as RectTransform, new Vector2(0.03f, 0.02f),
                new Vector2(0.97f, 0.98f), label, fontSize, Color.white,
                TextAlignmentOptions.Center, FontStyles.Bold);
            return button;
        }

        private static Image CreateImage(string objectName, RectTransform parent, Vector2 anchorMinimum,
            Vector2 anchorMaximum, Color color)
        {
            GameObject imageObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMinimum;
            rect.anchorMax = anchorMaximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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
            text.fontSizeMin = Mathf.Max(8f, fontSize * 0.54f);
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
