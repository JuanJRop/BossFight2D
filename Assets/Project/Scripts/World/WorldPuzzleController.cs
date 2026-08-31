using System;
using System.Collections.Generic;
using Project.Scripts.Controller;
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
            UpdateVisuals();
        }

        private void Update()
        {
            if (completed || player == null ||
                (UIManager.instance != null && UIManager.instance.IsPaused)) return;

            if (wrongPulseUntil > 0f && Time.time >= wrongPulseUntil)
            {
                wrongPulseUntil = 0f;
                UpdateVisuals();
            }

            if (!Input.GetKeyDown(KeyCode.E)) return;
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
                    new Vector2((index - 1) * 1.6f, 8.3f), new Vector2(0.9f, 0.28f),
                    WithAlpha(colors[nodeIndex], 0.78f), 7);
            }
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

            return new PuzzleNode
            {
                Transform = nodeObject.transform,
                Outer = outer,
                Core = core,
                BaseColor = color
            };
        }

        private void HandleSequenceInput(int nodeIndex)
        {
            if (nodeIndex != sequenceOrder[sequenceIndex])
            {
                sequenceIndex = 0;
                wrongPulseUntil = Time.time + 0.38f;
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
                float pulse = 0.42f + Mathf.Abs(Mathf.Sin(Time.time * 3f + index)) * 0.1f;
                node.Core.transform.localScale = new Vector3(pulse, pulse, 1f);
            }
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

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
