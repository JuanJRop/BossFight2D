using System;
using System.Collections;
using System.Collections.Generic;
using Project.Scripts.Controller;
using Project.Scripts.Progression;
using UnityEngine;

namespace Project.Scripts.World
{
    public sealed class WorldPuzzleChest : MonoBehaviour
    {
        private Transform player;
        private Action opened;
        private SpriteRenderer chestBody;
        private SpriteRenderer chestLid;
        private SpriteRenderer glow;
        private SpriteRenderer lockRenderer;
        private TextMesh label;
        private GameObject keyboardPrompt;
        private readonly List<SpriteRenderer> celebrationRenderers = new();
        private readonly List<Vector2> celebrationDirections = new();
        private Color accentColor;
        private bool isOpen;
        private float pulseOffset;

        public static WorldPuzzleChest CreateRuntime(Vector2 position, Transform playerTarget,
            Transform parent, Action openedCallback, bool legendary = true)
        {
            if (playerTarget == null || parent == null) return null;

            GameObject chestObject = new("Legendary Puzzle Chest");
            chestObject.transform.SetParent(parent, false);
            chestObject.transform.localPosition = new Vector3(position.x, position.y, -0.35f);
            WorldPuzzleChest chest = chestObject.AddComponent<WorldPuzzleChest>();
            chest.Configure(playerTarget, openedCallback, legendary);

            BoxCollider2D collider = chestObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.8f, 1.1f);
            return chest;
        }

        private void Configure(Transform playerTarget, Action openedCallback, bool legendary)
        {
            player = playerTarget;
            opened = openedCallback;
            pulseOffset = Mathf.Abs(GetInstanceID() % 100) * 0.05f;
            accentColor = legendary
                ? new Color(1f, 0.7f, 0.16f, 1f)
                : new Color(0.22f, 0.82f, 1f, 1f);

            chestBody = CreateVisual("Chest Body", new Vector2(0f, -0.18f),
                new Vector2(1.9f, 0.82f), legendary
                    ? new Color(0.34f, 0.08f, 0.03f, 1f)
                    : new Color(0.035f, 0.14f, 0.23f, 1f), 44);
            chestLid = CreateVisual("Chest Lid", new Vector2(0f, 0.3f),
                new Vector2(2.05f, 0.46f), accentColor, 45);
            glow = CreateVisual("Chest Glow", new Vector2(0f, 0f),
                new Vector2(2.5f, 1.7f), WithAlpha(accentColor, 0.12f), 43);
            lockRenderer = CreateVisual("Chest Lock", new Vector2(0f, -0.08f),
                new Vector2(0.3f, 0.36f), legendary
                    ? new Color(1f, 0.86f, 0.28f, 1f)
                    : new Color(0.7f, 0.94f, 1f, 1f), 46);

            label = CreateLabel("Legendary Chest Label", new Vector3(0f, 1.05f, -0.05f),
                GameLoadout.IsSpanish
                    ? legendary ? "COFRE LEGENDARIO" : "COFRE DE RECOMPENSA"
                    : legendary ? "LEGENDARY CHEST" : "PUZZLE REWARD CHEST",
                legendary ? new Color(1f, 0.86f, 0.48f, 1f) : new Color(0.58f, 0.92f, 1f, 1f),
                47);
            BuildKeyboardPrompt();
            BuildVictoryBurst();
        }

        private void Update()
        {
            if (player == null || isOpen) return;
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;

            bool nearby = Vector2.Distance(player.position, transform.position) <= 2.3f;
            if (keyboardPrompt != null) keyboardPrompt.SetActive(nearby);

            if (glow != null)
            {
                float alpha = 0.1f + Mathf.Abs(Mathf.Sin((Time.time + pulseOffset) * 3.2f)) * 0.12f;
                glow.color = WithAlpha(accentColor, alpha);
            }

            if (nearby && Input.GetKeyDown(KeyCode.E)) Open();
        }

