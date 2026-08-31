using System.Collections;
using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Scripts.Controller;
using Project.Scripts.World;
using UnityEngine;

namespace Project.Scripts.Progression
{
    public sealed class RunAbilityController : MonoBehaviour
    {
        private const float AutoBulletRange = 15f;
        private const float AbilityRange = 16f;
        private static readonly Vector2 RoomMinimum = new(-16.1f, -10.1f);
        private static readonly Vector2 RoomMaximum = new(16.1f, 10.1f);
        private static readonly HashSet<RunAbilityController> ActiveControllers = new();

        private readonly List<RunAbilityOrb> orbs = new();
        private readonly List<Health> targets = new();
        private readonly HashSet<Health> selectedTargets = new();
        private readonly List<GameObject> activeEffects = new();

        private Health playerHealth;
        private Transform player;
        private AttackPlayer attack;
        private PowerUp powerUp;
        private Material effectMaterial;
        private float autoBulletTimer;
        private float radiantBulletTimer;
        private float bladeWaveTimer;
        private float chainLaserTimer;
        private float firestormTimer;
        private float whirlwindTimer;
        private float healingTimer;
        private float sanctuaryTimer;
        private bool configured;

        public static RunAbilityController Ensure(Health health)
        {
            if (health == null) return null;
            RunAbilityController controller = health.GetComponent<RunAbilityController>();
            if (controller == null) controller = health.gameObject.AddComponent<RunAbilityController>();
            controller.Configure(health);
            return controller;
        }

        public static void ResetRoomEffects()
        {
            List<RunAbilityController> snapshot = new(ActiveControllers);
            foreach (RunAbilityController controller in snapshot)
                controller?.ResetRoomEffectsInternal();
        }

        private void Awake()
        {
            ActiveControllers.Add(this);
            if (playerHealth == null) playerHealth = GetComponent<Health>();
            if (playerHealth != null) Configure(playerHealth);
        }

        private void OnDestroy()
        {
            ActiveControllers.Remove(this);
            ClearOrbs();
            ClearEffects();
            if (effectMaterial != null) Destroy(effectMaterial);
        }

        private void Configure(Health health)
        {
            playerHealth = health;
            player = health.transform.root != null && health.transform.root.CompareTag("Player")
                ? health.transform.root
                : health.transform;
            attack = FindPlayerComponent<AttackPlayer>();
            powerUp = FindPlayerComponent<PowerUp>();
            configured = true;
            ActiveControllers.Add(this);
        }

        private void Update()
        {
            if (!configured || playerHealth == null || player == null) return;
            if (!playerHealth.IsAlive)
            {
                ClearOrbs();
                return;
            }
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;

            SyncOrbs();
            float deltaTime = Time.deltaTime;

            int autoBulletRank = RunSession.GetSkillRank(RunSkillType.ArrowRain);
            if (autoBulletRank > 0)
            {
                autoBulletTimer -= deltaTime;
                if (autoBulletTimer <= 0f)
                {
                    bool fired = FireAutomaticBullet(autoBulletRank);
                    autoBulletTimer = fired
                        ? Mathf.Max(0.3f, 1.02f - autoBulletRank * 0.14f)
                        : 0.2f;
                }
            }

            int radiantBoltRank = RunSession.GetSkillRank(RunSkillType.RadiantBolts);
            if (radiantBoltRank > 0)
            {
                radiantBulletTimer -= deltaTime;
                if (radiantBulletTimer <= 0f)
                {
                    bool fired = FireRadiantBullet(radiantBoltRank);
                    radiantBulletTimer = fired
                        ? Mathf.Max(0.42f, 1.3f - radiantBoltRank * 0.14f)
                        : 0.2f;
                }
            }

            int bladeWaveRank = RunSession.GetSkillRank(RunSkillType.BladeWave);
            if (bladeWaveRank > 0)
            {
                bladeWaveTimer -= deltaTime;
                if (bladeWaveTimer <= 0f)
                {
                    FireBladeWave(bladeWaveRank);
                    bladeWaveTimer = Mathf.Max(1.55f, 3.4f - bladeWaveRank * 0.38f);
                }
            }

            int chainLaserRank = RunSession.GetSkillRank(RunSkillType.ArcaneBeam);
            if (chainLaserRank > 0)
            {
                chainLaserTimer -= deltaTime;
                if (chainLaserTimer <= 0f)
                {
                    FireChainLaser(chainLaserRank);
                    chainLaserTimer = Mathf.Max(2.2f, 4.8f - chainLaserRank * 0.52f);
                }
            }

            int firestormRank = RunSession.GetSkillRank(RunSkillType.Firestorm);
            if (firestormRank > 0)
            {
                firestormTimer -= deltaTime;
                if (firestormTimer <= 0f)
                {
                    FireFirestorm(firestormRank);
                    firestormTimer = Mathf.Max(2.4f, 5.2f - firestormRank * 0.55f);
                }
            }

            int whirlwindRank = RunSession.GetSkillRank(RunSkillType.Whirlwind);
            if (whirlwindRank > 0)
            {
                whirlwindTimer -= deltaTime;
                if (whirlwindTimer <= 0f)
                {
                    FireWhirlwind(whirlwindRank);
                    whirlwindTimer = Mathf.Max(2.1f, 4.5f - whirlwindRank * 0.5f);
                }
            }

            int healingRank = RunSession.GetSkillRank(RunSkillType.HealingAura);
            if (healingRank > 0)
            {
                healingTimer -= deltaTime;
                if (healingTimer <= 0f)
                {
                    playerHealth.Heal(2.5f + healingRank * 3f);
                    healingTimer = Mathf.Max(1.8f, 4.2f - healingRank * 0.48f);
                }
            }

            int sanctuaryRank = RunSession.GetSkillRank(RunSkillType.Sanctuary);
            if (sanctuaryRank > 0)
            {
                sanctuaryTimer -= deltaTime;
                if (sanctuaryTimer <= 0f)
                {
                    FireSanctuary(sanctuaryRank);
                    sanctuaryTimer = Mathf.Max(3.4f, 6.4f - sanctuaryRank * 0.55f);
                }
            }
        }

