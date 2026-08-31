using System;
using System.Collections.Generic;
using Project.Scripts.Controller;
using TMPro;
using UnityEngine;

namespace Project.Scripts.World
{
    public enum WorldPuzzleKind
    {
        Sequence,
        Circuit
    }

    public sealed class WorldPuzzleController : MonoBehaviour
    {
        private sealed class PuzzleNode
        {
            public Transform Transform;
            public SpriteRenderer Outer;
            public SpriteRenderer Core;
            public TextMeshProUGUI Label;
            public Color BaseColor;
            public bool Active;
        }

        private readonly List<PuzzleNode> nodes = new();
        private readonly bool[] circuitStates = new bool[3];
        private readonly int[] sequenceOrder = { 1, 2, 0 };

        private Transform player;
        private Action solved;
        private WorldPuzzleKind kind;
        private int sequenceIndex;
        private float wrongPulseUntil;
        private float feedbackUntil;
        private TextMeshProUGUI interactionLabel;
        private TextMeshProUGUI progressLabel;
        private GameObject keyboardPrompt;
        private static Texture2D promptTexture;
        private static Sprite promptSprite;
        private bool completed;

        public static WorldPuzzleController CreateRuntime(WorldPuzzleKind puzzleKind, Transform playerTarget,
            Transform parent, Action solvedCallback)
        {
            if (parent == null || playerTarget == null) return null;

            GameObject puzzleObject = new(puzzleKind == WorldPuzzleKind.Sequence
                ? "Sequence Puzzle"
                : "Circuit Puzzle");
            puzzleObject.transform.SetParent(parent, false);
            WorldPuzzleController controller = puzzleObject.AddComponent<WorldPuzzleController>();
            controller.Configure(puzzleKind, playerTarget, solvedCallback);
            return controller;
        }

        private void Configure(WorldPuzzleKind puzzleKind, Transform playerTarget, Action solvedCallback)
        {
            kind = puzzleKind;
            player = playerTarget;
            solved = solvedCallback;
            if (kind == WorldPuzzleKind.Sequence) BuildSequencePuzzle();
            else BuildCircuitPuzzle();
            BuildKeyboardPrompt();
            UpdateVisuals();
        }

        private void Update()
        {
            if (player == null || (UIManager.instance != null && UIManager.instance.IsPaused)) return;

            if (wrongPulseUntil > 0f && Time.time >= wrongPulseUntil)
            {
                wrongPulseUntil = 0f;
                UpdateVisuals();
            }

            if (completed)
            {
                UpdatePrompt();
                return;
            }

            if (!Input.GetKeyDown(KeyCode.E))
            {
                UpdatePrompt();
                return;
            }
            int nodeIndex = FindClosestNode();
            if (nodeIndex < 0) return;

            if (kind == WorldPuzzleKind.Sequence) HandleSequenceInput(nodeIndex);
            else HandleCircuitInput(nodeIndex);
            UpdateVisuals();
        }

