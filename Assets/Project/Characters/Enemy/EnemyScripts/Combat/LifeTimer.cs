using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public class LifeTimer : MonoBehaviour
    {
        [Header("Life Settings")]
        [SerializeField] private float lifeTime = 3f;

        private float timer;

        #region Unity Lifecycle

        private void OnEnable()
        {
            ResetTimer();
        }

        private void Update()
        {
            Tick();
        }

        #endregion

        #region Logic

        private void ResetTimer()
        {
            timer = lifeTime;
        }

        private void Tick()
        {
            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                Deactivate();
            }
        }

        private void Deactivate()
        {
            gameObject.SetActive(false);
        }

        #endregion
    }
}