        private void SyncOrbs()
        {
            int rank = RunSession.GetSkillRank(RunSkillType.ArcaneBeam);
            int desiredCount = Mathf.Min(3, rank);

            for (int index = orbs.Count - 1; index >= 0; index--)
            {
                if (orbs[index] != null) continue;
                orbs.RemoveAt(index);
            }

            while (orbs.Count < desiredCount)
            {
                RunAbilityOrb orb = RunAbilityOrb.CreateRuntime(player, orbs.Count);
                if (orb == null) break;
                orbs.Add(orb);
            }
        }

        private bool FireAutomaticBullet(int rank)
        {
            if (attack == null) attack = FindPlayerComponent<AttackPlayer>();
            if (attack == null) return false;

            Health target = FindClosestEnemy(player.position, AutoBulletRange);
            if (target == null) return false;

            Vector2 direction = ((Vector2)target.transform.position - (Vector2)player.position).normalized;
            return attack.TryShootAutomatic(direction, 0.68f + rank * 0.18f,
                1.08f + rank * 0.05f, RunSession.GetClassColor(RunClassType.Archer));
        }

        private bool FireRadiantBullet(int rank)
        {
            if (attack == null) attack = FindPlayerComponent<AttackPlayer>();
            if (attack == null) return false;

            Health target = FindClosestEnemy(player.position, AutoBulletRange);
            if (target == null) return false;

            Vector2 direction = ((Vector2)target.transform.position - (Vector2)player.position).normalized;
            return attack.TryShootAutomatic(direction, 0.78f + rank * 0.2f,
                1.14f + rank * 0.06f, RunSession.GetClassColor(RunClassType.Healer));
        }

        private void FireChainLaser(int rank)
        {
            CollectEnemies(player.position, AbilityRange);
            if (targets.Count == 0) return;

            int targetCount = Mathf.Min(targets.Count, 1 + rank);
            selectedTargets.Clear();
            List<Vector3> points = new(targetCount + 1) { player.position };
            Vector2 previousPosition = player.position;

            for (int index = 0; index < targetCount; index++)
            {
                Health target = FindClosestUnusedEnemy(previousPosition);
                if (target == null) break;
                selectedTargets.Add(target);
                previousPosition = target.transform.position;
                points.Add(target.transform.position);
                ApplyAbilityDamage(target, 22f + rank * 12f);
            }

            if (points.Count > 1) SpawnLineEffect("Arcane Beam", points,
                RunSession.GetClassColor(RunClassType.Mage), 0.2f, 0.24f);
        }

        private void FireFirestorm(int rank)
        {
            float radius = 2.4f + rank * 0.42f;
            CollectEnemies(player.position, radius);
            foreach (Health target in targets)
                ApplyAbilityDamage(target, 30f + rank * 15f);

            SpawnRingEffect(player.position, radius, RunSession.GetClassColor(RunClassType.Mage),
                0.3f, 0.16f);
        }

