using System;
using System.Collections;
using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Combat;
using Project.Characters.Player.PlayerScripts.Controller;
using Project.Characters.Player.PlayerScripts.Core;
using Project.Scripts.Controller;
using Project.Scripts.Progression;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Combat
{
    public class AttackPlayer : MonoBehaviour
    {
        [Header("Attack Data")]
        [SerializeField] private AttackData attack;
        [SerializeField] private AttackData powerUpAttack;

        [Header("Attack References")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private ObjectPool objectPool;
        [SerializeField] private PowerUp powerUpHoming;

        [Header("Reload Settings")]
        [SerializeField] private float chargerCapacity = 6f;
        [SerializeField] private float chargerTime = 2f;

        [Header("Fire Rate")]
        [SerializeField] private float fireRate = 0.2f;

        [Header("Auto Shoot")]
        [SerializeField] private float autoShootRate = 0.2f;

        [Header("Overdrive")]
        [SerializeField, Range(3, 9)] private int overdriveProjectileCount = 5;
        [SerializeField, Range(0f, 90f)] private float overdriveSpread = 32f;
        [SerializeField, Min(0.05f)] private float shockwaveDuration = 0.18f;
        [SerializeField, Min(0.1f)] private float shockwaveRadius = 1.4f;

        [Header("Audio Settings")]
        [SerializeField, Range(0f, 0.5f)] private float volumeShoot;
        [SerializeField, Range(0f, 0.5f)] private float volumeReload;

        private readonly List<GameObject> reservedProjectiles = new();
        private AttackData currentAttack;
        private PlayerSoundController playerSoundController;
        private Transform enemyTarget;
        private Material overdriveMaterial;
        private int counterShoots;
        private bool isReloading;
        private float fireTimer;
        private float autoShootTimer;
        private float weaponDamageMultiplier = 1f;
        private float baseChargerCapacity;
        private float baseChargerTime;
        private float baseFireRate;
        private float baseAutoShootRate;
        private bool baseStatsCaptured;

        public event Action<float, float> OnReloadChange;

        public float ChargerTime => chargerTime;
        public int ShotsUsed => counterShoots;

        private int MagazineCapacity => Mathf.Max(1, Mathf.RoundToInt(chargerCapacity));

        public void ConfigureRuntimePool(ObjectPool runtimePool)
        {
            if (runtimePool != null) objectPool = runtimePool;
        }

        private void Awake()
        {
            baseChargerCapacity = chargerCapacity;
            baseChargerTime = chargerTime;
            baseFireRate = fireRate;
            baseAutoShootRate = autoShootRate;
            baseStatsCaptured = true;
            playerSoundController = GetComponent<PlayerSoundController>();
        }

        private void Start()
        {
            currentAttack = attack;
            ApplySelectedLoadout();
            ResolveEnemy();

            if (powerUpHoming != null)
            {
                powerUpHoming.OnPowerUpStateChanged += OnPowerUpStateChanged;
            }

            OnReloadChange?.Invoke(1f, 1f);
        }

        private void OnDestroy()
        {
            if (powerUpHoming != null)
            {
                powerUpHoming.OnPowerUpStateChanged -= OnPowerUpStateChanged;
            }

            if (overdriveMaterial != null) Destroy(overdriveMaterial);
        }

        private void Update()
        {
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;

            if (fireTimer > 0f) fireTimer -= Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.R) && !isReloading && counterShoots > 0)
                StartCoroutine(Reload());

            HandleInputShoot();
        }

        private void OnPowerUpStateChanged(bool isActive)
        {
            currentAttack = isActive && powerUpAttack != null ? powerUpAttack : attack;
            autoShootTimer = 0f;
        }

        private void HandleInputShoot()
        {
            if (isReloading || fireTimer > 0f || !Input.GetMouseButton(0)) return;

            if (counterShoots >= MagazineCapacity)
            {
                StartCoroutine(Reload());
                return;
            }

            Shoot();
            fireTimer = Mathf.Max(0.01f, fireRate);
        }

        private void AutoShoot()
        {
            if (isReloading) return;

            autoShootTimer += Time.deltaTime;
            if (autoShootTimer < Mathf.Max(0.01f, autoShootRate)) return;

            autoShootTimer = 0f;
            if (counterShoots >= MagazineCapacity)
            {
                StartCoroutine(Reload());
                return;
            }

            Shoot();
        }

        private IEnumerator Reload()
        {
            isReloading = true;
            if (playerSoundController != null) playerSoundController.PlayReload(volumeReload);

            float duration = Mathf.Max(0.01f, chargerTime);
            float elapsed = 0f;
            OnReloadChange?.Invoke(0f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                OnReloadChange?.Invoke(Mathf.Clamp01(elapsed / duration), 1f);
                yield return null;
            }

            counterShoots = 0;
            isReloading = false;
            OnReloadChange?.Invoke(1f, 1f);
        }

        private void Shoot()
        {
            if (!CanShoot()) return;
            if (enemyTarget == null) ResolveEnemy();

            bool overdriveActive = powerUpHoming != null && powerUpHoming.IsActive &&
                                   currentAttack == powerUpAttack;
            if (overdriveActive)
            {
                if (ShootAbilityVolley()) powerUpHoming.ConsumeCharge();
            }
            else
            {
                ShootSingleProjectile();
            }
        }

        private bool CanShoot()
        {
            if (currentAttack != null && currentAttack.BulletPrefab != null &&
                objectPool != null && firePoint != null) return true;

            Debug.LogError("AttackPlayer is missing AttackData, bullet prefab, pool, or fire point.", this);
            return false;
        }

        private void ShootSingleProjectile()
        {
            int projectileCount = Mathf.Clamp(RunSession.BasicProjectileCount, 1, 3);
            if (!objectPool.GetObjects(currentAttack.BulletPrefab, projectileCount, reservedProjectiles))
            {
                return;
            }

            for (int index = 0; index < reservedProjectiles.Count; index++)
            {
                GameObject projectile = reservedProjectiles[index];
                if (projectile == null ||
                    projectile.GetComponentInChildren<Rigidbody2D>(true) == null ||
                    projectile.GetComponentInChildren<Bullet>(true) == null)
                {
                    ReturnReservedVolley();
                    return;
                }
            }

            for (int index = 0; index < reservedProjectiles.Count; index++)
            {
                float normalized = reservedProjectiles.Count == 1
                    ? 0.5f
                    : index / (reservedProjectiles.Count - 1f);
                float angleOffset = Mathf.Lerp(-RunSession.BasicProjectileSpread * 0.5f,
                    RunSession.BasicProjectileSpread * 0.5f, normalized);
                Quaternion rotation = GetMouseAimRotation(angleOffset);
                if (!ConfigureProjectile(reservedProjectiles[index], rotation,
                        RunSession.BasicProjectileHoming, RunSession.BasicProjectileDamageMultiplier,
                        RunSession.BasicProjectileVisualScale, RunSession.BasicProjectileColor))
                {
                    ReturnReservedVolley();
                    return;
                }
            }

            RegisterVolley();
            reservedProjectiles.Clear();
        }

        private bool ShootAbilityVolley()
        {
            int projectileCount = Mathf.Clamp(RunSession.PowerProjectileCount, 1, 5);
            if (!objectPool.GetObjects(currentAttack.BulletPrefab, projectileCount, reservedProjectiles))
            {
                Debug.LogError($"Power ability could not reserve its complete {projectileCount}-projectile volley.", this);
                return false;
            }

            for (int index = 0; index < reservedProjectiles.Count; index++)
            {
                GameObject projectile = reservedProjectiles[index];
                if (projectile == null ||
                    projectile.GetComponentInChildren<Rigidbody2D>(true) == null ||
                    projectile.GetComponentInChildren<Bullet>(true) == null)
                {
                    ReturnReservedVolley();
                    Debug.LogError("Every power projectile requires Bullet and Rigidbody2D.", this);
                    return false;
                }
            }

            for (int index = 0; index < reservedProjectiles.Count; index++)
            {
                float normalized = reservedProjectiles.Count == 1
                    ? 0.5f
                    : index / (reservedProjectiles.Count - 1f);
                float angleOffset = Mathf.Lerp(-RunSession.PowerProjectileSpread * 0.5f,
                    RunSession.PowerProjectileSpread * 0.5f, normalized);
                Quaternion rotation = GetMouseAimRotation(angleOffset);
                if (!ConfigureProjectile(reservedProjectiles[index], rotation, RunSession.PowerProjectileHoming,
                        RunSession.PowerProjectileDamageMultiplier, RunSession.PowerProjectileVisualScale,
                        RunSession.PowerProjectileColor))
                {
                    ReturnReservedVolley();
                    return false;
                }
            }

            RegisterVolley();
            SpawnOverdriveShockwave();
            reservedProjectiles.Clear();
            return true;
        }

        public bool TryShootAutomatic(Vector2 direction, float damageMultiplier,
            float visualScale = 1.2f, Color? visualColor = null)
        {
            if (!CanShoot() || isReloading || direction.sqrMagnitude < 0.01f) return false;

            Quaternion rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            GameObject bulletObject = objectPool.GetObject(
                currentAttack.BulletPrefab, firePoint.position, rotation);
            if (bulletObject == null) return false;

            if (!ConfigureProjectile(bulletObject, rotation, false, damageMultiplier,
                    visualScale, visualColor ?? RunSession.BasicProjectileColor))
            {
                objectPool.ReturnObject(bulletObject, currentAttack.BulletPrefab);
                return false;
            }

            if (playerSoundController != null) playerSoundController.PlayFire(volumeShoot * 0.55f);
            return true;
        }

        private bool ConfigureProjectile(GameObject bulletObject, Quaternion rotation, bool homing,
            float abilityDamageMultiplier, float visualScale, Color visualColor)
        {
            Rigidbody2D body = bulletObject.GetComponentInChildren<Rigidbody2D>(true);
            Bullet bullet = bulletObject.GetComponentInChildren<Bullet>(true);
            if (body == null || bullet == null)
            {
                Debug.LogError("Player projectile requires Bullet and Rigidbody2D.", bulletObject);
                return false;
            }

            bulletObject.transform.SetPositionAndRotation(firePoint.position, rotation);
            Vector2 direction = rotation * Vector3.right;
            float projectileSpeed = currentAttack.Speed * RunSession.ProjectileSpeedMultiplier;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.linearVelocity = direction.normalized * projectileSpeed;
            bullet.SetPool(
                objectPool,
                currentAttack.BulletPrefab,
                bulletObject,
                currentAttack.LifeTime,
                BulletOwner.Player,
                currentAttack.Damage * weaponDamageMultiplier * Mathf.Max(0.1f, abilityDamageMultiplier));
            bullet.SetTarget(enemyTarget, homing, projectileSpeed);
            bullet.SetHitCallback(powerUpHoming != null ? powerUpHoming.RegisterEnemyHit : null);
            bullet.SetEmpoweredVisual(visualScale > 1f, visualScale, visualColor);
            bullet.SetWeaponVisual(RunSession.BasicProjectileColor);
            return true;
        }

        private Quaternion GetMouseAimRotation(float angleOffset)
        {
            Vector2 direction = GetMouseAimDirection();
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + angleOffset;
            return Quaternion.Euler(0f, 0f, angle);
        }

        private Vector2 GetMouseAimDirection()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null || firePoint == null)
                return firePoint != null ? (Vector2)firePoint.right : Vector2.right;

            Vector3 mousePosition = Input.mousePosition;
            mousePosition.z = firePoint.position.z - mainCamera.transform.position.z;
            Vector3 target = mainCamera.ScreenToWorldPoint(mousePosition);
            target.z = firePoint.position.z;

            Vector2 direction = target - firePoint.position;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : (Vector2)firePoint.right;
        }

        private void ReturnReservedVolley()
        {
            foreach (GameObject projectile in reservedProjectiles)
            {
                if (projectile != null) objectPool.ReturnObject(projectile, currentAttack.BulletPrefab);
            }

            reservedProjectiles.Clear();
        }

        private void RegisterVolley()
        {
            counterShoots++;
            if (playerSoundController != null) playerSoundController.PlayFire(volumeShoot);
        }

        private void SpawnOverdriveShockwave()
        {
            GameObject wave = new("Overdrive Muzzle Shockwave");
            wave.transform.position = firePoint.position;
            Destroy(wave, Mathf.Max(0.05f, shockwaveDuration) + 0.1f);

            LineRenderer line = wave.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 32;
            line.numCornerVertices = 2;
            line.sortingOrder = 45;
            line.material = GetOverdriveMaterial();
            StartCoroutine(AnimateShockwave(wave, line));
        }

        private IEnumerator AnimateShockwave(GameObject wave, LineRenderer line)
        {
            float duration = Mathf.Max(0.05f, shockwaveDuration);
            float elapsed = 0f;
            Color startColor = new(0.2f, 0.95f, 1f, 0.95f);
            Color endColor = new(1f, 0.08f, 0.48f, 0f);

            while (elapsed < duration && wave != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float radius = Mathf.Lerp(0.12f, shockwaveRadius, progress);
                float width = Mathf.Lerp(0.18f, 0.015f, progress);
                Color color = Color.Lerp(startColor, endColor, progress);

                line.startWidth = width;
                line.endWidth = width;
                line.startColor = color;
                line.endColor = color;

                for (int index = 0; index < line.positionCount; index++)
                {
                    float angle = index / (float)line.positionCount * Mathf.PI * 2f;
                    line.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }

                yield return null;
            }

            if (wave != null) Destroy(wave);
        }

        private Material GetOverdriveMaterial()
        {
            if (overdriveMaterial != null) return overdriveMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            overdriveMaterial = shader != null ? new Material(shader) : null;
            return overdriveMaterial;
        }

        private void ApplySelectedLoadout()
        {
            if (RunSession.SelectedClass == RunClassType.None)
                RunSession.SelectClass(GameLoadout.StartingClass);

            RefreshProgressionStats();
            PlayerWeaponVisual.ApplyAll(transform.root);

            if (firePoint == null) return;
            SpriteRenderer weaponRenderer = firePoint.GetComponentInParent<SpriteRenderer>();
            if (weaponRenderer != null) weaponRenderer.color = GameLoadout.WeaponColor;
        }

        public void RefreshProgressionStats()
        {
            if (!baseStatsCaptured)
            {
                baseChargerCapacity = chargerCapacity;
                baseChargerTime = chargerTime;
                baseFireRate = fireRate;
                baseAutoShootRate = autoShootRate;
                baseStatsCaptured = true;
            }

            weaponDamageMultiplier = RunSession.DamageMultiplier * GameLoadout.WeaponDamageMultiplier;
            chargerCapacity = Mathf.Max(1f,
                Mathf.Round(baseChargerCapacity *
                    GameLoadout.MagazineMultiplier *
                    (1f + RunSession.GetSkillRank(RunSkillType.QuickDraw) * 0.16f)));
            chargerTime = Mathf.Max(0.25f,
                baseChargerTime * GameLoadout.ReloadMultiplier /
                (1f + RunSession.GetSkillRank(RunSkillType.QuickDraw) * 0.08f));
            fireRate = Mathf.Max(0.04f,
                baseFireRate * GameLoadout.FireRateMultiplier * RunSession.AttackCooldownMultiplier);
            autoShootRate = Mathf.Max(0.04f,
                baseAutoShootRate * GameLoadout.FireRateMultiplier * RunSession.AttackCooldownMultiplier);
        }

        private void ResolveEnemy()
        {
            GameObject enemy = GameObject.FindGameObjectWithTag("Enemy");
            enemyTarget = enemy != null ? enemy.transform : null;
        }

        public void RestoreCombatState(int shotsUsed)
        {
            StopAllCoroutines();
            isReloading = false;
            fireTimer = 0f;
            autoShootTimer = 0f;
            counterShoots = Mathf.Clamp(shotsUsed, 0, MagazineCapacity);
            OnReloadChange?.Invoke(1f, 1f);
        }

        private void OnValidate()
        {
            overdriveProjectileCount = Mathf.Clamp(overdriveProjectileCount, 3, 9);
            overdriveSpread = Mathf.Clamp(overdriveSpread, 0f, 90f);
            shockwaveDuration = Mathf.Max(0.05f, shockwaveDuration);
            shockwaveRadius = Mathf.Max(0.1f, shockwaveRadius);
        }
    }
}
