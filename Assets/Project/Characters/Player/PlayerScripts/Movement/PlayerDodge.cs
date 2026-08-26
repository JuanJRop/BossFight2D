using System;
using System.Collections;
using Project.Characters.Player.PlayerScripts.Controller;
using Project.Scripts.Controller;
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
        [SerializeField, Range(1, 6)] private int maxDashCharges = 3;
        [SerializeField, Min(0.1f)] private float dashRechargeTime = 2.4f;

        [Header("References")]
        [SerializeField] private TrailRenderer trail;

        [Header("Camera Shake")]
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField, Range(0f, 0.5f)] private float volume;

        private PlayerMove playerMove;
        private Rigidbody2D rb;
        private CinemachineBasicMultiChannelPerlin noise;
        private PlayerSoundController playerSoundController;
        private float rechargeTimer;
        private int dashCharges;
        private bool isDashing;
        private bool isInvulnerable;

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
            noise = virtualCamera != null
                ? virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>()
                : null;

            if (trail != null) trail.enabled = false;
            dashCharges = Mathf.Max(1, maxDashCharges);
            NotifyStaminaChanged();
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
                playerMove.MoveInput.sqrMagnitude < 0.01f || dashCharges <= 0) return;

            dashCharges--;
            NotifyStaminaChanged();
            StartCoroutine(Dash(playerMove.MoveInput.normalized));

            if (playerSoundController != null) playerSoundController.PlayDodge(volume);
        }

        private IEnumerator Dash(Vector2 direction)
        {
            isInvulnerable = true;
            isDashing = true;

            if (trail != null) trail.enabled = true;
            if (noise != null)
            {
                noise.AmplitudeGain = 1.2f;
                noise.FrequencyGain = 2f;
            }

            if (rb != null) rb.linearVelocity = direction * dashForce;
            yield return new WaitForSeconds(Mathf.Max(0.01f, dashTime));
            EndDash();
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
            maxDashCharges = Mathf.Clamp(maxDashCharges, 1, 6);
            dashRechargeTime = Mathf.Max(0.1f, dashRechargeTime);
        }
    }
}
