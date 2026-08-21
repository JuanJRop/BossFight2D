using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    [DisallowMultipleComponent]
    public sealed class MoleVisualMotion : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed;
        [SerializeField, Min(0f)] private float pulseAmount;
        [SerializeField, Min(0f)] private float pulseSpeed = 6f;

        private Vector3 initialScale;
        private float phase;

        private void OnEnable()
        {
            initialScale = transform.localScale;
            phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            if (!Mathf.Approximately(rotationSpeed, 0f))
                transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

            if (pulseAmount <= 0f) return;
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed + phase) * pulseAmount;
            transform.localScale = initialScale * pulse;
        }
    }
}
