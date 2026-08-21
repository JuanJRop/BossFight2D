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
        [SerializeField] private float dodgeCost;

        [Header("Stamina")]
        [SerializeField] private float currentStamina;
        [SerializeField] private float maxStamina;
        [SerializeField] private float regenTime;
        [SerializeField] private float regenValue;

        [Header("References")]
        [SerializeField] private TrailRenderer trail;

        [Header("Camera Shake")]
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField, Range(0f, 0.5f)] private float volume;

        private PlayerMove playerMove;
        private Rigidbody2D rb;
        private CinemachineBasicMultiChannelPerlin noise;
        private PlayerSoundController playerSoundController;
        private float regenTimer;
        private bool isDashing;
        private bool isInvulnerable;

        public event Action<float, float> OnStaminaChanged;

        public bool IsInvulnerable => isInvulnerable;
        public bool IsDashing => isDashing;
        public float MaxStamina => maxStamina;
        public float CurrentStamina => currentStamina;

        private void Start()
        {
            playerSoundController = GetComponent<PlayerSoundController>();
            playerMove = GetComponent<PlayerMove>();
            rb = GetComponent<Rigidbody2D>();
            noise = virtualCamera != null
                ? virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>()
                : null;

            if (trail != null) trail.enabled = false;
            currentStamina = Mathf.Max(0f, maxStamina);
            NotifyStaminaChanged();
        }

        private void Update()
        {
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;

            if (Input.GetKeyDown(KeyCode.Space)) TryDodge();
            RegenerateStamina();
        }

        private void TryDodge()
        {
            if (isDashing || playerMove == null || playerMove.MoveInput.sqrMagnitude < 0.01f) return;
            if (currentStamina < dodgeCost) return;

            currentStamina = Mathf.Max(0f, currentStamina - dodgeCost);
            regenTimer = 0f;
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

        private void RegenerateStamina()
        {
            if (isDashing || currentStamina >= maxStamina) return;

            regenTimer += Time.deltaTime;
            float interval = Mathf.Max(0.01f, regenTime);
            if (regenTimer < interval) return;

            regenTimer -= interval;
            currentStamina = Mathf.Min(maxStamina, currentStamina + Mathf.Max(0f, regenValue));
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
            currentStamina = Mathf.Clamp(value, 0f, maxStamina);
            regenTimer = 0f;
            NotifyStaminaChanged();
        }

        private void NotifyStaminaChanged()
        {
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        private void OnValidate()
        {
            dashForce = Mathf.Max(0f, dashForce);
            dashTime = Mathf.Max(0.01f, dashTime);
            maxStamina = Mathf.Max(0.01f, maxStamina);
            dodgeCost = Mathf.Clamp(dodgeCost, 0f, maxStamina);
            regenTime = Mathf.Max(0.01f, regenTime);
            regenValue = Mathf.Max(0f, regenValue);
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }
    }
}
