using System.Collections;
using Project.Characters.Player.PlayerScripts.Controller;
using UnityEngine;

namespace Project.Scripts.Controller
{
    public class DeathEvent : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour[] componentsToDisable;
        [SerializeField] private Rigidbody2D rb2DsToDisable;
        [SerializeField] private Animator animator;
        [SerializeField, Range(0f, 0.5f)] private float volumeLose;
        [SerializeField] private GameObject DeathMenu;

        private PlayerSoundController soundController;
        private Coroutine openMenuRoutine;

        private void Awake()
        {
            soundController = GetComponent<PlayerSoundController>();
            if (animator == null) animator = GetComponent<Animator>();
            if (rb2DsToDisable == null) rb2DsToDisable = GetComponent<Rigidbody2D>();
        }

        public void Die()
        {
            if (soundController != null) soundController.PlayLose(volumeLose);

            foreach (MonoBehaviour componentToDisable in componentsToDisable ?? System.Array.Empty<MonoBehaviour>())
            {
                if (componentToDisable != null) componentToDisable.enabled = false;
            }

            if (rb2DsToDisable != null) rb2DsToDisable.linearVelocity = Vector2.zero;
            if (animator != null) animator.SetTrigger("Die");

            if (openMenuRoutine != null) StopCoroutine(openMenuRoutine);
            openMenuRoutine = StartCoroutine(OpenUI());
        }

        public void Revive()
        {
            if (openMenuRoutine != null)
            {
                StopCoroutine(openMenuRoutine);
                openMenuRoutine = null;
            }

            foreach (MonoBehaviour componentToDisable in componentsToDisable ?? System.Array.Empty<MonoBehaviour>())
            {
                if (componentToDisable != null) componentToDisable.enabled = true;
            }

            if (rb2DsToDisable != null) rb2DsToDisable.linearVelocity = Vector2.zero;
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            if (DeathMenu != null) DeathMenu.SetActive(false);
            if (UIManager.instance != null) UIManager.instance.IsPaused = false;
            Time.timeScale = 1f;
        }

        private IEnumerator OpenUI()
        {
            yield return new WaitForSeconds(2f);
            openMenuRoutine = null;

            if (UIManager.instance != null && DeathMenu != null)
            {
                UIManager.instance.YouDieManager(DeathMenu);
            }
        }
    }
}