        private void FireBladeWave(int rank)
        {
            Vector2 direction = player.right;
            CollectEnemies(player.position, 3.4f + rank * 0.35f);
            foreach (Health target in targets)
            {
                Vector2 offset = ((Vector2)target.transform.position - (Vector2)player.position).normalized;
                if (Vector2.Dot(direction.normalized, offset) < 0.05f) continue;
                ApplyAbilityDamage(target, 18f + rank * 13f);
            }

            List<Vector3> points = new()
            {
                player.position,
                player.position + (Vector3)(direction.normalized * (2.7f + rank * 0.3f))
            };
            SpawnLineEffect("Blade Wave", points, RunSession.GetClassColor(RunClassType.Warrior),
                0.22f, 0.28f);
        }

        private void FireWhirlwind(int rank)
        {
            float radius = 1.65f + rank * 0.35f;
            CollectEnemies(player.position, radius);
            foreach (Health target in targets)
                ApplyAbilityDamage(target, 24f + rank * 14f);

            SpawnRingEffect(player.position, radius, RunSession.GetClassColor(RunClassType.Warrior),
                0.24f, 0.2f);
        }

        private void FireSanctuary(int rank)
        {
            float radius = 1.55f + rank * 0.25f;
            playerHealth.Heal(7f + rank * 4f);
            SpawnRingEffect(player.position, radius, RunSession.GetClassColor(RunClassType.Healer),
                0.42f, 0.12f);
        }

        private void ApplyAbilityDamage(Health target, float damage)
        {
            if (target == null || !target.IsAlive || !target.CompareTag("Enemy")) return;

            float before = target.CurrentHealth;
            target.TakeDamage(damage * RunSession.DamageMultiplier);
            float dealtDamage = Mathf.Max(0f, before - target.CurrentHealth);
            if (dealtDamage > 0f) powerUp?.RegisterEnemyHit(dealtDamage);
        }

        private void CollectEnemies(Vector2 origin, float range)
        {
            targets.Clear();
            HashSet<Health> seen = new();
            foreach (GameObject enemyObject in GameObject.FindGameObjectsWithTag("Enemy"))
            {
                if (enemyObject == null) continue;
                Health enemyHealth = enemyObject.GetComponent<Health>();
                if (enemyHealth == null) enemyHealth = enemyObject.GetComponentInChildren<Health>(true);
                if (enemyHealth == null || !enemyHealth.IsAlive || !seen.Add(enemyHealth)) continue;
                if (Vector2.Distance(origin, enemyHealth.transform.position) <= range)
                    targets.Add(enemyHealth);
            }
        }

        private Health FindClosestEnemy(Vector2 origin, float range)
        {
            CollectEnemies(origin, range);
            Health closest = null;
            float closestDistance = float.MaxValue;
            foreach (Health target in targets)
            {
                float distance = Vector2.Distance(origin, target.transform.position);
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closest = target;
            }

            return closest;
        }

        private Health FindClosestUnusedEnemy(Vector2 origin)
        {
            Health closest = null;
            float closestDistance = float.MaxValue;
            foreach (Health target in targets)
            {
                if (target == null || selectedTargets.Contains(target)) continue;
                float distance = Vector2.Distance(origin, target.transform.position);
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closest = target;
            }

            return closest;
        }

        private void SpawnLineEffect(string objectName, List<Vector3> points, Color color,
            float duration, float width)
        {
            GameObject effect = new(objectName);
            LineRenderer glow = effect.AddComponent<LineRenderer>();
            ConfigureLineRenderer(glow, points, color, width * 3.4f, 46, 0.2f);
            LineRenderer core = effect.AddComponent<LineRenderer>();
            ConfigureLineRenderer(core, points, color, width, 48, 1f);
            LineRenderer highlight = effect.AddComponent<LineRenderer>();
            ConfigureLineRenderer(highlight, points, Color.Lerp(color, Color.white, 0.72f),
                width * 0.24f, 49, 0.9f);
            activeEffects.Add(effect);
            StartCoroutine(FadeLineLayers(effect, new[] { glow, core, highlight }, color, duration));
        }

        private void SpawnRingEffect(Vector2 center, float radius, Color color,
            float duration, float width)
        {
            const int pointCount = 42;
            List<Vector3> points = new(pointCount);
            for (int index = 0; index < pointCount; index++)
            {
                float angle = index / (float)pointCount * Mathf.PI * 2f;
                points.Add(new Vector3(center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius, -0.35f));
            }

            GameObject effect = new("Void Nova Ring");
            LineRenderer line = effect.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = pointCount;
            line.numCornerVertices = 3;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.1f);
            line.sortingOrder = 48;
            line.material = GetEffectMaterial();
            line.SetPositions(points.ToArray());
            activeEffects.Add(effect);
            StartCoroutine(FadeLine(effect, line, color, duration));
        }

