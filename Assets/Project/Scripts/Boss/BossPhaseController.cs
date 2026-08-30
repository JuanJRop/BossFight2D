using System;
using Project.Characters.Enemy.EnemyScripts.Combat;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Scripts.Controller;
using Project.Scripts.Progression;
using TMPro;
using UnityEngine;

namespace Project.Scripts.Boss
{
    public class BossPhaseController : MonoBehaviour
    {
        [SerializeField] private Health bossHealth;
        [SerializeField] private EnemyAttackController attackController;
        [SerializeField, Range(0.05f, 0.95f)] private float phaseTwoThreshold = 0.5f;
        [Header("Victory Reward")]
        [SerializeField, Min(0)] private int goldReward = 100;
        [SerializeField, Min(0)] private int experienceReward = 200;

        public event Action<int> OnPhaseChanged;

        public int CurrentPhase { get; private set; } = 1;
        public float PhaseTwoThreshold => phaseTwoThreshold;

        private bool rewardGranted;

        private void Awake()
        {
            if (bossHealth == null) bossHealth = GetComponent<Health>();
            if (attackController == null) attackController = GetComponent<EnemyAttackController>();
            phaseTwoThreshold = 0.5f;
        }

        private void OnEnable()
        {
            if (bossHealth != null) bossHealth.OnHealthChanged += HandleHealthChanged;
            if (bossHealth != null) bossHealth.OnDied += HandleBossDied;
        }

        private void OnDisable()
        {
            if (bossHealth != null) bossHealth.OnHealthChanged -= HandleHealthChanged;
            if (bossHealth != null) bossHealth.OnDied -= HandleBossDied;
        }

        private void HandleBossDied()
        {
            if (rewardGranted) return;
            rewardGranted = true;
            PlayerEconomy.AddGold(goldReward);
            PlayerEconomy.AddExperience(experienceReward);
            ShowVictoryReward();
        }

        private void ShowVictoryReward()
        {
            GameObject winScreen = FindSceneObject("WinScreen");
            if (winScreen == null) return;

            Transform existing = winScreen.transform.Find("Victory Reward Summary");
            TextMeshProUGUI rewardText = existing != null
                ? existing.GetComponent<TextMeshProUGUI>()
                : CreateVictoryRewardText(winScreen.transform);
            if (rewardText == null) return;

            rewardText.text = GameLoadout.IsSpanish
                ? $"ORO +{goldReward}   EXP +{experienceReward}"
                : $"GOLD +{goldReward}   XP +{experienceReward}";
        }

        private static TextMeshProUGUI CreateVictoryRewardText(Transform parent)
        {
            GameObject rewardObject = new("Victory Reward Summary");
            rewardObject.layer = parent.gameObject.layer;
            rewardObject.transform.SetParent(parent, false);

            RectTransform rect = rewardObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -92f);
            rect.sizeDelta = new Vector2(560f, 42f);

            TextMeshProUGUI text = rewardObject.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 30f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(0.35f, 1f, 0.55f, 1f);
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.name == objectName && candidate.scene.IsValid()) return candidate;
            }

            return null;
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
