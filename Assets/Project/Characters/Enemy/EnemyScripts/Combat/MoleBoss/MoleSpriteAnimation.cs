using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MoleSpriteAnimation : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float framesPerSecond = 12f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool randomStart;

        private float elapsed;
        private int frameIndex;

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            frameIndex = randomStart && frames != null && frames.Length > 0 ? Random.Range(0, frames.Length) : 0;
            elapsed = 0f;
            ApplyFrame();
        }

        private void Update()
        {
            if (targetRenderer == null || frames == null || frames.Length < 2) return;
            elapsed += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            while (elapsed >= frameDuration)
            {
                elapsed -= frameDuration;
                frameIndex++;
                if (frameIndex >= frames.Length)
                {
                    frameIndex = loop ? 0 : frames.Length - 1;
                    if (!loop) enabled = false;
                }
                ApplyFrame();
            }
        }

        private void ApplyFrame()
        {
            if (targetRenderer != null && frames != null && frames.Length > 0)
                targetRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
        }
    }
}