        private void BuildSequencePuzzle()
        {
            Vector2[] positions =
            {
                new(-7f, 0f), new(0f, 5.2f), new(7f, 0f)
            };
            Color[] colors =
            {
                new Color(0.16f, 0.7f, 1f, 1f),
                new Color(1f, 0.58f, 0.12f, 1f),
                new Color(0.26f, 0.92f, 0.56f, 1f)
            };
            for (int index = 0; index < positions.Length; index++)
                nodes.Add(CreateNode(index, positions[index], colors[index]));

            for (int index = 0; index < sequenceOrder.Length; index++)
            {
                int nodeIndex = sequenceOrder[index];
                CreateVisual($"Sequence Hint {index + 1}",
                    new Vector2((index - 1) * 1.6f, 8.3f), new Vector2(1.05f, 0.32f),
                    WithAlpha(colors[nodeIndex], 0.78f), 7);
                CreateWorldLabel($"Sequence Hint Label {index + 1}",
                    new Vector2((index - 1) * 1.6f, 8.3f), new Vector2(1.05f, 0.7f),
                    (nodeIndex + 1).ToString(), 48f, new Color(0.12f, 0.04f, 0.02f, 1f), 12,
                    FontStyles.Bold);
            }

            bool spanish = GameLoadout.IsSpanish;
            CreateWorldLabel("Sequence Title", new Vector2(0f, 10.25f), new Vector2(25f, 0.7f),
                spanish ? "PUZZLE DE SECUENCIA" : "SEQUENCE PUZZLE", 48f,
                new Color(0.98f, 0.92f, 0.82f, 1f), 12, FontStyles.Bold);
            CreateWorldLabel("Sequence Order", new Vector2(0f, 9.38f), new Vector2(31f, 0.72f),
                spanish ? "ORDEN: 2 NARANJA  >  3 VERDE  >  1 AZUL" :
                    "ORDER: 2 ORANGE  >  3 GREEN  >  1 BLUE", 36f,
                new Color(0.86f, 0.78f, 0.66f, 1f), 12);

            CreateVisual("Puzzle Instruction Backing", new Vector2(0f, -8.65f),
                new Vector2(24f, 1.15f), new Color(0.04f, 0.015f, 0.012f, 0.92f), 8);
            interactionLabel = CreateWorldLabel("Puzzle Interaction", new Vector2(0f, -8.62f),
                new Vector2(23f, 0.72f), spanish ? "ACERCATE A UN TERMINAL" : "MOVE NEAR A TERMINAL",
                38f, new Color(0.98f, 0.92f, 0.82f, 1f), 13, FontStyles.Bold);
            progressLabel = CreateWorldLabel("Puzzle Progress", new Vector2(0f, -7.72f),
                new Vector2(16f, 0.55f), string.Empty, 30f,
                new Color(0.72f, 0.58f, 0.48f, 1f), 13);
        }

        private void BuildCircuitPuzzle()
        {
            Vector2[] positions =
            {
                new(-6f, 0f), new(0f, 0f), new(6f, 0f)
            };
            Color color = new(0.18f, 0.72f, 1f, 1f);
            for (int index = 0; index < positions.Length; index++)
                nodes.Add(CreateNode(index, positions[index], color));

            circuitStates[1] = true;
            CreateVisual("Circuit Left Link", new Vector2(-3f, 0f), new Vector2(4.4f, 0.14f),
                new Color(0.12f, 0.32f, 0.42f, 0.9f), 5);
            CreateVisual("Circuit Right Link", new Vector2(3f, 0f), new Vector2(4.4f, 0.14f),
                new Color(0.12f, 0.32f, 0.42f, 0.9f), 5);
            CreateVisual("Circuit Goal", new Vector2(0f, 8.3f), new Vector2(3.3f, 0.24f),
                new Color(0.22f, 0.86f, 0.55f, 0.8f), 7);

            bool spanish = GameLoadout.IsSpanish;
            CreateWorldLabel("Circuit Title", new Vector2(0f, 10.25f), new Vector2(25f, 0.7f),
                spanish ? "CIRCUITO: ACTIVA LOS 3" : "CIRCUIT: ACTIVATE ALL 3", 48f,
                new Color(0.98f, 0.92f, 0.82f, 1f), 12, FontStyles.Bold);
            CreateWorldLabel("Circuit Instructions", new Vector2(0f, 9.38f), new Vector2(31f, 0.72f),
                spanish ? "E  CAMBIA ESTE TERMINAL Y SUS VECINOS" :
                    "E  TOGGLE THIS TERMINAL AND ITS NEIGHBORS", 34f,
                new Color(0.86f, 0.78f, 0.66f, 1f), 12);

            CreateVisual("Puzzle Instruction Backing", new Vector2(0f, -8.65f),
                new Vector2(24f, 1.15f), new Color(0.04f, 0.015f, 0.012f, 0.92f), 8);
            interactionLabel = CreateWorldLabel("Puzzle Interaction", new Vector2(0f, -8.62f),
                new Vector2(23f, 0.72f), spanish ? "ACERCATE A UN TERMINAL" : "MOVE NEAR A TERMINAL",
                38f, new Color(0.98f, 0.92f, 0.82f, 1f), 13, FontStyles.Bold);
            progressLabel = CreateWorldLabel("Puzzle Progress", new Vector2(0f, -7.72f),
                new Vector2(16f, 0.55f), string.Empty, 30f,
                new Color(0.72f, 0.58f, 0.48f, 1f), 13);
        }

