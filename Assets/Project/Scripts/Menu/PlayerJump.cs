using DG.Tweening;
using UnityEngine;

namespace Project.Scripts.Menu
{
    public class PlayerJump : MonoBehaviour
    {
        [SerializeField] private GameObject player;
        [SerializeField] private float jumpHeight = 1f;
        [SerializeField] private float jumpDuration = 0.5f;

        private Vector3 originalPosition;
        private Vector3 originalScale;
        private Tween idleTween;

        private void Awake()
        {
            if (player == null) player = gameObject;
            originalPosition = player.transform.position;
            originalScale = player.transform.localScale;
        }

        private void Start()
        {
            PlayStableIdle();
        }

        private void OnDisable()
        {
            idleTween?.Kill();
            if (player == null) return;
            player.transform.position = originalPosition;
            player.transform.localScale = originalScale;
        }

        public void Jump()
        {
            PlayStableIdle();
        }

        private void PlayStableIdle()
        {
            if (player == null) return;
            idleTween?.Kill();
            player.transform.position = originalPosition;
            player.transform.localScale = originalScale;
            float breathingAmount = Mathf.Clamp(jumpHeight * 0.025f, 0.015f, 0.06f);
            idleTween = player.transform
                .DOScale(originalScale * (1f + breathingAmount), Mathf.Max(0.4f, jumpDuration))
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetLink(player, LinkBehaviour.KillOnDisable);
        }
    }
}
