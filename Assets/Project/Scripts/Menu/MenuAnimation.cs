using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace Project.Scripts.Menu
{
    public class MenuAnimation : MonoBehaviour
    {
        [Header("Spawner Settings")]
        [SerializeField] private GameObject[] spawner;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform endPoint;
        [SerializeField] private float timeBullets = 1f;
        [SerializeField] private float travelTime = 2f;

        [Header("Menu State")]
        [SerializeField] private bool isMenu = true;

        private Coroutine movementRoutine;

        private void OnEnable()
        {
            if (isMenu) movementRoutine = StartCoroutine(MovementBullets());
        }

        private void OnDisable()
        {
            if (movementRoutine != null) StopCoroutine(movementRoutine);
            movementRoutine = null;
            transform.DOKill();
        }

        private IEnumerator MovementBullets()
        {
            while (isMenu)
            {
                foreach (GameObject bullet in spawner)
                {
                    if (!isActiveAndEnabled) yield break;
                    if (bullet == null || spawnPoint == null || endPoint == null) continue;

                    GameObject instance = Instantiate(bullet, spawnPoint.position, spawnPoint.rotation);
                    instance.transform
                        .DOMove(endPoint.position, Mathf.Max(0.01f, travelTime))
                        .SetEase(Ease.Linear)
                        .SetUpdate(true)
                        .SetLink(instance, LinkBehaviour.KillOnDestroy)
                        .OnComplete(() =>
                        {
                            if (instance != null) Destroy(instance);
                        });

                    yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, timeBullets));
                }
            }
        }
    }
}
