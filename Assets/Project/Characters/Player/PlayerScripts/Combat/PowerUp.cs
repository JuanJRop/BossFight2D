using System;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Combat
{
    public class PowerUp : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private float maxMana = 5f;
        [SerializeField] private float drainSpeed = 1f;
        [SerializeField] private float regenValue = 0.5f;
        [SerializeField] private float regenTime = 0.1f;

        private float currentMana;
        private float regenTimer;
        private bool isActive;

        public event Action<bool> OnPowerUpStateChanged;
        public event Action<float> OnManaChanged;

        public bool IsActive => isActive;
        public float CurrentMana => currentMana;
        public float MaxMana => maxMana;

        private void Start()
        {
            maxMana = Mathf.Max(0.01f, maxMana);
            currentMana = maxMana;
            SetActive(false);
            NotifyManaChanged();
        }

        private void Update()
        {
            if (UIManager.instance != null && UIManager.instance.IsPaused) return;

            HandleInput();
            if (isActive)
            {
                ConsumeMana();
            }
            else
            {
                RegenerateMana();
            }
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.E) && currentMana >= maxMana && !isActive)
            {
                SetActive(true);
            }
        }

        private void ConsumeMana()
        {
            currentMana = Mathf.Max(0f, currentMana - Mathf.Max(0f, drainSpeed) * Time.deltaTime);
            NotifyManaChanged();

            if (currentMana <= 0f)
            {
                SetActive(false);
            }
        }

        private void RegenerateMana()
        {
            if (currentMana >= maxMana) return;

            regenTimer += Time.deltaTime;
            float interval = Mathf.Max(0.01f, regenTime);
            if (regenTimer < interval) return;

            regenTimer -= interval;
            currentMana = Mathf.Min(maxMana, currentMana + Mathf.Max(0f, regenValue));
            NotifyManaChanged();
        }

        private void SetActive(bool active)
        {
            if (isActive == active) return;

            isActive = active;
            regenTimer = 0f;
            OnPowerUpStateChanged?.Invoke(isActive);
        }

        private void NotifyManaChanged()
        {
            OnManaChanged?.Invoke(GetManaNormalized());
        }

        public void RestoreMana(float value)
        {
            SetActive(false);
            currentMana = Mathf.Clamp(value, 0f, maxMana);
            regenTimer = 0f;
            NotifyManaChanged();
        }

        public float GetManaNormalized()
        {
            return maxMana > 0f ? currentMana / maxMana : 0f;
        }

        private void OnValidate()
        {
            maxMana = Mathf.Max(0.01f, maxMana);
            drainSpeed = Mathf.Max(0f, drainSpeed);
            regenValue = Mathf.Max(0f, regenValue);
            regenTime = Mathf.Max(0.01f, regenTime);
        }
    }
}
