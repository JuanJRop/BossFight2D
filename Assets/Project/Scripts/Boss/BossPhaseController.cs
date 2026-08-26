using System;
using Project.Characters.Enemy.EnemyScripts.Combat;
using Project.Characters.Enemy.EnemyScripts.Core;
using UnityEngine;

namespace Project.Scripts.Boss
{
    public class BossPhaseController : MonoBehaviour
    {
        [SerializeField] private Health bossHealth;
        [SerializeField] private EnemyAttackController attackController;
        [SerializeField, Range(0.05f, 0.95f)] private float phaseTwoThreshold = 0.5f;

        public event Action<int> OnPhaseChanged;

        public int CurrentPhase { get; private set; } = 1;
        public float PhaseTwoThreshold => phaseTwoThreshold;

        private void Awake()
        {
            if (bossHealth == null) bossHealth = GetComponent<Health>();
            if (attackController == null) attackController = GetComponent<EnemyAttackController>();
            phaseTwoThreshold = 0.5f;
        }

        private void OnEnable()
        {
            if (bossHealth != null) bossHealth.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            if (bossHealth != null) bossHealth.OnHealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(float current, float maximum)
        {
            if (CurrentPhase != 1 || maximum <= 0f) return;
            if (current / maximum > phaseTwoThreshold) return;
            SetPhase(2);
        }

        private void SetPhase(int phase)
        {
            int nextPhase = Mathf.Clamp(phase, 1, 2);
            if (CurrentPhase == nextPhase) return;

            CurrentPhase = nextPhase;
            OnPhaseChanged?.Invoke(CurrentPhase);
            if (attackController != null) attackController.RestartAttacks();
        }

        public void RestartCurrentPhase()
        {
            if (bossHealth == null) return;

            float health = CurrentPhase == 2
                ? bossHealth.MaxHealth * phaseTwoThreshold
                : bossHealth.MaxHealth;

            bossHealth.RestoreHealth(health);
            if (attackController != null) attackController.RestartAttacks();
            OnPhaseChanged?.Invoke(CurrentPhase);
        }

        private void OnValidate()
        {
            phaseTwoThreshold = 0.5f;
        }
    }
}
