using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public sealed class MoleBossTelegraphService
    {
        private const int CircleSegments = 48;
        private readonly List<GameObject> visuals = new();
        private Material material;

        public GameObject CreateLine(string name, Color color, float width, params Vector2[] positions)
        {
            if (positions == null || positions.Length < 2) return null;
            GameObject visual = CreateVisual(name);
            LineRenderer line = visual.AddComponent<LineRenderer>();
            Configure(line, color, width);
            line.positionCount = positions.Length;
            line.numCapVertices = 4;
            for (int i = 0; i < positions.Length; i++) line.SetPosition(i, positions[i]);
            return visual;
        }

        public GameObject CreateCircle(string name, Vector2 center, float radius, Color color)
        {
            GameObject visual = CreateVisual(name);
            LineRenderer line = visual.AddComponent<LineRenderer>();
            Configure(line, color, 0.09f);
            line.loop = true;
            line.positionCount = CircleSegments;
            line.numCornerVertices = 3;
            UpdateCircle(line, center, radius);
            return visual;
        }

        public GameObject CreateSprite(string name, Vector2 position, Sprite sprite, Color color, int sortingOrder)
        {
            GameObject visual = CreateVisual(name);
            visual.transform.position = position;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return visual;
        }

        public void Release(GameObject visual)
        {
            if (visual == null) return;
            visuals.Remove(visual);
            Object.Destroy(visual);
        }

        public void ReleaseAll()
        {
            foreach (GameObject visual in visuals)
            {
                if (visual != null) Object.Destroy(visual);
            }
            visuals.Clear();
        }

        public void Dispose()
        {
            ReleaseAll();
            if (material != null) Object.Destroy(material);
            material = null;
        }

        public static void UpdateCircle(LineRenderer line, Vector2 center, float radius)
        {
            if (line == null) return;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private GameObject CreateVisual(string name)
        {
            GameObject visual = new(name);
            visuals.Add(visual);
            return visual;
        }

        private void Configure(LineRenderer line, Color color, float width)
        {
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 25;
            line.material = GetMaterial();
        }

        private Material GetMaterial()
        {
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = shader != null ? new Material(shader) : null;
            return material;
        }
    }
}
