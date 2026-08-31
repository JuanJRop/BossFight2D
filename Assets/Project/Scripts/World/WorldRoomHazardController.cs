using System;
using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Movement;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Scripts.World
{
    public enum WorldRoomHazardTheme
    {
        MovingLasers,
        SawGrid,
        Hybrid
    }

    public sealed class WorldRoomHazardController : MonoBehaviour
    {
        private const float RoomMinX = -15.4f;
        private const float RoomMaxX = 15.4f;
        private const float RoomMinY = -9.4f;
        private const float RoomMaxY = 9.4f;

        private readonly List<MovingLaser> lasers = new();
        private readonly List<MovingSaw> saws = new();
        private Transform player;
        private Health playerHealth;
        private PlayerDodge playerDodge;
        private Material lineMaterial;
        private WorldRoomHazardTheme theme;
        private bool configured;

        public static WorldRoomHazardController CreateRuntime(WorldRoomHazardTheme hazardTheme,
            Transform playerTarget, Transform parent, int seed)
        {
            if (playerTarget == null || parent == null) return null;

            GameObject hazardObject = new("Room Hazards");
            hazardObject.transform.SetParent(parent, false);
            WorldRoomHazardController controller = hazardObject.AddComponent<WorldRoomHazardController>();
            controller.Configure(hazardTheme, playerTarget, seed);
            return controller;
        }

        private void Configure(WorldRoomHazardTheme hazardTheme, Transform playerTarget, int seed)
        {
            theme = hazardTheme;
            player = playerTarget;
            playerHealth = playerTarget.GetComponentInParent<Health>();
            if (playerHealth == null) playerHealth = playerTarget.GetComponentInChildren<Health>(true);
            playerDodge = playerTarget.GetComponentInParent<PlayerDodge>();
            if (playerDodge == null) playerDodge = playerTarget.GetComponentInChildren<PlayerDodge>(true);

            int safeSeed = Mathf.Abs(seed == int.MinValue ? 1 : seed);
            switch (theme)
            {
                case WorldRoomHazardTheme.MovingLasers:
                    AddLaser(true, -3.8f, 3.9f, 0.2f, 0.4f);
                    AddLaser(false, 3.4f, 3.2f, 1.9f, 0.55f);
                    AddLaser(true, 1.8f, 3.1f, 3.4f, 0.35f);
                    break;
                case WorldRoomHazardTheme.SawGrid:
                    AddSaw(new Vector2(-9f, -5.2f), new Vector2(9f, -5.2f), 1.25f, 0.4f, safeSeed);
                    AddSaw(new Vector2(9.2f, 5.1f), new Vector2(-9.2f, 5.1f), 1.05f, 1.6f, safeSeed + 7);
                    AddSaw(new Vector2(-5.2f, -7.5f), new Vector2(-5.2f, 7.5f), 0.9f, 2.4f, safeSeed + 13);
                    break;
                case WorldRoomHazardTheme.Hybrid:
                    AddLaser(false, -1.8f, 3.4f, 0.9f, 0.45f);
                    AddLaser(true, 3.1f, 3.2f, 2.6f, 0.5f);
                    AddSaw(new Vector2(-9.5f, 6.3f), new Vector2(9.5f, 6.3f), 1.1f, 1.25f, safeSeed);
                    AddSaw(new Vector2(8f, -6.2f), new Vector2(8f, 6.2f), 1.2f, 2.9f, safeSeed + 11);
                    break;
            }

            configured = true;
        }

        private void Update()
        {
            if (!configured || player == null || playerHealth == null || !playerHealth.IsAlive) return;
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;

            foreach (MovingLaser laser in lasers) laser?.Tick(this);
            foreach (MovingSaw saw in saws) saw?.Tick(this);
        }

        private void AddLaser(bool horizontal, float lane, float movementRange, float phase,
            float speedScale)
        {
            GameObject laserObject = new(horizontal ? "Moving Laser Horizontal" : "Moving Laser Vertical");
            laserObject.transform.SetParent(transform, false);
            MovingLaser laser = new(horizontal, lane, movementRange, phase, speedScale);
            laser.Build(this, laserObject);
            lasers.Add(laser);
        }

        private void AddSaw(Vector2 start, Vector2 end, float speed, float phase, int seed)
        {
            GameObject sawObject = new($"Moving Saw {saws.Count + 1}");
            sawObject.transform.SetParent(transform, false);
            MovingSaw saw = new(start, end, speed, phase, seed);
            saw.Build(this, sawObject);
            saws.Add(saw);
        }

        private void TryDamagePlayer(Vector2 position, float radius, float damage,
            ref float nextDamageTime)
        {
            if (player == null || playerHealth == null || !playerHealth.IsAlive ||
                Time.time < nextDamageTime || playerDodge != null && playerDodge.IsInvulnerable)
                return;
            if (Vector2.Distance(player.position, position) > radius) return;

            playerHealth.TakeDamage(damage);
            nextDamageTime = Time.time + 0.55f;
        }

        private void TryDamagePlayerOnSegment(Vector2 start, Vector2 end, float radius, float damage,
            ref float nextDamageTime)
        {
            if (player == null || playerHealth == null || !playerHealth.IsAlive ||
                Time.time < nextDamageTime || playerDodge != null && playerDodge.IsInvulnerable)
                return;
            if (DistanceToSegment(player.position, start, end) > radius) return;

            playerHealth.TakeDamage(damage);
            nextDamageTime = Time.time + 0.55f;
        }

        private Material GetLineMaterial()
        {
            if (lineMaterial != null) return lineMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            lineMaterial = shader != null ? new Material(shader) : null;
            if (lineMaterial != null) lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            return lineMaterial;
        }

        private void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.0001f) return Vector2.Distance(point, start);
            float projection = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * projection);
        }

        private sealed class MovingLaser
        {
            private readonly bool horizontal;
            private readonly float lane;
            private readonly float movementRange;
            private readonly float phase;
            private readonly float speedScale;
            private LineRenderer warningLine;
            private LineRenderer glowLine;
            private LineRenderer coreLine;
            private float nextDamageTime;
            private WorldRoomHazardController owner;

            public MovingLaser(bool isHorizontal, float baseLane, float range, float startPhase,
                float movementSpeed)
            {
                horizontal = isHorizontal;
                lane = baseLane;
                movementRange = range;
                phase = startPhase;
                speedScale = movementSpeed;
            }

            public void Build(WorldRoomHazardController controller, GameObject root)
            {
                owner = controller;
                warningLine = CreateLine(root.transform, "Laser Warning", 0.07f, 30);
                glowLine = CreateLine(root.transform, "Laser Glow", 0.42f, 31);
                coreLine = CreateLine(root.transform, "Laser Core", 0.12f, 32);
                RefreshVisual(0f, false);
            }

            public void Tick(WorldRoomHazardController controller)
            {
                float cycleTime = 4.8f;
                float elapsed = Mathf.Repeat(Time.time * speedScale + phase, cycleTime);
                bool active = elapsed >= 0.92f && elapsed <= 2.12f;
                float travel = Mathf.Sin(Time.time * (0.62f + speedScale * 0.1f) + phase);
                float currentLane = lane + travel * movementRange;
                Vector2 start;
                Vector2 end;
                if (horizontal)
                {
                    start = new Vector2(RoomMinX, currentLane);
                    end = new Vector2(RoomMaxX, currentLane);
                }
                else
                {
                    start = new Vector2(currentLane, RoomMinY);
                    end = new Vector2(currentLane, RoomMaxY);
                }

                SetPositions(start, end);
                RefreshVisual(elapsed, active);
                if (active)
                    controller.TryDamagePlayerOnSegment(start, end, 0.42f, 18f, ref nextDamageTime);
            }

            private void SetPositions(Vector2 start, Vector2 end)
            {
                warningLine.SetPosition(0, new Vector3(start.x, start.y, -0.18f));
                warningLine.SetPosition(1, new Vector3(end.x, end.y, -0.18f));
                glowLine.SetPosition(0, new Vector3(start.x, start.y, -0.2f));
                glowLine.SetPosition(1, new Vector3(end.x, end.y, -0.2f));
                coreLine.SetPosition(0, new Vector3(start.x, start.y, -0.22f));
                coreLine.SetPosition(1, new Vector3(end.x, end.y, -0.22f));
            }

            private void RefreshVisual(float elapsed, bool active)
            {
                float warningPulse = 0.2f + Mathf.Abs(Mathf.Sin(Time.time * 8f + phase)) * 0.35f;
                bool warning = elapsed < 0.92f;
                Color warningColor = new(1f, 0.48f, 0.08f, warning ? warningPulse : 0.08f);
                Color glowColor = active
                    ? new Color(1f, 0.1f, 0.52f, 0.34f)
                    : new Color(1f, 0.32f, 0.08f, warningPulse * 0.48f);
                Color coreColor = active
                    ? new Color(1f, 0.78f, 0.9f, 0.98f)
                    : new Color(1f, 0.5f, 0.16f, warningPulse * 0.78f);
                warningLine.startColor = warningColor;
                warningLine.endColor = warningColor;
                glowLine.startColor = glowColor;
                glowLine.endColor = glowColor;
                coreLine.startColor = coreColor;
                coreLine.endColor = coreColor;
                warningLine.enabled = warning || active;
                glowLine.enabled = active || warning;
                coreLine.enabled = active;
            }

            private LineRenderer CreateLine(Transform parent, string objectName, float width,
                int sortingOrder)
            {
                GameObject lineObject = new(objectName);
                lineObject.transform.SetParent(parent, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.numCapVertices = 4;
                line.numCornerVertices = 4;
                line.startWidth = width;
                line.endWidth = width;
                line.sortingOrder = sortingOrder;
                line.material = owner.GetLineMaterial();
                line.textureMode = LineTextureMode.Stretch;
                return line;
            }
        }

        private sealed class MovingSaw
        {
            private static Sprite sawSprite;
            private static Sprite sawGlowSprite;
            private readonly Vector2 start;
            private readonly Vector2 end;
            private readonly float speed;
            private readonly float phase;
            private readonly int seed;
            private Transform root;
            private SpriteRenderer renderer;
            private SpriteRenderer glowRenderer;
            private float nextDamageTime;
            private WorldRoomHazardController owner;

            public MovingSaw(Vector2 pathStart, Vector2 pathEnd, float movementSpeed, float startPhase,
                int sawSeed)
            {
                start = pathStart;
                end = pathEnd;
                speed = movementSpeed;
                phase = startPhase;
                seed = sawSeed;
            }

            public void Build(WorldRoomHazardController controller, GameObject sawObject)
            {
                owner = controller;
                root = sawObject.transform;
                root.position = start;

                GameObject glowObject = new("Saw Glow");
                glowObject.transform.SetParent(root, false);
                glowRenderer = glowObject.AddComponent<SpriteRenderer>();
                glowRenderer.sprite = GetSawGlowSprite();
                glowRenderer.color = new Color(1f, 0.18f, 0.04f, 0.32f);
                glowRenderer.sortingOrder = 20;

                renderer = sawObject.AddComponent<SpriteRenderer>();
                renderer.sprite = GetSawSprite(seed);
                renderer.color = Color.white;
                renderer.sortingOrder = 21;
                sawObject.transform.localScale = Vector3.one * 0.78f;
            }

            public void Tick(WorldRoomHazardController controller)
            {
                float movement = Mathf.PingPong((Time.time + phase) * speed * 0.34f, 1f);
                Vector2 position = Vector2.Lerp(start, end, movement);
                root.position = new Vector3(position.x, position.y, -0.22f);
                root.rotation = Quaternion.Euler(0f, 0f, Time.time * 270f + seed % 90);
                float pulse = 1f + Mathf.Sin(Time.time * 9f + phase) * 0.08f;
                root.localScale = Vector3.one * 0.78f * pulse;
                if (glowRenderer != null)
                    glowRenderer.transform.localScale = Vector3.one * (1.25f + pulse * 0.12f);

                controller.TryDamagePlayer(position, 0.75f, 20f, ref nextDamageTime);
            }

            private static Sprite GetSawSprite(int sawSeed)
            {
                if (sawSprite != null) return sawSprite;
                const int size = 28;
                Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Moving Saw Pixel Texture",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 offset = new Vector2(x, y) - center;
                        float distance = offset.magnitude;
                        float angle = Mathf.Atan2(offset.y, offset.x);
                        int tooth = Mathf.FloorToInt((angle + Mathf.PI) / (Mathf.PI * 2f) * 12f);
                        float outerRadius = tooth % 2 == 0 ? 12f : 9.4f;
                        Color pixel = Color.clear;
                        if (distance <= outerRadius && distance >= 3.4f)
                            pixel = new Color(1f, 0.25f, 0.05f, 1f);
                        if (distance < 8.4f && distance >= 3f)
                            pixel = Color.Lerp(new Color(0.28f, 0.3f, 0.38f, 1f),
                                new Color(0.92f, 0.94f, 1f, 1f), distance / 8.4f);
                        if (distance <= 3.2f) pixel = new Color(1f, 0.74f, 0.2f, 1f);
                        texture.SetPixel(x, y, pixel);
                    }
                }
                texture.Apply(false, false);
                sawSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
                sawSprite.name = "Moving Saw Pixel Sprite";
                return sawSprite;
            }

            private static Sprite GetSawGlowSprite()
            {
                if (sawGlowSprite != null) return sawGlowSprite;
                const int size = 28;
                Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Moving Saw Glow Texture",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        float alpha = distance <= 13f
                            ? Mathf.Clamp01((13f - distance) / 7f) * 0.8f
                            : 0f;
                        texture.SetPixel(x, y, new Color(1f, 0.12f, 0.02f, alpha));
                    }
                }
                texture.Apply(false, false);
                sawGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
                sawGlowSprite.name = "Moving Saw Glow Sprite";
                return sawGlowSprite;
            }
        }
    }
}
