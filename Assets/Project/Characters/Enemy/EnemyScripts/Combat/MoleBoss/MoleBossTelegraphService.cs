using System.Collections.Generic;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public sealed class MoleBossTelegraphService
    {
        private const int CircleSegments = 48;
        private readonly List<GameObject> visuals = new();
        private Material material;
        private Material pixelLaserMaterial;
        private Texture2D pixelLaserTexture;

        public GameObject CreateLine(string name, Color color, float width, params Vector2[] positions)
        {
            return CreateLineInternal(name, color, width, false, positions);
        }

        public GameObject CreatePixelLaser(string name, Color color, float width, params Vector2[] positions)
        {
            return CreateLineInternal(name, color, width, true, positions);
        }

        public GameObject CreateCircle(string name, Vector2 center, float radius, Color color)
        {
            GameObject visual = CreateVisual(name);
            LineRenderer line = visual.AddComponent<LineRenderer>();
            Configure(line, color, 0.09f, false);
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

        public GameObject CreatePrefab(string name, GameObject prefab, Vector2 position, float scale = 1f)
        {
            if (prefab == null) return null;
            GameObject visual = Object.Instantiate(prefab, position, Quaternion.identity);
            visual.name = name;
            visual.transform.localScale *= Mathf.Max(0.01f, scale);
            visuals.Add(visual);
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
            if (pixelLaserMaterial != null) Object.Destroy(pixelLaserMaterial);
            if (pixelLaserTexture != null) Object.Destroy(pixelLaserTexture);
            material = null;
            pixelLaserMaterial = null;
            pixelLaserTexture = null;
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

        private GameObject CreateLineInternal(string name, Color color, float width, bool pixelLaser,
            params Vector2[] positions)
        {
            if (positions == null || positions.Length < 2) return null;
            GameObject visual = CreateVisual(name);
            LineRenderer line = visual.AddComponent<LineRenderer>();
            Configure(line, color, width, pixelLaser);
            line.positionCount = positions.Length;
            line.numCapVertices = pixelLaser ? 0 : 4;
            for (int i = 0; i < positions.Length; i++) line.SetPosition(i, positions[i]);
            if (pixelLaser)
            {
                float distance = Vector2.Distance(positions[0], positions[positions.Length - 1]);
                line.textureScale = new Vector2(Mathf.Max(1f, distance / 0.55f), 1f);
            }
            return visual;
        }

        private GameObject CreateVisual(string name)
        {
            GameObject visual = new(name);
            visuals.Add(visual);
            return visual;
        }

        private void Configure(LineRenderer line, Color color, float width, bool pixelLaser)
        {
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 25;
            line.textureMode = pixelLaser ? LineTextureMode.Tile : LineTextureMode.Stretch;
            line.alignment = LineAlignment.TransformZ;
            line.material = pixelLaser ? GetPixelLaserMaterial() : GetMaterial();
        }

        private Material GetMaterial()
        {
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = shader != null ? new Material(shader) : null;
            return material;
        }

        private Material GetPixelLaserMaterial()
        {
            if (pixelLaserMaterial != null) return pixelLaserMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            pixelLaserTexture = BuildPixelLaserTexture();
            pixelLaserMaterial = new Material(shader)
            {
                name = "Pixel Energy Laser Material",
                mainTexture = pixelLaserTexture
            };
            return pixelLaserMaterial;
        }

        private static Texture2D BuildPixelLaserTexture()
        {
            const int width = 24;
            const int height = 8;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                name = "Pixel Energy Laser Strip",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int x = 0; x < width; x++)
            {
                bool notch = x % 6 == 0 || x % 6 == 1;
                for (int y = 0; y < height; y++)
                {
                    int centerDistance = Mathf.Abs(y - (height - 1) / 2);
                    Color pixel = Color.clear;
                    if (centerDistance <= 1) pixel = Color.white;
                    else if (centerDistance == 2) pixel = new Color(0.72f, 0.95f, 1f, 0.95f);
                    else if (centerDistance == 3 && !notch) pixel = new Color(0.25f, 0.58f, 1f, 0.72f);
                    texture.SetPixel(x, y, pixel);
                }
            }
            texture.Apply(false, false);
            return texture;
        }
    }
}
