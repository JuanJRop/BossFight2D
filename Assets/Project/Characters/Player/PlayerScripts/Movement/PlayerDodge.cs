using System;
using System.Collections;
using System.Collections.Generic;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Controller;
using Project.Scripts.Controller;
using Project.Scripts.Progression;
using Unity.Cinemachine;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Movement
{
    public class PlayerDodge : MonoBehaviour
    {
        [Header("Dodge Movement")]
        [SerializeField] private float dashForce;
        [SerializeField] private float dashTime;

        [Header("Dash Charges")]
        [SerializeField, Range(1, 8)] private int maxDashCharges = 3;
        [SerializeField, Min(0.1f)] private float dashRechargeTime = 2.4f;

        [Header("Impact Dash")]
        [SerializeField, Min(0f)] private float dashDamage = 34f;
        [SerializeField, Min(0.1f)] private float dashHitRadius = 0.9f;

        [Header("References")]
        [SerializeField] private TrailRenderer trail;

        [Header("Camera Shake")]
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField, Range(0f, 0.5f)] private float volume;

        private PlayerMove playerMove;
        private Rigidbody2D rb;
        private CinemachineBasicMultiChannelPerlin noise;
        private PlayerSoundController playerSoundController;
        private PowerUp powerUp;
        private readonly HashSet<Health> dashHitTargets = new();
        private float rechargeTimer;
        private int dashCharges;
        private bool isDashing;
        private bool isInvulnerable;
        private float baseDashForce;
        private float baseDashTime;
        private float baseDashRechargeTime;
        private float baseDashDamage;
        private int baseMaxDashCharges;
        private bool statsInitialized;

        public event Action<float, float> OnStaminaChanged;
        public event Action<int, int, float> OnDashChargesChanged;

        public bool IsInvulnerable => isInvulnerable;
        public bool IsDashing => isDashing;
        public float MaxStamina => maxDashCharges;
        public float CurrentStamina => dashCharges;
        public int MaxDashCharges => maxDashCharges;
        public int DashCharges => dashCharges;
        public float RechargeProgress => dashCharges >= maxDashCharges
            ? 1f
            : Mathf.Clamp01(rechargeTimer / Mathf.Max(0.1f, dashRechargeTime));

        private void Start()
        {
            playerSoundController = GetComponent<PlayerSoundController>();
            playerMove = GetComponent<PlayerMove>();
            rb = GetComponent<Rigidbody2D>();
            powerUp = GetComponentInChildren<PowerUp>();
            if (virtualCamera == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null) virtualCamera = mainCamera.GetComponent<CinemachineCamera>();
            }

            noise = virtualCamera != null
                ? virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>()
                : null;

            if (trail != null) trail.enabled = false;
            baseDashForce = dashForce;
            baseDashTime = dashTime;
            baseDashRechargeTime = dashRechargeTime;
            baseDashDamage = dashDamage;
            baseMaxDashCharges = maxDashCharges;
            RefreshProgressionStats();
            dashCharges = maxDashCharges;
            statsInitialized = true;
            NotifyStaminaChanged();
        }

        public void RefreshProgressionStats()
        {
            if (baseMaxDashCharges <= 0)
            {
                baseDashForce = dashForce;
                baseDashTime = dashTime;
                baseDashRechargeTime = dashRechargeTime;
                baseDashDamage = dashDamage;
                baseMaxDashCharges = maxDashCharges;
            }

            int previousMax = maxDashCharges;
            maxDashCharges = Mathf.Clamp(baseMaxDashCharges + GameLoadout.DashChargeBonus +
                RunSession.StaminaDashChargeBonus, 1, 8);
            dashForce = Mathf.Max(0f, baseDashForce * RunSession.DashForceMultiplier);
            dashTime = Mathf.Max(0.01f, baseDashTime);
            dashRechargeTime = Mathf.Max(0.1f,
                baseDashRechargeTime * GameLoadout.DashRechargeMultiplier *
                RunSession.DashRechargeMultiplier);
            dashDamage = Mathf.Max(0f,
                baseDashDamage * GameLoadout.DashDamageMultiplier * RunSession.DamageMultiplier);

            if (statsInitialized)
            {
                dashCharges = Mathf.Clamp(dashCharges + maxDashCharges - previousMax, 0, maxDashCharges);
                NotifyStaminaChanged();
            }
        }

        private void Update()
        {
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;

            if (Input.GetKeyDown(KeyCode.Space)) TryDodge();
            RechargeDash();
        }

        private void TryDodge()
        {
            if (isDashing || playerMove == null || playerMove.IsStunned ||
                dashCharges <= 0) return;

            Vector2 direction = playerMove.MoveInput.sqrMagnitude >= 0.01f
                ? playerMove.MoveInput.normalized
                : playerMove.LastMove.normalized;
            if (direction.sqrMagnitude < 0.01f) return;

            dashCharges--;
            NotifyStaminaChanged();
            StartCoroutine(Dash(direction));

            if (playerSoundController != null) playerSoundController.PlayDodge(volume);
        }

        private IEnumerator Dash(Vector2 direction)
        {
            isInvulnerable = true;
            isDashing = true;
            dashHitTargets.Clear();

            if (trail != null) trail.enabled = true;
            if (noise != null)
            {
                noise.AmplitudeGain = 1.2f;
                noise.FrequencyGain = 2f;
            }

            if (rb != null) rb.linearVelocity = direction * dashForce;

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, dashTime);
            while (elapsed < duration)
            {
                DetectDashHits();
                elapsed += Time.deltaTime;
                yield return null;
            }

            DetectDashHits();
            EndDash();
        }

        private void DetectDashHits()
        {
            if (dashDamage <= 0f) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position,
                Mathf.Max(0.1f, dashHitRadius));
            foreach (Collider2D hit in hits)
            {
                if (hit == null) continue;
                Health targetHealth = hit.GetComponentInParent<Health>();
                if (targetHealth == null || !targetHealth.IsAlive || targetHealth.CompareTag("Player") ||
                    dashHitTargets.Contains(targetHealth)) continue;

                dashHitTargets.Add(targetHealth);
                float before = targetHealth.CurrentHealth;
                targetHealth.TakeDamage(dashDamage);
                float dealtDamage = Mathf.Max(0f, before - targetHealth.CurrentHealth);
                if (dealtDamage > 0f && targetHealth.CompareTag("Enemy"))
                    powerUp?.RegisterEnemyHit(dealtDamage);
            }
        }

        private void RechargeDash()
        {
            if (dashCharges >= maxDashCharges)
            {
                rechargeTimer = 0f;
                return;
            }

            rechargeTimer += Time.deltaTime;
            float interval = Mathf.Max(0.1f, dashRechargeTime);
            if (rechargeTimer < interval)
            {
                NotifyDashChargesChanged();
                return;
            }

            rechargeTimer -= interval;
            dashCharges = Mathf.Min(maxDashCharges, dashCharges + 1);
            NotifyStaminaChanged();
        }

        private void EndDash()
        {
            if (noise != null)
            {
                noise.AmplitudeGain = 0f;
                noise.FrequencyGain = 0f;
            }

            if (trail != null) trail.enabled = false;
            isDashing = false;
            isInvulnerable = false;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            dashHitTargets.Clear();
            EndDash();
        }

        public void RestoreStamina(float value)
        {
            StopAllCoroutines();
            EndDash();
            dashCharges = Mathf.Clamp(Mathf.RoundToInt(value), 0, maxDashCharges);
            rechargeTimer = 0f;
            NotifyStaminaChanged();
        }

        private void NotifyStaminaChanged()
        {
            OnStaminaChanged?.Invoke(dashCharges, maxDashCharges);
            NotifyDashChargesChanged();
        }

        private void NotifyDashChargesChanged()
        {
            OnDashChargesChanged?.Invoke(dashCharges, maxDashCharges, RechargeProgress);
        }

        private void OnValidate()
        {
            dashForce = Mathf.Max(0f, dashForce);
            dashTime = Mathf.Max(0.01f, dashTime);
            maxDashCharges = Mathf.Clamp(maxDashCharges, 1, 8);
            dashRechargeTime = Mathf.Max(0.1f, dashRechargeTime);
            dashDamage = Mathf.Max(0f, dashDamage);
            dashHitRadius = Mathf.Max(0.1f, dashHitRadius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.08f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, dashHitRadius);
        }
    }
}
