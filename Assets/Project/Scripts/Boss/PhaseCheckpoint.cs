using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Player.PlayerScripts.Combat;
using Project.Characters.Player.PlayerScripts.Core;
using Project.Characters.Player.PlayerScripts.Movement;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Scripts.Boss
{
    public class PhaseCheckpoint : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Health playerHealth;
        [SerializeField] private DeathEvent playerDeath;
        [SerializeField] private AttackPlayer playerAttack;
        [SerializeField] private PlayerDodge playerDodge;
        [SerializeField] private PowerUp playerPowerUp;

        [Header("Encounter")]
        [SerializeField] private BossPhaseController bossPhase;
        [SerializeField] private ObjectPool projectilePool;
        [SerializeField, Range(0f, 1f)] private float minimumRestartHealth = 0.5f;

        private CheckpointState checkpoint;
        private bool restoring;

        private void Start()
        {
            CaptureCheckpoint(1);
        }

        private void OnEnable()
        {
            if (bossPhase != null) bossPhase.OnPhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            if (bossPhase != null) bossPhase.OnPhaseChanged -= HandlePhaseChanged;
        }

        private void HandlePhaseChanged(int phase)
        {
            if (!restoring) CaptureCheckpoint(phase);
        }

        private void CaptureCheckpoint(int phase)
        {
            checkpoint = new CheckpointState
            {
                IsValid = true,
                Phase = Mathf.Clamp(phase, 1, 2),
                PlayerPosition = playerTransform != null ? playerTransform.position : Vector3.zero,
                PlayerHealth = playerHealth != null ? playerHealth.CurrentHealth : 0f,
                ShotsUsed = playerAttack != null ? playerAttack.ShotsUsed : 0,
                Stamina = playerDodge != null ? playerDodge.CurrentStamina : 0f,
                Mana = playerPowerUp != null ? playerPowerUp.CurrentMana : 0f
            };
        }

        public void RestartPhase()
        {
            if (!checkpoint.IsValid) return;

            restoring = true;
            Time.timeScale = 1f;

            if (projectilePool != null) projectilePool.ReturnAll();
            if (playerDeath != null) playerDeath.Revive();

            if (playerTransform != null) playerTransform.position = checkpoint.PlayerPosition;
            if (playerHealth != null)
            {
                playerHealth.RestoreHealthAtLeast(checkpoint.PlayerHealth, minimumRestartHealth);
            }

            if (playerAttack != null) playerAttack.RestoreCombatState(checkpoint.ShotsUsed);
            if (playerDodge != null) playerDodge.RestoreStamina(checkpoint.Stamina);
            if (playerPowerUp != null) playerPowerUp.RestoreMana(checkpoint.Mana);
            if (bossPhase != null) bossPhase.RestartCurrentPhase();

            restoring = false;
        }

        private struct CheckpointState
        {
            public bool IsValid;
            public int Phase;
            public Vector3 PlayerPosition;
            public float PlayerHealth;
            public int ShotsUsed;
            public float Stamina;
            public float Mana;
        }
    }
}