        private PuzzleNode CreateNode(int index, Vector2 position, Color color)
        {
            GameObject nodeObject = new($"Puzzle Node {index + 1}");
            nodeObject.transform.SetParent(transform, false);
            nodeObject.transform.localPosition = new Vector3(position.x, position.y, -0.2f);
            nodeObject.transform.localScale = Vector3.one * 1.25f;

            SpriteRenderer outer = nodeObject.AddComponent<SpriteRenderer>();
            outer.sprite = RuntimeWhiteSprite.Instance;
            outer.sortingOrder = 6;

            GameObject coreObject = new("Puzzle Core");
            coreObject.transform.SetParent(nodeObject.transform, false);
            coreObject.transform.localScale = new Vector3(0.48f, 0.48f, 1f);
            SpriteRenderer core = coreObject.AddComponent<SpriteRenderer>();
            core.sprite = RuntimeWhiteSprite.Instance;
            core.sortingOrder = 7;

            TextMeshProUGUI label = CreateWorldLabel($"Puzzle Node Label {index + 1}", position,
                new Vector2(1.45f, 0.86f), (index + 1).ToString(), 52f,
                new Color(0.12f, 0.04f, 0.02f, 1f), 12, FontStyles.Bold);
            if (kind == WorldPuzzleKind.Sequence)
            {
                string colorName = GetColorName(index, GameLoadout.IsSpanish);
                CreateWorldLabel($"Puzzle Node Name {index + 1}", position + Vector2.down * 1.25f,
                    new Vector2(4.2f, 0.58f), colorName, 30f,
                    new Color(0.84f, 0.76f, 0.65f, 1f), 12, FontStyles.Bold);
            }

            return new PuzzleNode
            {
                Transform = nodeObject.transform,
                Outer = outer,
                Core = core,
                Label = label,
                BaseColor = color
            };
        }

        private void HandleSequenceInput(int nodeIndex)
        {
            if (nodeIndex != sequenceOrder[sequenceIndex])
            {
                sequenceIndex = 0;
                wrongPulseUntil = Time.time + 0.38f;
                feedbackUntil = Time.time + 0.72f;
                foreach (PuzzleNode node in nodes) node.Active = false;
                return;
            }

            nodes[nodeIndex].Active = true;
            sequenceIndex++;
            if (sequenceIndex >= sequenceOrder.Length) CompletePuzzle();
        }

        private void HandleCircuitInput(int nodeIndex)
        {
            for (int index = Mathf.Max(0, nodeIndex - 1); index <= Mathf.Min(2, nodeIndex + 1); index++)
                circuitStates[index] = !circuitStates[index];

            if (circuitStates[0] && circuitStates[1] && circuitStates[2]) CompletePuzzle();
        }

        private int FindClosestNode()
        {
            int closestIndex = -1;
            float closestDistance = 1.85f;
            Vector2 playerPosition = player.position;
            for (int index = 0; index < nodes.Count; index++)
            {
                float distance = Vector2.Distance(playerPosition, nodes[index].Transform.position);
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closestIndex = index;
            }

            return closestIndex;
        }

        private void CompletePuzzle()
        {
            if (completed) return;
            completed = true;
            foreach (PuzzleNode node in nodes) node.Active = true;
            solved?.Invoke();
        }

