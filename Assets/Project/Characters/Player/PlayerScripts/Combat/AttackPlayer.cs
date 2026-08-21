using System;
using System.Collections;
using Project.Characters.Enemy.EnemyScripts.Combat;
using Project.Characters.Player.PlayerScripts.Controller;
using Project.Characters.Player.PlayerScripts.Core;
using Project.Scripts.Controller;
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

        [Header("Audio Settings")]
        [SerializeField, Range(0f, 0.5f)] private float volumeShoot;
        [SerializeField, Range(0f, 0.5f)] private float volumeReload;

        private AttackData currentAttack;
        private PlayerSoundController playerSoundController;
        private Transform enemyTarget;
        private int counterShoots;
        private bool isReloading;
        private float fireTimer;
        private float autoShootTimer;

        public event Action<float, float> OnReloadChange;

        public float ChargerTime => chargerTime;
        public int ShotsUsed => counterShoots;

        private int MagazineCapacity => Mathf.Max(1, Mathf.RoundToInt(chargerCapacity));

        private void Awake()
        {
            playerSoundController = GetComponent<PlayerSoundController>();
        }

        private void Start()
        {
            currentAttack = attack;
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
        }

        private void Update()
        {
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;

            if (fireTimer > 0f) fireTimer -= Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.R) && !isReloading && counterShoots > 0)
                StartCoroutine(Reload());

            if (powerUpHoming != null && powerUpHoming.IsActive)
            {
                AutoShoot();
            }
            else
            {
                HandleInputShoot();
            }
        }

        private void OnPowerUpStateChanged(bool isActive)
        {
            currentAttack = isActive && powerUpAttack != null ? powerUpAttack : attack;
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
            if (currentAttack == null || currentAttack.BulletPrefab == null || objectPool == null || firePoint == null)
            {
                Debug.LogError("AttackPlayer is missing AttackData, bullet prefab, pool, or fire point.", this);
                return;
            }

            if (enemyTarget == null) ResolveEnemy();

            GameObject bulletObject = objectPool.GetObject(
                currentAttack.BulletPrefab,
                firePoint.position,
                firePoint.rotation
            );

            if (bulletObject == null) return;

            Rigidbody2D body = bulletObject.GetComponentInChildren<Rigidbody2D>(true);
            Bullet bullet = bulletObject.GetComponentInChildren<Bullet>(true);
            if (body == null || bullet == null)
            {
                objectPool.ReturnObject(bulletObject, currentAttack.BulletPrefab);
                Debug.LogError("Player projectile requires Bullet and Rigidbody2D.", bulletObject);
                return;
            }

            counterShoots++;
            if (playerSoundController != null) playerSoundController.PlayFire(volumeShoot);

            body.linearVelocity = firePoint.right * currentAttack.Speed;
            bullet.SetPool(
                objectPool,
                currentAttack.BulletPrefab,
                bulletObject,
                currentAttack.LifeTime,
                BulletOwner.Player,
                currentAttack.Damage
            );

            bool homing = powerUpHoming != null && powerUpHoming.IsActive;
            bullet.SetTarget(enemyTarget, homing);
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
    }
}
