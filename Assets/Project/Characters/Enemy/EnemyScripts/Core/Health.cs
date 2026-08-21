using System;
using System.Collections;
using Project.Characters.Enemy.EnemyScripts.Movement;
using Project.Characters.Player.PlayerScripts.Controller;
using Project.Scripts.Controller;
using Unity.Cinemachine;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Core
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float flashTime = 0.1f;
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private float amplitude;
        [SerializeField] private float frequency;
        [SerializeField] private DeathEvent deathEvent;
        [SerializeField] private AudioClip damageSound;
        [SerializeField, Range(0f, 0.5f)] private float volume;
        [SerializeField] private EnemyMove enemyMove;

        private float currentHealth;
        private bool isAlive;
        private CinemachineBasicMultiChannelPerlin noise;
        private PlayerSoundController soundController;
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private Coroutine feedbackRoutine;

        public event Action<float, float> OnHealthChanged;
        public event Action OnDied;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float NormalizedHealth => maxHealth > 0f ? currentHealth / maxHealth : 0f;
        public bool IsAlive => isAlive;

        private void Awake()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = maxHealth;
            isAlive = true;

            soundController = GetComponent<PlayerSoundController>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) originalColor = spriteRenderer.color;

            noise = virtualCamera != null
                ? virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>()
                : null;
        }

        public void TakeDamage(float damage)
        {
            if (!isAlive || damage <= 0f) return;
            if (enemyMove != null && enemyMove.IsUnderGround) return;

            currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
            feedbackRoutine = StartCoroutine(ApplyDamageFeedback());

            if (currentHealth > 0f) return;

            isAlive = false;
            OnDied?.Invoke();
            if (deathEvent != null) deathEvent.Die();
        }

        public void Heal(float amount)
        {
            if (!isAlive || amount <= 0f) return;
            RestoreHealth(currentHealth + amount);
        }

        public void RestoreHealth(float value)
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
                feedbackRoutine = null;
            }

            currentHealth = Mathf.Clamp(value, 0f, maxHealth);
            isAlive = currentHealth > 0f;
            ResetFeedback();
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void RestoreHealthAtLeast(float savedHealth, float minimumNormalized)
        {
            float minimum = maxHealth * Mathf.Clamp01(minimumNormalized);
            RestoreHealth(Mathf.Max(savedHealth, minimum));
        }

        private IEnumerator ApplyDamageFeedback()
        {
            if (soundController != null) soundController.PlayDamage(damageSound, volume);
            if (spriteRenderer != null) spriteRenderer.color = Color.red;

            if (noise != null)
            {
                noise.AmplitudeGain = amplitude;
                noise.FrequencyGain = frequency;
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, flashTime));
            ResetFeedback();
            feedbackRoutine = null;
        }

        private void ResetFeedback()
        {
            if (noise != null)
            {
                noise.AmplitudeGain = 0f;
                noise.FrequencyGain = 0f;
            }

            if (spriteRenderer != null) spriteRenderer.color = originalColor;
        }
    }
}