        private void UpdateVisuals()
        {
            bool wrongPulse = wrongPulseUntil > Time.time;
            for (int index = 0; index < nodes.Count; index++)
            {
                PuzzleNode node = nodes[index];
                bool active = kind == WorldPuzzleKind.Sequence ? node.Active : circuitStates[index];
                Color color = wrongPulse
                    ? new Color(1f, 0.14f, 0.08f, 0.95f)
                    : completed
                        ? new Color(0.35f, 1f, 0.62f, 1f)
                        : active ? node.BaseColor : WithAlpha(node.BaseColor, 0.22f);
                node.Outer.color = color;
                node.Core.color = active || completed
                    ? new Color(1f, 0.92f, 0.68f, 0.95f)
                    : WithAlpha(node.BaseColor, 0.32f);
                if (node.Label != null)
                    node.Label.color = active || completed
                        ? new Color(0.12f, 0.04f, 0.02f, 1f)
                        : new Color(0.98f, 0.92f, 0.82f, 1f);
                float pulse = 0.42f + Mathf.Abs(Mathf.Sin(Time.time * 3f + index)) * 0.1f;
                node.Core.transform.localScale = new Vector3(pulse, pulse, 1f);
            }

            UpdatePrompt();
            UpdateProgress();
        }

        private void UpdatePrompt()
        {
            if (interactionLabel == null) return;
            bool spanish = GameLoadout.IsSpanish;
            if (completed)
            {
                SetKeyboardPromptVisible(false);
                interactionLabel.text = spanish ? "RESUELTO  |  PUERTA ABIERTA" : "SOLVED  |  DOOR OPEN";
                interactionLabel.color = new Color(0.35f, 1f, 0.62f, 1f);
                return;
            }

            int nodeIndex = FindClosestNode();
            UpdateKeyboardPrompt(nodeIndex);

            if (feedbackUntil > Time.time)
            {
                interactionLabel.text = spanish
                    ? "ORDEN INCORRECTO  |  VUELVE A INTENTAR"
                    : "WRONG ORDER  |  TRY AGAIN";
                interactionLabel.color = new Color(1f, 0.28f, 0.18f, 1f);
                return;
            }

            if (nodeIndex < 0)
            {
                interactionLabel.text = spanish ? "ACERCATE A UN TERMINAL" : "MOVE NEAR A TERMINAL";
                interactionLabel.color = new Color(0.98f, 0.92f, 0.82f, 1f);
                return;
            }

            if (kind == WorldPuzzleKind.Sequence)
            {
                int expectedNode = sequenceOrder[sequenceIndex];
                interactionLabel.text = spanish
                    ? $"TERMINAL {nodeIndex + 1}  |  PULSA E  |  SIGUIENTE: {expectedNode + 1}"
                    : $"TERMINAL {nodeIndex + 1}  |  PRESS E  |  NEXT: {expectedNode + 1}";
            }
            else
            {
                interactionLabel.text = spanish
                    ? $"TERMINAL {nodeIndex + 1}  |  PULSA E  |  AFECTA A SUS VECINOS"
                    : $"TERMINAL {nodeIndex + 1}  |  PRESS E  |  AFFECTS NEIGHBORS";
            }
            interactionLabel.color = new Color(0.98f, 0.92f, 0.82f, 1f);
        }

        private void UpdateProgress()
        {
            if (progressLabel == null) return;
            bool spanish = GameLoadout.IsSpanish;
            if (completed)
            {
                progressLabel.text = spanish ? "RECOMPENSA DESBLOQUEADA" : "REWARD UNLOCKED";
                return;
            }

            if (kind == WorldPuzzleKind.Sequence)
            {
                progressLabel.text = spanish
                    ? $"PROGRESO: {sequenceIndex}/{sequenceOrder.Length}"
                    : $"PROGRESS: {sequenceIndex}/{sequenceOrder.Length}";
                return;
            }

            int activeCount = 0;
            foreach (bool active in circuitStates)
                if (active) activeCount++;
            progressLabel.text = spanish
                ? $"TERMINALES ACTIVOS: {activeCount}/3"
                : $"ACTIVE TERMINALS: {activeCount}/3";
        }

        private static string GetColorName(int index, bool spanish)
        {
            if (spanish)
                return index switch { 0 => "AZUL", 1 => "NARANJA", _ => "VERDE" };
            return index switch { 0 => "BLUE", 1 => "ORANGE", _ => "GREEN" };
        }