        private IEnumerator FadeLine(GameObject effect, LineRenderer line, Color color,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && effect != null)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                Color start = new(color.r, color.g, color.b, alpha);
                Color end = new(color.r, color.g, color.b, alpha * 0.12f);
                line.startColor = start;
                line.endColor = end;
                yield return null;
            }

            if (effect != null)
            {
                activeEffects.Remove(effect);
                Destroy(effect);
            }
        }

        private IEnumerator FadeLineLayers(GameObject effect, LineRenderer[] lines, Color color,
            float duration)
        {
            float elapsed = 0f;
            float[] opacity = { 0.2f, 1f, 0.9f };
            while (elapsed < duration && effect != null)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                for (int index = 0; index < lines.Length; index++)
                {
                    LineRenderer line = lines[index];
                    if (line == null) continue;
                    float layerAlpha = alpha * opacity[Mathf.Min(index, opacity.Length - 1)];
                    line.startColor = new Color(color.r, color.g, color.b, layerAlpha);
                    line.endColor = new Color(color.r, color.g, color.b, layerAlpha * 0.12f);
                }
                yield return null;
            }

            if (effect != null)
            {
                activeEffects.Remove(effect);
                Destroy(effect);
            }
        }

        private void ConfigureLineRenderer(LineRenderer line, List<Vector3> points, Color color,
            float width, int sortingOrder, float opacity)
        {
            if (line == null) return;
            line.useWorldSpace = true;
            line.positionCount = points.Count;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.startWidth = width;
            line.endWidth = width * 0.72f;
            line.startColor = new Color(color.r, color.g, color.b, opacity);
            line.endColor = new Color(color.r, color.g, color.b, opacity * 0.12f);
            line.sortingOrder = sortingOrder;
            line.material = GetEffectMaterial();
            line.textureMode = LineTextureMode.Stretch;
            line.SetPositions(points.ToArray());
        }

        private Material GetEffectMaterial()
        {
            if (effectMaterial != null) return effectMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            effectMaterial = shader != null ? new Material(shader) : null;
            return effectMaterial;
        }

        private void ResetRoomEffectsInternal()
        {
            ClearOrbs();
            ClearEffects();
            StopAllCoroutines();
            autoBulletTimer = 0f;
            radiantBulletTimer = 0f;
            bladeWaveTimer = 0f;
            chainLaserTimer = 0f;
            firestormTimer = 0f;
            whirlwindTimer = 0f;
            healingTimer = 0f;
            sanctuaryTimer = 0f;
        }

        private void ClearOrbs()
        {
            foreach (RunAbilityOrb orb in orbs)
            {
                if (orb != null) Destroy(orb.gameObject);
            }
            orbs.Clear();
        }

        private void ClearEffects()
        {
            foreach (GameObject effect in activeEffects)
            {
                if (effect != null) Destroy(effect);
            }
            activeEffects.Clear();
        }

        private T FindPlayerComponent<T>() where T : Component
        {
            T component = playerHealth != null ? playerHealth.GetComponent<T>() : null;
            if (component == null && playerHealth != null)
                component = playerHealth.GetComponentInParent<T>();
            if (component == null && playerHealth != null)
                component = playerHealth.GetComponentInChildren<T>(true);
            return component;
        }
    }

    public sealed class RunAbilityOrb : MonoBehaviour
    {
        private static readonly Vector2 RoomMinimum = new(-16.1f, -10.1f);
        private static readonly Vector2 RoomMaximum = new(16.1f, 10.1f);
        private readonly Dictionary<Health, float> hitCooldowns = new();
        private static Sprite orbSprite;
        private static Sprite glowSprite;

        private Transform owner;
        private Vector2 velocity;
        private SpriteRenderer orbRenderer;
        private SpriteRenderer glowRenderer;
        private float nextHitTime;
        private int index;

        public static RunAbilityOrb CreateRuntime(Transform ownerTransform, int orbIndex)
        {
            if (ownerTransform == null) return null;
            GameObject orbObject = new($"Bouncing Orb {orbIndex + 1}");
            orbObject.transform.position = ownerTransform.position;
            RunAbilityOrb orb = orbObject.AddComponent<RunAbilityOrb>();
            orb.Configure(ownerTransform, orbIndex);
            return orb;
        }

        private void Configure(Transform ownerTransform, int orbIndex)
        {
            owner = ownerTransform;
            index = orbIndex;
            float angle = (42f + index * 137f) * Mathf.Deg2Rad;
            velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 4.4f;

            GameObject glowObject = new("Orb Glow");
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            glowRenderer = glowObject.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = GetGlowSprite();
            glowRenderer.color = new Color(0.16f, 0.86f, 1f, 0.42f);
            glowRenderer.sortingOrder = 46;
            glowObject.transform.localScale = Vector3.one * 1.35f;

            orbRenderer = gameObject.AddComponent<SpriteRenderer>();
            orbRenderer.sprite = GetOrbSprite();
            orbRenderer.color = Color.white;
            orbRenderer.sortingOrder = 48;
            transform.localScale = Vector3.one * 0.62f;
            nextHitTime = Time.time + 0.25f;
        }

        private void Update()
        {
            int rank = RunSession.GetSkillRank(RunSkillType.ArcaneBeam);
            if (owner == null || rank <= 0)
            {
                Destroy(gameObject);
                return;
            }

            velocity = velocity.normalized * (4.4f + rank * 0.38f);
            Vector2 position = transform.position;
            position += velocity * Time.deltaTime;
            if (position.x <= RoomMinimum.x || position.x >= RoomMaximum.x)
            {
                position.x = Mathf.Clamp(position.x, RoomMinimum.x, RoomMaximum.x);
                velocity.x = -velocity.x;
            }
            if (position.y <= RoomMinimum.y || position.y >= RoomMaximum.y)
            {
                position.y = Mathf.Clamp(position.y, RoomMinimum.y, RoomMaximum.y);
                velocity.y = -velocity.y;
            }

            transform.position = new Vector3(position.x, position.y, -0.3f);
            transform.Rotate(0f, 0f, 260f * Time.deltaTime);
            float pulse = 1f + Mathf.Sin(Time.time * 14f + index) * 0.12f;
            transform.localScale = Vector3.one * (0.62f + rank * 0.035f) * pulse;
            if (glowRenderer != null)
                glowRenderer.transform.localScale = Vector3.one * (1.35f + pulse * 0.12f);

            if (Time.time < nextHitTime) return;
            nextHitTime = Time.time + 0.12f;
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(position, 0.58f))
            {
                if (hit == null) continue;
                Health target = hit.GetComponentInParent<Health>();
                if (target == null || !target.IsAlive || !target.CompareTag("Enemy")) continue;
                if (hitCooldowns.TryGetValue(target, out float cooldown) && Time.time < cooldown) continue;

                float before = target.CurrentHealth;
                target.TakeDamage((19f + rank * 11f) * RunSession.DamageMultiplier);
                if (target.CurrentHealth < before) hitCooldowns[target] = Time.time + 0.38f;
            }
        }

        private static Sprite GetOrbSprite()
        {
            if (orbSprite != null) return orbSprite;
            const int size = 24;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Bouncing Orb Pixel Texture",
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
                    Color pixel = Color.clear;
                    if (distance <= 10.5f)
                    {
                        if (distance >= 8.8f)
                            pixel = new Color(0.08f, 0.27f, 0.52f, 1f);
                        else if (distance <= 2.8f)
                            pixel = Color.white;
                        else
                            pixel = Color.Lerp(new Color(0.1f, 0.82f, 1f, 1f),
                                new Color(0.42f, 0.2f, 1f, 1f), Mathf.Clamp01((distance - 3f) / 6f));
                    }
                    if (x >= 8 && x <= 10 && y >= 14 && y <= 17)
                        pixel = new Color(1f, 1f, 1f, 0.9f);
                    texture.SetPixel(x, y, pixel);
                }
            }
            texture.Apply(false, false);
            orbSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            orbSprite.name = "Bouncing Orb Pixel Sprite";
            return orbSprite;
        }

        private static Sprite GetGlowSprite()
        {
            if (glowSprite != null) return glowSprite;
            const int size = 24;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Bouncing Orb Glow Texture",
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
                    float alpha = distance <= 11f
                        ? Mathf.Clamp01((11f - distance) / 6f) * 0.76f
                        : 0f;
                    texture.SetPixel(x, y, new Color(0.05f, 0.72f, 1f, alpha));
                }
            }
            texture.Apply(false, false);
            glowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            glowSprite.name = "Bouncing Orb Glow Sprite";
            return glowSprite;
        }
    }
}
