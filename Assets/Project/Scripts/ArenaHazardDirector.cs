using System.Collections;
using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Movement;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Scripts.Arena
{
    [DefaultExecutionOrder(-300)]
    public sealed class ArenaHazardDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ArenaBounds arenaBounds;
        [SerializeField] private Transform player;

        [Header("Rhythm")]
        [SerializeField, Min(0f)] private float initialDelay = 2.5f;
        [SerializeField, Min(0.1f)] private float cycleDelay = 3.2f;
        [SerializeField, Min(1)] private int baseLaserCount = 3;
        [SerializeField, Min(1)] private int hardLaserCount = 4;
        [SerializeField, Min(1)] private int hardModeAfterCycles = 4;

        [Header("Laser")]
        [SerializeField, Min(0.1f)] private float warningTime = 0.82f;
        [SerializeField, Min(0.1f)] private float hardWarningTime = 0.58f;
        [SerializeField, Min(0.1f)] private float activeTime = 0.62f;
        [SerializeField, Min(0.1f)] private float hardActiveTime = 0.55f;
        [SerializeField, Min(0.1f)] private float laserWidth = 0.9f;
        [SerializeField, Min(0.1f)] private float hardLaserWidth = 1.05f;
        [SerializeField, Min(0f)] private float laserDamage = 24f;
        [SerializeField, Min(0f)] private float hardLaserDamage = 30f;
        [SerializeField, Range(0f, 0.2f)] private float positionJitter = 0.055f;

        private readonly List<GameObject> runtimeVisuals = new();
        private Coroutine hazardRoutine;
        private Transform playerCombatTransform;
        private Health playerHealth;
        private PlayerDodge playerDodge;
        private Material laserMaterial;
        private int completedCycles;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            hazardRoutine = StartCoroutine(HazardLoop());
        }

        private void OnDisable()
        {
            if (hazardRoutine != null) StopCoroutine(hazardRoutine);
            hazardRoutine = null;
            ReleaseAll();
        }

        private void OnDestroy()
        {
            if (laserMaterial != null) Destroy(laserMaterial);
        }

        private IEnumerator HazardLoop()
        {
            yield return null;
            ResolveReferences();
            if (arenaBounds == null || player == null)
            {
                Debug.LogError("ArenaHazardDirector requires ArenaBounds and a tagged Player.", this);
                hazardRoutine = null;
                yield break;
            }

            yield return Wait(initialDelay);
            while (playerHealth == null || playerHealth.IsAlive)
            {
                yield return ExecuteLaserCycle();
                completedCycles++;
                yield return Wait(cycleDelay);
            }

            hazardRoutine = null;
            ReleaseAll();
        }

        private IEnumerator ExecuteLaserCycle()
        {
            bool hardMode = completedCycles >= hardModeAfterCycles;
            int count = hardMode ? hardLaserCount : baseLaserCount;
            bool horizontal = completedCycles % 2 == 0;
            List<Beam> beams = CreateParallelBeams(count, horizontal);

            float telegraphDuration = hardMode ? hardWarningTime : warningTime;
            yield return RunWarnings(beams, telegraphDuration);

            Activate(beams, hardMode);
            yield return RunActive(beams, hardMode);
            Release(beams);
        }

        private List<Beam> CreateParallelBeams(int count, bool horizontal)
        {
            List<Beam> beams = new(count);
            for (int i = 0; i < count; i++)
            {
                float normalized = (i + 1f) / (count + 1f);
                normalized = Mathf.Clamp(normalized + Random.Range(-positionJitter, positionJitter), 0.08f, 0.92f);

                Vector2 start;
                Vector2 end;
                if (horizontal)
                    arenaBounds.GetHorizontalPath(normalized, out start, out end);
                else
                    arenaBounds.GetVerticalPath(normalized, out start, out end);

                GameObject warning = CreateLine("Arena laser warning",
                    new Color(0.05f, 0.9f, 1f, 0.72f), 0.1f, start, end);
                beams.Add(new Beam(start, end, warning));
            }
            return beams;
        }

        private IEnumerator RunWarnings(IEnumerable<Beam> beams, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!IsPaused)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    float pulse = 0.08f + Mathf.PingPong(elapsed * 0.5f, 0.18f);
                    foreach (Beam beam in beams)
                    {
                        SetWidth(beam.Warning, pulse);
                        SetAlpha(beam.Warning, Mathf.Lerp(0.45f, 1f, progress));
                    }
                }
                yield return null;
            }
        }

        private void Activate(IEnumerable<Beam> beams, bool hardMode)
        {
            float width = hardMode ? hardLaserWidth : laserWidth;
            foreach (Beam beam in beams)
            {
                ReleaseVisual(beam.Warning);
                beam.Glow = CreateLine("Arena laser glow",
                    new Color(0.02f, 0.72f, 1f, 0.5f), width * 1.75f, beam.Start, beam.End);
                beam.Core = CreateLine("Arena laser core", Color.white, width * 0.3f, beam.Start, beam.End);
            }
        }

        private IEnumerator RunActive(IEnumerable<Beam> beams, bool hardMode)
        {
            float duration = hardMode ? hardActiveTime : activeTime;
            float width = hardMode ? hardLaserWidth : laserWidth;
            float damage = hardMode ? hardLaserDamage : laserDamage;
            bool playerHit = false;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (!IsPaused)
                {
                    elapsed += Time.deltaTime;
                    float pulse = 0.88f + Mathf.Sin(elapsed * 28f) * 0.12f;
                    foreach (Beam beam in beams)
                    {
                        SetWidth(beam.Glow, width * 1.75f * pulse);
                        if (playerHit || DistanceToSegment(playerCombatTransform.position, beam.Start, beam.End) > width * 0.5f)
                            continue;

                        if (playerDodge == null || !playerDodge.IsInvulnerable)
                        {
                            if (playerHealth != null) playerHealth.TakeDamage(damage);
                            playerHit = true;
                        }
                    }
                }
                yield return null;
            }
        }

        private void ResolveReferences()
        {
            if (arenaBounds == null) arenaBounds = GetComponent<ArenaBounds>();
            if (arenaBounds == null) arenaBounds = ArenaBounds.Instance;

            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) player = playerObject.transform;
            }

            if (player == null) return;
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            if (playerBody == null) playerBody = player.GetComponentInChildren<Rigidbody2D>();
            playerCombatTransform = playerBody != null ? playerBody.transform : player;

            playerHealth = player.GetComponent<Health>();
            if (playerHealth == null) playerHealth = player.GetComponentInChildren<Health>();
            playerDodge = player.GetComponent<PlayerDodge>();
            if (playerDodge == null) playerDodge = player.GetComponentInChildren<PlayerDodge>();
        }

        private GameObject CreateLine(string objectName, Color color, float width, Vector2 start, Vector2 end)
        {
            GameObject visual = new(objectName);
            visual.transform.SetParent(transform);
            runtimeVisuals.Add(visual);

            LineRenderer line = visual.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 4;
            line.sortingOrder = 25;
            line.material = GetLaserMaterial();
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            return visual;
        }

        private Material GetLaserMaterial()
        {
            if (laserMaterial != null) return laserMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            laserMaterial = shader != null ? new Material(shader) : null;
            return laserMaterial;
        }

        private IEnumerator Wait(float duration)
        {
            float elapsed = 0f;
            while (elapsed < Mathf.Max(0f, duration))
            {
                if (!IsPaused) elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private bool IsPaused => UIManager.instance != null && UIManager.instance.IsPaused;

        private void Release(IEnumerable<Beam> beams)
        {
            foreach (Beam beam in beams)
            {
                ReleaseVisual(beam.Warning);
                ReleaseVisual(beam.Glow);
                ReleaseVisual(beam.Core);
            }
        }

        private void ReleaseVisual(GameObject visual)
        {
            if (visual == null) return;
            runtimeVisuals.Remove(visual);
            Destroy(visual);
        }

        private void ReleaseAll()
        {
            foreach (GameObject visual in runtimeVisuals)
            {
                if (visual != null) Destroy(visual);
            }
            runtimeVisuals.Clear();
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            if (segment.sqrMagnitude < 0.001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, start + segment * t);
        }

        private static void SetWidth(GameObject visual, float width)
        {
            if (visual == null) return;
            LineRenderer line = visual.GetComponent<LineRenderer>();
            if (line == null) return;
            line.startWidth = width;
            line.endWidth = width;
        }

        private static void SetAlpha(GameObject visual, float alpha)
        {
            if (visual == null) return;
            LineRenderer line = visual.GetComponent<LineRenderer>();
            if (line == null) return;
            Color start = line.startColor;
            Color end = line.endColor;
            start.a = alpha;
            end.a = alpha;
            line.startColor = start;
            line.endColor = end;
        }

        private sealed class Beam
        {
            public Beam(Vector2 start, Vector2 end, GameObject warning)
            {
                Start = start;
                End = end;
                Warning = warning;
            }

            public Vector2 Start { get; }
            public Vector2 End { get; }
            public GameObject Warning { get; }
            public GameObject Glow { get; set; }
            public GameObject Core { get; set; }
        }

        private void OnValidate()
        {
            initialDelay = Mathf.Max(0f, initialDelay);
            cycleDelay = Mathf.Max(0.1f, cycleDelay);
            baseLaserCount = Mathf.Max(1, baseLaserCount);
            hardLaserCount = Mathf.Max(baseLaserCount, hardLaserCount);
            hardModeAfterCycles = Mathf.Max(1, hardModeAfterCycles);
            warningTime = Mathf.Max(0.1f, warningTime);
            hardWarningTime = Mathf.Max(0.1f, hardWarningTime);
            activeTime = Mathf.Max(0.1f, activeTime);
            hardActiveTime = Mathf.Max(0.1f, hardActiveTime);
            laserWidth = Mathf.Max(0.1f, laserWidth);
            hardLaserWidth = Mathf.Max(laserWidth, hardLaserWidth);
        }
    }
}