        private void Open()
        {
            if (isOpen) return;
            isOpen = true;
            if (keyboardPrompt != null) keyboardPrompt.SetActive(false);
            opened?.Invoke();
            StartCoroutine(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            const float duration = 0.68f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                if (chestLid != null)
                {
                    chestLid.transform.localRotation = Quaternion.Euler(0f, 0f, -24f * progress);
                    chestLid.transform.localPosition = new Vector3(0f, 0.3f + 0.2f * progress, 0f);
                }
                if (lockRenderer != null)
                    lockRenderer.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, progress);
                if (glow != null)
                    glow.color = WithAlpha(Color.Lerp(accentColor, Color.white, progress * 0.4f),
                        0.24f + progress * 0.45f);

                float burstProgress = Mathf.Clamp01((progress - 0.24f) / 0.76f);
                for (int index = 0; index < celebrationRenderers.Count; index++)
                {
                    SpriteRenderer burst = celebrationRenderers[index];
                    if (burst == null) continue;
                    Transform burstTransform = burst.transform;
                    float distance = Mathf.Lerp(0f, 1.45f, Mathf.SmoothStep(0f, 1f,
                        burstProgress));
                    burstTransform.localPosition = celebrationDirections[index] * distance;
                    float size = Mathf.Lerp(0.18f, 0.03f, burstProgress);
                    burstTransform.localScale = Vector3.one * size;
                    burst.color = WithAlpha(Color.Lerp(accentColor, Color.white, burstProgress),
                        Mathf.Clamp01(burstProgress * 2.2f) * (1f - burstProgress * 0.65f));
                }
                yield return null;
            }

            if (label != null)
            {
                bool spanish = GameLoadout.IsSpanish;
                label.text = spanish ? "RECOMPENSA OBTENIDA" : "REWARD CLAIMED";
                label.color = new Color(0.38f, 1f, 0.62f, 1f);
            }
        }

        private void BuildVictoryBurst()
        {
            for (int index = 0; index < 8; index++)
            {
                float angle = (Mathf.PI * 2f * index) / 8f + Mathf.PI * 0.125f;
                GameObject burstObject = new($"Victory Spark {index + 1}");
                burstObject.transform.SetParent(transform, false);
                burstObject.transform.localPosition = Vector3.zero;
                burstObject.transform.localScale = Vector3.one * 0.03f;

                SpriteRenderer burst = burstObject.AddComponent<SpriteRenderer>();
                burst.sprite = RuntimeWhiteSprite.Instance;
                burst.color = Color.clear;
                burst.sortingOrder = 48;
                celebrationRenderers.Add(burst);
                celebrationDirections.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }
        }

        private SpriteRenderer CreateVisual(string objectName, Vector2 localPosition,
            Vector2 localScale, Color color, int sortingOrder)
        {
            GameObject visual = new(objectName);
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            visual.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeWhiteSprite.Instance;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private TextMesh CreateLabel(string objectName, Vector3 localPosition, string value,
            Color color, int sortingOrder)
        {
            GameObject textObject = new(objectName);
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = localPosition;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 32;
            text.characterSize = 0.045f;
            text.color = color;
            MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = sortingOrder;
            return text;
        }

        private void BuildKeyboardPrompt()
        {
            keyboardPrompt = new GameObject("Chest Keyboard E Prompt");
            keyboardPrompt.transform.SetParent(transform, false);
            keyboardPrompt.transform.localPosition = new Vector3(0f, 1.65f, -0.05f);
            keyboardPrompt.transform.localScale = Vector3.one * 0.82f;

            CreatePromptLayer("Key Shadow", new Vector3(0f, -0.05f, 0f),
                new Vector3(0.52f, 0.4f, 1f), new Color(0.02f, 0.02f, 0.03f, 0.96f), 50);
            CreatePromptLayer("Key Border", Vector3.zero,
                new Vector3(0.5f, 0.38f, 1f), new Color(1f, 0.86f, 0.48f, 1f), 51);
            CreatePromptLayer("Key Face", new Vector3(0f, 0.014f, 0f),
                new Vector3(0.4f, 0.29f, 1f), new Color(0.12f, 0.04f, 0.02f, 1f), 52);

            GameObject keyTextObject = new("E");
            keyTextObject.transform.SetParent(keyboardPrompt.transform, false);
            keyTextObject.transform.localPosition = new Vector3(0f, 0.02f, -0.02f);
            TextMesh keyText = keyTextObject.AddComponent<TextMesh>();
            keyText.text = "E";
            keyText.anchor = TextAnchor.MiddleCenter;
            keyText.alignment = TextAlignment.Center;
            keyText.fontSize = 48;
            keyText.characterSize = 0.05f;
            keyText.color = Color.white;
            MeshRenderer keyRenderer = keyTextObject.GetComponent<MeshRenderer>();
            if (keyRenderer != null) keyRenderer.sortingOrder = 53;
            keyboardPrompt.SetActive(false);
        }

        private void CreatePromptLayer(string objectName, Vector3 localPosition,
            Vector3 localScale, Color color, int sortingOrder)
        {
            GameObject layer = new(objectName);
            layer.transform.SetParent(keyboardPrompt.transform, false);
            layer.transform.localPosition = localPosition;
            layer.transform.localScale = localScale;
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeWhiteSprite.Instance;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