        private GameObject CreateVisual(string objectName, Vector2 position, Vector2 size, Color color,
            int sortingOrder)
        {
            GameObject visual = new(objectName);
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(position.x, position.y, -0.25f);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeWhiteSprite.Instance;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return visual;
        }

        private TextMeshProUGUI CreateWorldLabel(string objectName, Vector2 position, Vector2 worldSize,
            string value, float fontSize, Color color, int sortingOrder,
            FontStyles style = FontStyles.Normal)
        {
            GameObject canvasObject = new(objectName, typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = new Vector3(position.x, position.y, -0.55f);
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.01f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = worldSize * 100f;

            GameObject textObject = new("Text", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(canvasRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontSizeMin = fontSize;
            text.fontSizeMax = fontSize;
            text.enableAutoSizing = false;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.outlineColor = new Color(0f, 0f, 0f, 0.82f);
            text.outlineWidth = 0.18f;
            text.raycastTarget = false;
            return text;
        }

        private void BuildKeyboardPrompt()
        {
            keyboardPrompt = new GameObject("Puzzle Keyboard E Prompt");
            keyboardPrompt.transform.SetParent(transform, false);
            CounterParentScale(keyboardPrompt.transform);

            CreateKeyLayer("Key Shadow", new Vector3(0f, -0.045f, 0f),
                new Vector3(0.46f, 0.36f, 1f), new Color(0.025f, 0.03f, 0.045f, 0.95f), 40);
            CreateKeyLayer("Key Border", Vector3.zero, new Vector3(0.44f, 0.34f, 1f),
                new Color(0.86f, 0.9f, 0.96f, 1f), 41);
            CreateKeyLayer("Key Face", new Vector3(0f, 0.012f, 0f), new Vector3(0.36f, 0.26f, 1f),
                new Color(0.08f, 0.1f, 0.14f, 1f), 42);

            GameObject textObject = new("E");
            textObject.transform.SetParent(keyboardPrompt.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            TextMesh promptText = textObject.AddComponent<TextMesh>();
            promptText.text = "E";
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.fontSize = 48;
            promptText.characterSize = 0.05f;
            promptText.color = Color.white;
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            if (textRenderer != null) textRenderer.sortingOrder = 43;
            keyboardPrompt.SetActive(false);
        }

        private void UpdateKeyboardPrompt(int nodeIndex)
        {
            bool visible = nodeIndex >= 0 && !completed;
            SetKeyboardPromptVisible(visible);
            if (!visible || keyboardPrompt == null) return;

            keyboardPrompt.transform.position = nodes[nodeIndex].Transform.position + Vector3.up * 1.5f;
            CounterParentScale(keyboardPrompt.transform);
        }

        private void SetKeyboardPromptVisible(bool visible)
        {
            if (keyboardPrompt != null && keyboardPrompt.activeSelf != visible)
                keyboardPrompt.SetActive(visible);
        }

        private void CreateKeyLayer(string objectName, Vector3 localPosition, Vector3 localScale,
            Color color, int sortingOrder)
        {
            GameObject layer = new(objectName);
            layer.transform.SetParent(keyboardPrompt.transform, false);
            layer.transform.localPosition = localPosition;
            layer.transform.localScale = localScale;
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPromptSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static Sprite GetPromptSprite()
        {
            if (promptSprite != null) return promptSprite;

            promptTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Puzzle Key Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            promptTexture.SetPixel(0, 0, Color.white);
            promptTexture.Apply();
            promptSprite = Sprite.Create(promptTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            promptSprite.name = "Runtime Puzzle Key Sprite";
            promptSprite.hideFlags = HideFlags.HideAndDontSave;
            return promptSprite;
        }

        private void CounterParentScale(Transform target)
        {
            if (target == null) return;
            Vector3 parentScale = transform.lossyScale;
            target.localScale = new Vector3(
                1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
            target.rotation = Quaternion.identity;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
