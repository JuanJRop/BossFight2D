using System;
using System.Collections;
using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Enemy.EnemyScripts.Movement;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Core;
using Project.Characters.Player.PlayerScripts.Movement;
using Project.Scripts.Boss;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public enum MoleBossState
    {
        Dormant,
        Burrowing,
        Emerging,
        Telegraphing,
        Attacking,
        Recovering,
        PhaseTransition,
        Defeated
    }

    public enum MoleBossAttack
    {
        AimedFan,
        RadialBurst,
        Spiral,
        Corridor,
        RockRain,
        ChargeDash
    }

    public class EnemyAttackController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AttackConfiguration attackConfig;
        [SerializeField] private Transform firePoint;
        [SerializeField] private Transform player;
        [SerializeField] private ObjectPool pool;
        [SerializeField] private Animator animator;
        [SerializeField] private Health bossHealth;
        [SerializeField] private BossPhaseController phaseController;
        [SerializeField] private EnemyMove movement;
        [SerializeField] private Rigidbody2D bossBody;

        [Header("AI Rhythm")]
        [SerializeField, Min(0f)] private float minDelay = 0.8f;
        [SerializeField, Min(0f)] private float maxDelay = 1.5f;
        [SerializeField, Range(0f, 1f)] private float phaseOneBurrowChance = 0.45f;
        [SerializeField, Range(0f, 1f)] private float phaseTwoBurrowChance = 0.7f;
        [SerializeField, Min(0.1f)] private float burrowHideTime = 0.55f;
        [SerializeField, Min(0.1f)] private float phaseTransitionTime = 1.25f;

        [Header("Projectile Balance")]
        [SerializeField, Min(0f)] private float bulletDamage = 8f;
        [SerializeField, Min(0.1f)] private float bulletSpeed = 7f;
        [SerializeField, Min(0.1f)] private float bulletLifeTime = 8f;

        [Header("Rock Rain")]
        [SerializeField, Min(1)] private int phaseOneRockCount = 4;
        [SerializeField, Min(1)] private int phaseTwoRockCount = 7;
        [SerializeField, Min(0.1f)] private float rockWarningTime = 1.15f;
        [SerializeField, Min(0.1f)] private float rockRadius = 1.15f;
        [SerializeField, Min(0f)] private float rockDamage = 18f;

        [Header("Charge Dash")]
        [SerializeField, Min(0.1f)] private float dashChargeTime = 0.85f;
        [SerializeField, Min(0.1f)] private float dashSpeed = 15f;
        [SerializeField, Min(0.1f)] private float dashMaxDistance = 10f;
        [SerializeField, Min(0.1f)] private float dashHitRadius = 1.2f;
        [SerializeField, Min(0f)] private float dashDamage = 22f;
        [SerializeField, Min(0f)] private float dashPushForce = 13f;

        private readonly List<GameObject> runtimeVisuals = new();
        private readonly Queue<MoleBossAttack> phaseOneIntroduction = new();
        private readonly Queue<MoleBossAttack> phaseTwoIntroduction = new();

        private Coroutine attackRoutine;
        private AttackData projectileData;
        private Transform playerCombatTransform;
        private Material telegraphMaterial;
        private MoleBossAttack? previousAttack;
        private int observedPhase;
        private bool phaseTransitionPending;
        private bool missingReferencesReported;

        public event Action<MoleBossState> OnStateChanged;
        public event Action<MoleBossAttack> OnAttackStarted;

        public MoleBossState CurrentState { get; private set; } = MoleBossState.Dormant;
        public MoleBossAttack CurrentAttack { get; private set; }
        public int CurrentPhase => phaseController != null ? phaseController.CurrentPhase : GetHealthPhase();

        private void Awake()
        {
            ResolveReferences();
            BuildIntroductionQueues();
            if (movement != null) movement.SetAiControlled(true);
        }

        private void OnEnable()
        {
            RestartAttacks();
        }

        private void OnDisable()
        {
            StopAttackRoutine();
            CleanupRuntimeVisuals();
            if (bossBody != null) bossBody.linearVelocity = Vector2.zero;
            SetState(bossHealth != null && !bossHealth.IsAlive ? MoleBossState.Defeated : MoleBossState.Dormant);
        }

        private void OnDestroy()
        {
            if (telegraphMaterial != null) Destroy(telegraphMaterial);
        }

        public void RestartAttacks()
        {
            if (!isActiveAndEnabled) return;

            ResolveReferences();
            int nextPhase = CurrentPhase;
            phaseTransitionPending = observedPhase > 0 && nextPhase != observedPhase;

            StopAttackRoutine();
            CleanupRuntimeVisuals();

            if (movement != null)
            {
                movement.SetAiControlled(true);
                movement.ForceEmerge();
            }

            if (bossBody != null) bossBody.linearVelocity = Vector2.zero;
            attackRoutine = StartCoroutine(AttackLoop());
        }

        private IEnumerator AttackLoop()
        {
            yield return null;
            ResolveReferences();

            if (!HasRequiredReferences())
            {
                if (!missingReferencesReported)
                {
                    Debug.LogError("Mole boss AI requires a player, projectile pool, fire point and projectile AttackData.", this);
                    missingReferencesReported = true;
                }

                SetState(MoleBossState.Dormant);
                yield break;
            }

            missingReferencesReported = false;
            observedPhase = CurrentPhase;
            if (phaseTransitionPending)
            {
                phaseTransitionPending = false;
                yield return PlayPhaseTransition();
            }

            while (bossHealth == null || bossHealth.IsAlive)
            {
                int phase = CurrentPhase;
                if (phase != observedPhase)
                {
                    observedPhase = phase;
                    yield return PlayPhaseTransition();
                }

                float delayMultiplier = phase == 2 ? 0.72f : 1f;
                yield return WaitForGameplaySeconds(UnityEngine.Random.Range(minDelay, Mathf.Max(minDelay, maxDelay)) * delayMultiplier);

                if (ShouldBurrow(phase) && movement != null && movement.HasValidSpots)
                {
                    SetState(MoleBossState.Burrowing);
                    yield return movement.BurrowToRandomSpot(burrowHideTime);
                    SetState(MoleBossState.Emerging);
                    yield return WaitForGameplaySeconds(0.2f);
                }

                MoleBossAttack nextAttack = SelectAttack(phase);
                previousAttack = nextAttack;
                CurrentAttack = nextAttack;
                OnAttackStarted?.Invoke(nextAttack);

                yield return ExecuteAttack(nextAttack, phase);
                SetState(MoleBossState.Recovering);
                yield return WaitForGameplaySeconds(phase == 2 ? 0.35f : 0.55f);
            }

            SetState(MoleBossState.Defeated);
            attackRoutine = null;
        }

        private IEnumerator ExecuteAttack(MoleBossAttack attack, int phase)
        {
            switch (attack)
            {
                case MoleBossAttack.AimedFan:
                    yield return AimedFan(phase);
                    break;
                case MoleBossAttack.RadialBurst:
                    yield return RadialBurst(phase);
                    break;
                case MoleBossAttack.Spiral:
                    yield return Spiral(phase);
                    break;
                case MoleBossAttack.Corridor:
                    yield return Corridor(phase);
                    break;
                case MoleBossAttack.RockRain:
                    yield return RockRain(phase);
                    break;
                case MoleBossAttack.ChargeDash:
                    yield return ChargeDash(phase);
                    break;
            }
        }

        private IEnumerator AimedFan(int phase)
        {
            int volleys = phase == 2 ? 5 : 3;
            int projectileCount = phase == 2 ? 5 : 3;
            float spread = phase == 2 ? 13f : 16f;

            for (int volley = 0; volley < volleys; volley++)
            {
                SetState(MoleBossState.Telegraphing);
                Vector2 origin = GetFirePosition();
                Vector2 targetPosition = GetPlayerPosition();
                GameObject line = CreateLine("Aimed fan warning", new Color(1f, 0.65f, 0.1f, 0.9f), 0.08f, origin, targetPosition);
                yield return WaitForGameplaySeconds(0.35f);
                DestroyRuntimeVisual(line);

                SetState(MoleBossState.Attacking);
                Vector2 baseDirection = (GetPlayerPosition() - GetFirePosition()).normalized;
                float middle = (projectileCount - 1) * 0.5f;
                for (int i = 0; i < projectileCount; i++)
                {
                    SpawnProjectile(GetFirePosition(), Rotate(baseDirection, (i - middle) * spread), 1f);
                }

                TriggerAttackAnimation();
                yield return WaitForGameplaySeconds(phase == 2 ? 0.32f : 0.48f);
            }
        }

        private IEnumerator RadialBurst(int phase)
        {
            int rings = phase == 2 ? 3 : 2;
            int count = phase == 2 ? 20 : 14;

            SetState(MoleBossState.Telegraphing);
            GameObject warning = CreateCircle("Radial warning", transform.position, 2f, new Color(1f, 0.45f, 0.05f, 0.9f));
            yield return WaitForGameplaySeconds(0.7f);
            DestroyRuntimeVisual(warning);

            SetState(MoleBossState.Attacking);
            for (int ring = 0; ring < rings; ring++)
            {
                float offset = ring * (180f / count);
                for (int i = 0; i < count; i++)
                {
                    float angle = offset + i * 360f / count;
                    SpawnProjectile(GetFirePosition(), DirectionFromAngle(angle), phase == 2 ? 1.12f : 1f);
                }

                TriggerAttackAnimation();
                yield return WaitForGameplaySeconds(phase == 2 ? 0.42f : 0.6f);
            }
        }

        private IEnumerator Spiral(int phase)
        {
            SetState(MoleBossState.Telegraphing);
            GameObject warning = CreateCircle("Spiral warning", transform.position, 1.5f, new Color(0.9f, 0.2f, 1f, 0.9f));
            yield return WaitForGameplaySeconds(0.65f);
            DestroyRuntimeVisual(warning);

            SetState(MoleBossState.Attacking);
            int steps = phase == 2 ? 34 : 24;
            int arms = phase == 2 ? 4 : 3;
            for (int step = 0; step < steps; step++)
            {
                float angle = step * (phase == 2 ? 15f : 18f);
                for (int arm = 0; arm < arms; arm++)
                {
                    SpawnProjectile(GetFirePosition(), DirectionFromAngle(angle + arm * 360f / arms), 0.92f);
                }

                yield return WaitForGameplaySeconds(phase == 2 ? 0.105f : 0.14f);
            }
        }

        private IEnumerator Corridor(int phase)
        {
            GetArenaBounds(out Vector2 minimum, out Vector2 maximum);
            float safeWidth = phase == 2 ? 2.3f : 2.8f;
            int waves = phase == 2 ? 10 : 7;
            float spacing = phase == 2 ? 0.72f : 0.86f;
            float safeCenter = Mathf.Clamp(GetPlayerPosition().x, minimum.x + safeWidth, maximum.x - safeWidth);

            SetState(MoleBossState.Telegraphing);
            GameObject leftGuide = CreateLine("Corridor left", new Color(0.2f, 1f, 0.45f, 0.85f), 0.07f,
                new Vector2(safeCenter - safeWidth * 0.5f, minimum.y), new Vector2(safeCenter - safeWidth * 0.5f, maximum.y));
            GameObject rightGuide = CreateLine("Corridor right", new Color(0.2f, 1f, 0.45f, 0.85f), 0.07f,
                new Vector2(safeCenter + safeWidth * 0.5f, minimum.y), new Vector2(safeCenter + safeWidth * 0.5f, maximum.y));
            yield return WaitForGameplaySeconds(0.9f);
            DestroyRuntimeVisual(leftGuide);
            DestroyRuntimeVisual(rightGuide);

            SetState(MoleBossState.Attacking);
            for (int wave = 0; wave < waves; wave++)
            {
                float movementOffset = Mathf.Sin(wave * 0.72f) * (phase == 2 ? 1.3f : 0.75f);
                float waveSafeCenter = Mathf.Clamp(safeCenter + movementOffset, minimum.x + safeWidth, maximum.x - safeWidth);
                bool fromTop = phase == 1 || wave % 2 == 0;
                float y = fromTop ? maximum.y + 0.35f : minimum.y - 0.35f;
                Vector2 direction = fromTop ? Vector2.down : Vector2.up;

                for (float x = minimum.x; x <= maximum.x; x += spacing)
                {
                    if (Mathf.Abs(x - waveSafeCenter) < safeWidth * 0.5f) continue;
                    SpawnProjectile(new Vector2(x, y), direction, phase == 2 ? 1.18f : 1f);
                }

                yield return WaitForGameplaySeconds(phase == 2 ? 0.34f : 0.48f);
            }
        }

        private IEnumerator RockRain(int phase)
        {
            int totalRocks = phase == 2 ? phaseTwoRockCount : phaseOneRockCount;
            int rocksPerWave = phase == 2 ? 3 : 2;
            int spawned = 0;

            while (spawned < totalRocks)
            {
                int waveCount = Mathf.Min(rocksPerWave, totalRocks - spawned);
                var markers = new List<RockMarker>(waveCount);
                GetArenaBounds(out Vector2 minimum, out Vector2 maximum);

                for (int i = 0; i < waveCount; i++)
                {
                    Vector2 target = GetPlayerPosition();
                    if (i > 0) target += UnityEngine.Random.insideUnitCircle * (phase == 2 ? 2.4f : 1.8f);
                    target.x = Mathf.Clamp(target.x, minimum.x + rockRadius, maximum.x - rockRadius);
                    target.y = Mathf.Clamp(target.y, minimum.y + rockRadius, maximum.y - rockRadius);
                    markers.Add(CreateRockMarker(target));
                    spawned++;
                }

                SetState(MoleBossState.Telegraphing);
                float elapsed = 0f;
                while (elapsed < rockWarningTime)
                {
                    if (!IsGameplayPaused())
                    {
                        elapsed += Time.deltaTime;
                        float progress = Mathf.Clamp01(elapsed / rockWarningTime);
                        UpdateRockMarkers(markers, progress);
                    }

                    yield return null;
                }

                SetState(MoleBossState.Attacking);
                foreach (RockMarker marker in markers)
                {
                    ResolveRockImpact(marker.Target, phase);
                    DestroyRockMarker(marker);
                }

                yield return WaitForGameplaySeconds(phase == 2 ? 0.28f : 0.45f);
            }
        }

        private IEnumerator ChargeDash(int phase)
        {
            if (bossBody == null) yield break;

            if (movement != null) movement.ForceEmerge();
            Vector2 start = bossBody.position;
            Vector2 direction = (GetPlayerPosition() - start).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.down;

            GetArenaBounds(out Vector2 minimum, out Vector2 maximum);
            Vector2 destination = start + direction * dashMaxDistance;
            destination.x = Mathf.Clamp(destination.x, minimum.x + 0.6f, maximum.x - 0.6f);
            destination.y = Mathf.Clamp(destination.y, minimum.y + 0.6f, maximum.y - 0.6f);

            SetState(MoleBossState.Telegraphing);
            GameObject guide = CreateLine("Dash trajectory", new Color(1f, 0.08f, 0.08f, 0.95f), 0.12f, start, destination);
            LineRenderer guideLine = guide != null ? guide.GetComponent<LineRenderer>() : null;
            float chargeElapsed = 0f;
            while (chargeElapsed < dashChargeTime)
            {
                if (!IsGameplayPaused())
                {
                    chargeElapsed += Time.deltaTime;
                    if (guideLine != null)
                    {
                        float pulse = 0.1f + Mathf.PingPong(chargeElapsed * 0.22f, 0.13f);
                        guideLine.startWidth = pulse;
                        guideLine.endWidth = pulse;
                    }
                }

                yield return null;
            }

            SetState(MoleBossState.Attacking);
            TriggerAttackAnimation();
            bool hitPlayer = false;
            float speed = dashSpeed * (phase == 2 ? 1.18f : 1f);
            while (Vector2.Distance(bossBody.position, destination) > 0.08f)
            {
                if (!IsGameplayPaused())
                {
                    Vector2 next = Vector2.MoveTowards(bossBody.position, destination, speed * Time.fixedDeltaTime);
                    bossBody.MovePosition(next);

                    if (!hitPlayer && Vector2.Distance(next, GetPlayerPosition()) <= dashHitRadius)
                    {
                        hitPlayer = TryHitPlayerWithDash(direction);
                    }
                }

                yield return new WaitForFixedUpdate();
            }

            bossBody.MovePosition(destination);
            bossBody.linearVelocity = Vector2.zero;
            DestroyRuntimeVisual(guide);
        }

        private IEnumerator PlayPhaseTransition()
        {
            SetState(MoleBossState.PhaseTransition);
            if (movement != null && movement.HasValidSpots)
            {
                yield return movement.BurrowToRandomSpot(Mathf.Max(0.2f, phaseTransitionTime * 0.45f));
            }

            GameObject ring = CreateCircle("Phase two shockwave", transform.position, 0.5f, new Color(1f, 0.15f, 0.05f, 1f));
            LineRenderer line = ring != null ? ring.GetComponent<LineRenderer>() : null;
            float elapsed = 0f;
            while (elapsed < phaseTransitionTime)
            {
                if (!IsGameplayPaused())
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / phaseTransitionTime);
                    UpdateCircle(line, transform.position, Mathf.Lerp(0.5f, 3.5f, progress));
                    if (line != null)
                    {
                        Color color = line.startColor;
                        color.a = 1f - progress;
                        line.startColor = color;
                        line.endColor = color;
                    }
                }

                yield return null;
            }

            DestroyRuntimeVisual(ring);
            yield return RadialBurst(2);
        }

        private MoleBossAttack SelectAttack(int phase)
        {
            Queue<MoleBossAttack> introduction = phase == 2 ? phaseTwoIntroduction : phaseOneIntroduction;
            if (introduction.Count > 0) return introduction.Dequeue();

            MoleBossAttack[] choices = phase == 2
                ? new[]
                {
                    MoleBossAttack.Spiral, MoleBossAttack.Spiral,
                    MoleBossAttack.Corridor, MoleBossAttack.Corridor,
                    MoleBossAttack.RockRain, MoleBossAttack.RockRain, MoleBossAttack.RockRain,
                    MoleBossAttack.ChargeDash, MoleBossAttack.ChargeDash,
                    MoleBossAttack.RadialBurst, MoleBossAttack.AimedFan
                }
                : new[]
                {
                    MoleBossAttack.AimedFan, MoleBossAttack.AimedFan,
                    MoleBossAttack.RadialBurst, MoleBossAttack.RadialBurst,
                    MoleBossAttack.RockRain, MoleBossAttack.RockRain,
                    MoleBossAttack.Corridor, MoleBossAttack.ChargeDash
                };

            MoleBossAttack selected = choices[UnityEngine.Random.Range(0, choices.Length)];
            for (int attempt = 0; attempt < 6 && previousAttack.HasValue && selected == previousAttack.Value; attempt++)
            {
                selected = choices[UnityEngine.Random.Range(0, choices.Length)];
            }

            if (player != null && Vector2.Distance(transform.position, GetPlayerPosition()) > 7f && phase == 2)
            {
                if (UnityEngine.Random.value < 0.35f) selected = MoleBossAttack.ChargeDash;
            }

            return selected;
        }

        private bool SpawnProjectile(Vector2 position, Vector2 direction, float speedMultiplier, float damageMultiplier = 1f)
        {
            if (pool == null || projectileData == null || projectileData.bulletPrefab == null) return false;
            if (direction.sqrMagnitude < 0.001f) return false;

            direction.Normalize();
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GameObject projectile = pool.GetObject(projectileData.bulletPrefab, position, Quaternion.Euler(0f, 0f, angle));
            if (projectile == null) return false;

            AttackEntity entity = projectile.GetComponentInChildren<AttackEntity>(true);
            Rigidbody2D body = projectile.GetComponentInChildren<Rigidbody2D>(true);
            if (entity == null || body == null)
            {
                pool.ReturnObject(projectile, projectileData.bulletPrefab);
                return false;
            }

            entity.SetPool(pool, projectileData.bulletPrefab, projectile, BulletOwner.Enemy,
                bulletDamage * Mathf.Max(0f, damageMultiplier), bulletLifeTime);
            body.linearVelocity = direction * bulletSpeed * Mathf.Max(0.1f, speedMultiplier);
            return true;
        }

        private RockMarker CreateRockMarker(Vector2 target)
        {
            GameObject warning = CreateCircle("Rock impact warning", target, rockRadius, new Color(1f, 0.72f, 0.05f, 0.95f));
            GameObject rock = new("Falling rock");
            rock.transform.position = target + Vector2.up * 6f;
            runtimeVisuals.Add(rock);

            SpriteRenderer renderer = rock.AddComponent<SpriteRenderer>();
            SpriteRenderer source = projectileData != null && projectileData.bulletPrefab != null
                ? projectileData.bulletPrefab.GetComponentInChildren<SpriteRenderer>(true)
                : null;
            renderer.sprite = source != null ? source.sprite : null;
            renderer.color = new Color(0.42f, 0.34f, 0.28f, 1f);
            renderer.sortingOrder = 30;
            rock.transform.localScale = Vector3.one * 0.25f;

            return new RockMarker(target, warning, rock);
        }

        private void UpdateRockMarkers(List<RockMarker> markers, float progress)
        {
            foreach (RockMarker marker in markers)
            {
                if (marker.Rock != null)
                {
                    float eased = progress * progress;
                    marker.Rock.transform.position = Vector3.Lerp(marker.Target + Vector2.up * 6f, marker.Target, eased);
                    marker.Rock.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.45f, eased);
                }

                if (marker.Warning != null)
                {
                    LineRenderer line = marker.Warning.GetComponent<LineRenderer>();
                    if (line != null)
                    {
                        Color warningColor = Color.Lerp(new Color(1f, 0.75f, 0.05f, 0.8f), new Color(1f, 0.05f, 0.02f, 1f), progress);
                        line.startColor = warningColor;
                        line.endColor = warningColor;
                        line.startWidth = Mathf.Lerp(0.06f, 0.16f, progress);
                        line.endWidth = line.startWidth;
                    }
                }
            }
        }

        private void ResolveRockImpact(Vector2 target, int phase)
        {
            float distance = Vector2.Distance(GetPlayerPosition(), target);
            PlayerDodge dodge = GetPlayerComponent<PlayerDodge>();
            if (distance <= rockRadius && (dodge == null || !dodge.IsInvulnerable))
            {
                Health playerHealth = GetPlayerComponent<Health>();
                if (playerHealth != null) playerHealth.TakeDamage(rockDamage);
            }

            int shards = phase == 2 ? 10 : 6;
            for (int i = 0; i < shards; i++)
            {
                Vector2 direction = DirectionFromAngle(i * 360f / shards);
                SpawnProjectile(target + direction * rockRadius, direction, 0.72f, 0.65f);
            }
        }

        private bool TryHitPlayerWithDash(Vector2 direction)
        {
            PlayerDodge dodge = GetPlayerComponent<PlayerDodge>();
            if (dodge != null && dodge.IsInvulnerable) return false;

            Health playerHealth = GetPlayerComponent<Health>();
            if (playerHealth != null) playerHealth.TakeDamage(dashDamage);

            PlayerMove playerMove = GetPlayerComponent<PlayerMove>();
            if (playerMove != null) playerMove.ApplyKnockback(direction * dashPushForce, 0.32f);
            return true;
        }

        private GameObject CreateLine(string objectName, Color color, float width, params Vector2[] positions)
        {
            if (positions == null || positions.Length < 2) return null;

            GameObject visual = new(objectName);
            runtimeVisuals.Add(visual);
            LineRenderer line = visual.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = positions.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 4;
            line.sortingOrder = 25;
            line.material = GetTelegraphMaterial();

            for (int i = 0; i < positions.Length; i++) line.SetPosition(i, positions[i]);
            return visual;
        }

        private GameObject CreateCircle(string objectName, Vector2 center, float radius, Color color)
        {
            const int segments = 48;
            GameObject visual = new(objectName);
            runtimeVisuals.Add(visual);
            LineRenderer line = visual.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = segments;
            line.startWidth = 0.09f;
            line.endWidth = 0.09f;
            line.startColor = color;
            line.endColor = color;
            line.numCornerVertices = 3;
            line.sortingOrder = 25;
            line.material = GetTelegraphMaterial();
            UpdateCircle(line, center, radius);
            return visual;
        }

        private static void UpdateCircle(LineRenderer line, Vector2 center, float radius)
        {
            if (line == null) return;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private Material GetTelegraphMaterial()
        {
            if (telegraphMaterial != null) return telegraphMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            telegraphMaterial = shader != null ? new Material(shader) : null;
            return telegraphMaterial;
        }

        private void DestroyRockMarker(RockMarker marker)
        {
            DestroyRuntimeVisual(marker.Warning);
            DestroyRuntimeVisual(marker.Rock);
        }

        private void DestroyRuntimeVisual(GameObject visual)
        {
            if (visual == null) return;
            runtimeVisuals.Remove(visual);
            Destroy(visual);
        }

        private void CleanupRuntimeVisuals()
        {
            foreach (GameObject visual in runtimeVisuals)
            {
                if (visual != null) Destroy(visual);
            }

            runtimeVisuals.Clear();
        }

        private void ResolveReferences()
        {
            if (firePoint == null) firePoint = transform;
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) player = playerObject.transform;
            }

            if (player != null && playerCombatTransform == null)
            {
                Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
                if (playerBody == null) playerBody = player.GetComponentInChildren<Rigidbody2D>();
                playerCombatTransform = playerBody != null ? playerBody.transform : player;
            }

            if (pool == null) pool = FindFirstObjectByType<ObjectPool>();
            if (bossHealth == null) bossHealth = GetComponent<Health>();
            if (phaseController == null) phaseController = GetComponent<BossPhaseController>();
            if (movement == null) movement = GetComponent<EnemyMove>();
            if (bossBody == null) bossBody = GetComponent<Rigidbody2D>();
            if (animator == null) animator = GetComponent<Animator>();

            if (projectileData == null && attackConfig != null)
            {
                AttackExecutorBase executor = attackConfig.GetRandomExecutor();
                projectileData = executor != null ? executor.Data : null;
            }
        }

        private bool HasRequiredReferences()
        {
            return player != null && pool != null && firePoint != null && projectileData != null && projectileData.bulletPrefab != null;
        }

        private void BuildIntroductionQueues()
        {
            if (phaseOneIntroduction.Count == 0)
            {
                phaseOneIntroduction.Enqueue(MoleBossAttack.AimedFan);
                phaseOneIntroduction.Enqueue(MoleBossAttack.RadialBurst);
                phaseOneIntroduction.Enqueue(MoleBossAttack.RockRain);
                phaseOneIntroduction.Enqueue(MoleBossAttack.ChargeDash);
                phaseOneIntroduction.Enqueue(MoleBossAttack.Corridor);
            }

            if (phaseTwoIntroduction.Count == 0)
            {
                phaseTwoIntroduction.Enqueue(MoleBossAttack.Spiral);
                phaseTwoIntroduction.Enqueue(MoleBossAttack.RockRain);
                phaseTwoIntroduction.Enqueue(MoleBossAttack.ChargeDash);
                phaseTwoIntroduction.Enqueue(MoleBossAttack.Corridor);
            }
        }

        private void GetArenaBounds(out Vector2 minimum, out Vector2 maximum)
        {
            Camera camera = Camera.main;
            if (camera != null && camera.orthographic)
            {
                float distance = Mathf.Abs(camera.transform.position.z);
                minimum = camera.ViewportToWorldPoint(new Vector3(0.04f, 0.06f, distance));
                maximum = camera.ViewportToWorldPoint(new Vector3(0.96f, 0.94f, distance));
                return;
            }

            minimum = new Vector2(-9f, -5f);
            maximum = new Vector2(9f, 5f);
        }

        private IEnumerator WaitForGameplaySeconds(float duration)
        {
            float elapsed = 0f;
            while (elapsed < Mathf.Max(0f, duration))
            {
                if (!IsGameplayPaused()) elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private bool ShouldBurrow(int phase)
        {
            float chance = phase == 2 ? phaseTwoBurrowChance : phaseOneBurrowChance;
            return UnityEngine.Random.value < chance;
        }

        private bool IsGameplayPaused()
        {
            return UIManager.instance != null && UIManager.instance.IsPaused;
        }

        private int GetHealthPhase()
        {
            return bossHealth != null && bossHealth.NormalizedHealth <= 0.4f ? 2 : 1;
        }

        private Vector2 GetFirePosition()
        {
            return firePoint != null ? firePoint.position : transform.position;
        }

        private Vector2 GetPlayerPosition()
        {
            return playerCombatTransform != null ? playerCombatTransform.position : Vector2.zero;
        }

        private T GetPlayerComponent<T>() where T : Component
        {
            if (player == null) return null;
            T component = player.GetComponent<T>();
            return component != null ? component : player.GetComponentInChildren<T>();
        }

        private void TriggerAttackAnimation()
        {
            if (animator != null) animator.SetTrigger("Attack");
        }

        private void SetState(MoleBossState state)
        {
            if (CurrentState == state) return;
            CurrentState = state;
            OnStateChanged?.Invoke(CurrentState);
        }

        private void StopAttackRoutine()
        {
            if (attackRoutine == null) return;
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        private static Vector2 DirectionFromAngle(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(direction.x * cosine - direction.y * sine, direction.x * sine + direction.y * cosine);
        }

        private readonly struct RockMarker
        {
            public RockMarker(Vector2 target, GameObject warning, GameObject rock)
            {
                Target = target;
                Warning = warning;
                Rock = rock;
            }

            public Vector2 Target { get; }
            public GameObject Warning { get; }
            public GameObject Rock { get; }
        }

        private void OnValidate()
        {
            maxDelay = Mathf.Max(minDelay, maxDelay);
            bulletSpeed = Mathf.Max(0.1f, bulletSpeed);
            bulletLifeTime = Mathf.Max(0.1f, bulletLifeTime);
            rockRadius = Mathf.Max(0.1f, rockRadius);
            dashChargeTime = Mathf.Max(0.1f, dashChargeTime);
            dashSpeed = Mathf.Max(0.1f, dashSpeed);
            dashMaxDistance = Mathf.Max(0.1f, dashMaxDistance);
        }
    }
}
