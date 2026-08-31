using System;
using System.Collections;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Movement;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Scripts.World
{
    public enum WorldEnemyPattern
    {
        Chaser,
        Charger,
        Shooter
    }

    public sealed class WorldSecondaryEnemy : MonoBehaviour
    {
        private enum EnemyState
        {
            Pursue,
            Telegraph,
            Dash,
            Recover,
            Defeated
        }

        private Transform player;
        private Health health;
        private Rigidbody2D body;
        private Collider2D enemyCollider;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer indicatorRenderer;
        private WorldEnemyPattern pattern;
        private EnemyState state;
        private Action onDefeated;
        private Action<GameObject> registerProjectile;
        private float moveSpeed;
        private float contactDamage;
        private float manaReward;
        private float stateTime;
        private float nextAttackTime;
        private float nextContactDamageTime;
        private Vector2 dashDirection;
        private bool deathReported;
        private bool dying;

        public static WorldSecondaryEnemy CreateRuntime(string objectName, Vector2 position,
            WorldEnemyPattern enemyPattern, float maxHealth, float speed, float damage,
            Transform playerTarget, Transform parent, Action<GameObject> onProjectileCreated,
            Action defeatedCallback)
        {
            if (parent == null || playerTarget == null) return null;

            GameObject enemyObject = new(objectName);
            enemyObject.tag = "Enemy";
            enemyObject.transform.SetParent(parent, false);
            enemyObject.transform.position = new Vector3(position.x, position.y, -0.05f);
            enemyObject.transform.localScale = enemyPattern == WorldEnemyPattern.Charger
                ? Vector3.one * 1.08f
                : Vector3.one * 0.92f;

            SpriteRenderer renderer = enemyObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeWhiteSprite.Instance;
            renderer.sortingOrder = 9;

            GameObject indicatorObject = new("Attack Pattern Indicator");
            indicatorObject.transform.SetParent(enemyObject.transform, false);
            indicatorObject.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            indicatorObject.transform.localScale = new Vector3(0.24f, 0.24f, 1f);
            SpriteRenderer indicator = indicatorObject.AddComponent<SpriteRenderer>();
            indicator.sprite = RuntimeWhiteSprite.Instance;
            indicator.sortingOrder = 10;

            CircleCollider2D collider = enemyObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.45f;

            Rigidbody2D rigidbody = enemyObject.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.freezeRotation = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            Health enemyHealth = enemyObject.AddComponent<Health>();
            enemyHealth.ConfigureRuntime(maxHealth);

            WorldSecondaryEnemy enemy = enemyObject.AddComponent<WorldSecondaryEnemy>();
            enemy.Configure(renderer, indicator, collider, rigidbody, enemyHealth, enemyPattern,
                speed, damage, playerTarget, onProjectileCreated, defeatedCallback);
            return enemy;
        }

        private void Configure(SpriteRenderer renderer, SpriteRenderer indicator, Collider2D collider,
            Rigidbody2D rigidbody, Health enemyHealth, WorldEnemyPattern enemyPattern, float speed,
            float damage, Transform playerTarget, Action<GameObject> onProjectileCreated,
            Action defeatedCallback)
        {
            bodyRenderer = renderer;
            indicatorRenderer = indicator;
            enemyCollider = collider;
            body = rigidbody;
            health = enemyHealth;
            pattern = enemyPattern;
            player = playerTarget;
            registerProjectile = onProjectileCreated;
            onDefeated = defeatedCallback;
            moveSpeed = Mathf.Max(0.1f, speed);
            contactDamage = Mathf.Max(0f, damage);
            manaReward = pattern == WorldEnemyPattern.Shooter ? 3f : 2f;
            state = EnemyState.Pursue;
            nextAttackTime = Time.time + (pattern == WorldEnemyPattern.Chaser ? 999f : 0.9f);
            ApplyPatternColors();

            if (health != null) health.OnDied += HandleDied;
            FloatingHealthBar healthBar = GetComponent<FloatingHealthBar>();
            if (healthBar == null) healthBar = gameObject.AddComponent<FloatingHealthBar>();
            healthBar.ConfigureRuntime(health, new Vector2(1.15f, 0.12f), new Vector2(0f, 0.82f),
                new Color(1f, 0.28f, 0.1f, 1f));
        }

        private void Update()
        {
            if (dying || health == null || !health.IsAlive || player == null) return;
            if (UIManager.instance != null && UIManager.instance.IsPaused)
            {
                if (body != null) body.linearVelocity = Vector2.zero;
                return;
            }

            switch (pattern)
            {
                case WorldEnemyPattern.Charger:
                    UpdateCharger();
                    break;
                case WorldEnemyPattern.Shooter:
                    UpdateShooter();
                    break;
                default:
                    MoveTowardsPlayer(moveSpeed);
                    break;
            }

            ClampToRoom();
            UpdatePresentation();
        }

        private void UpdateCharger()
        {
            switch (state)
            {
                case EnemyState.Telegraph:
                    StopMoving();
                    stateTime -= Time.deltaTime;
                    if (stateTime <= 0f)
                    {
                        dashDirection = ((Vector2)player.position - body.position).normalized;
                        if (dashDirection.sqrMagnitude < 0.01f) dashDirection = Vector2.right;
                        state = EnemyState.Dash;
                        stateTime = 0.38f;
                    }
                    break;
                case EnemyState.Dash:
                    body.linearVelocity = dashDirection * moveSpeed * 3.2f;
                    stateTime -= Time.deltaTime;
                    if (stateTime <= 0f)
                    {
                        state = EnemyState.Recover;
                        stateTime = 0.72f;
                        StopMoving();
                    }
                    break;
                case EnemyState.Recover:
                    StopMoving();
                    stateTime -= Time.deltaTime;
                    if (stateTime <= 0f)
                    {
                        state = EnemyState.Pursue;
                        nextAttackTime = Time.time + 0.65f;
                    }
                    break;
                default:
                    MoveTowardsPlayer(moveSpeed * 0.72f);
                    if (Time.time >= nextAttackTime)
                    {
                        state = EnemyState.Telegraph;
                        stateTime = 0.58f;
                    }
                    break;
            }
        }

        private void UpdateShooter()
        {
            switch (state)
            {
                case EnemyState.Telegraph:
                    StopMoving();
                    stateTime -= Time.deltaTime;
                    if (stateTime <= 0f)
                    {
                        FireProjectile();
                        state = EnemyState.Recover;
                        stateTime = 0.36f;
                        nextAttackTime = Time.time + 1.65f;
                    }
                    break;
                case EnemyState.Recover:
                    StopMoving();
                    stateTime -= Time.deltaTime;
                    if (stateTime <= 0f) state = EnemyState.Pursue;
                    break;
                default:
                    MoveShooter();
                    if (Time.time >= nextAttackTime)
                    {
                        state = EnemyState.Telegraph;
                        stateTime = 0.42f;
                    }
                    break;
            }
        }

        private void MoveShooter()
        {
            Vector2 toPlayer = (Vector2)player.position - body.position;
            float distance = toPlayer.magnitude;
            Vector2 direction = distance > 0.01f ? toPlayer / distance : Vector2.right;
            Vector2 movement;
            if (distance < 5.5f)
                movement = -direction;
            else if (distance > 8f)
                movement = direction;
            else
            {
                Vector2 strafe = new(-direction.y, direction.x);
                float side = GetInstanceID() % 2 == 0 ? 1f : -1f;
                movement = strafe * side * 0.72f;
            }

            body.linearVelocity = movement.normalized * moveSpeed;
        }

        private void MoveTowardsPlayer(float speed)
        {
            Vector2 direction = ((Vector2)player.position - body.position).normalized;
            body.linearVelocity = direction * speed;
        }

        private void FireProjectile()
        {
            Vector2 direction = ((Vector2)player.position - body.position).normalized;
            WorldEnemyProjectile projectile = WorldEnemyProjectile.CreateRuntime(
                "Secondary Enemy Projectile", body.position, direction, 7.4f, contactDamage * 0.82f,
                transform.parent);
            if (projectile != null) registerProjectile?.Invoke(projectile.gameObject);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (dying || !health.IsAlive || Time.time < nextContactDamageTime || !IsPlayer(other)) return;

            PlayerDodge dodge = other.GetComponentInParent<PlayerDodge>();
            if (dodge != null && dodge.IsInvulnerable) return;

            Health playerHealth = other.GetComponentInParent<Health>(true);
            if (playerHealth == null || !playerHealth.IsAlive) return;
            playerHealth.TakeDamage(contactDamage);
            nextContactDamageTime = Time.time + 0.78f;
        }

        private void HandleDied()
        {
            if (deathReported) return;
            deathReported = true;
            dying = true;
            state = EnemyState.Defeated;
            if (enemyCollider != null) enemyCollider.enabled = false;
            if (body != null) body.linearVelocity = Vector2.zero;
            if (health != null) health.SetExternalInvulnerable(true);
            RewardPlayer();
            onDefeated?.Invoke();
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            Vector3 originalScale = transform.localScale;
            const float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration && this != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                transform.localScale = originalScale * Mathf.Lerp(1f, 1.3f, eased);
                if (bodyRenderer != null)
                {
                    Color color = bodyRenderer.color;
                    color.a = 1f - eased;
                    bodyRenderer.color = color;
                }
                if (indicatorRenderer != null)
                {
                    Color color = indicatorRenderer.color;
                    color.a = 1f - eased;
                    indicatorRenderer.color = color;
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        private void RewardPlayer()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null) return;
            PowerUp powerUp = playerObject.GetComponentInChildren<PowerUp>();
            if (powerUp != null) powerUp.TryAddMana(manaReward);
        }

        private void ApplyPatternColors()
        {
            Color color = pattern switch
            {
                WorldEnemyPattern.Charger => new Color(1f, 0.42f, 0.08f, 1f),
                WorldEnemyPattern.Shooter => new Color(0.42f, 0.72f, 1f, 1f),
                _ => new Color(0.34f, 0.92f, 0.68f, 1f)
            };
            if (bodyRenderer != null) bodyRenderer.color = color;
            if (indicatorRenderer != null) indicatorRenderer.color = color;
        }

        private void UpdatePresentation()
        {
            if (indicatorRenderer == null) return;
            Color color = indicatorRenderer.color;
            float alpha = state == EnemyState.Telegraph ? 1f : state == EnemyState.Dash ? 0.9f : 0.42f;
            float pulse = state == EnemyState.Telegraph
                ? 0.22f + Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.22f
                : state == EnemyState.Dash ? 0.3f : 0.2f;
            indicatorRenderer.color = new Color(color.r, color.g, color.b, alpha);
            indicatorRenderer.transform.localScale = new Vector3(pulse, pulse, 1f);
        }

        private void StopMoving()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void ClampToRoom()
        {
            if (body == null) return;
            Vector2 position = body.position;
            position.x = Mathf.Clamp(position.x, -16.4f, 16.4f);
            position.y = Mathf.Clamp(position.y, -10.4f, 10.4f);
            body.position = position;
        }

        private void OnDestroy()
        {
            if (health != null) health.OnDied -= HandleDied;
        }

        private static bool IsPlayer(Collider2D other)
        {
            if (other == null) return false;
            if (other.CompareTag("Player")) return true;
            Transform root = other.transform.root;
            return root != null && root.CompareTag("Player");
        }
    }

    public sealed class WorldEnemyProjectile : MonoBehaviour
    {
        private Vector2 direction;
        private float speed;
        private float damage;
        private float lifetime;
        private Rigidbody2D body;
        private bool spent;

        public static WorldEnemyProjectile CreateRuntime(string objectName, Vector2 position,
            Vector2 travelDirection, float projectileSpeed, float damageAmount, Transform parent)
        {
            if (parent == null || travelDirection.sqrMagnitude < 0.01f) return null;

            GameObject projectileObject = new(objectName);
            projectileObject.transform.SetParent(parent, false);
            projectileObject.transform.position = new Vector3(position.x, position.y, -0.1f);
            projectileObject.transform.localScale = Vector3.one * 0.32f;

            SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeWhiteSprite.Instance;
            renderer.color = new Color(0.38f, 0.78f, 1f, 1f);
            renderer.sortingOrder = 11;

            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.5f;

            Rigidbody2D rigidbody = projectileObject.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            WorldEnemyProjectile projectile = projectileObject.AddComponent<WorldEnemyProjectile>();
            projectile.Configure(rigidbody, travelDirection, projectileSpeed, damageAmount);
            return projectile;
        }

        private void Configure(Rigidbody2D rigidbody, Vector2 travelDirection, float projectileSpeed,
            float damageAmount)
        {
            body = rigidbody;
            direction = travelDirection.normalized;
            speed = Mathf.Max(0.1f, projectileSpeed);
            damage = Mathf.Max(0f, damageAmount);
            lifetime = 3.2f;
            body.linearVelocity = direction * speed;
        }

        private void Update()
        {
            lifetime -= Time.deltaTime;
            if (lifetime <= 0f || Mathf.Abs(transform.position.x) > 17f ||
                Mathf.Abs(transform.position.y) > 11f)
                Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (spent || other == null || !IsPlayer(other)) return;

            PlayerDodge dodge = other.GetComponentInParent<PlayerDodge>();
            if (dodge != null && dodge.IsInvulnerable) return;

            Health playerHealth = other.GetComponentInParent<Health>(true);
            if (playerHealth == null || !playerHealth.IsAlive) return;
            spent = true;
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
        }

        private static bool IsPlayer(Collider2D other)
        {
            if (other.CompareTag("Player")) return true;
            Transform root = other.transform.root;
            return root != null && root.CompareTag("Player");
        }
    }
}
