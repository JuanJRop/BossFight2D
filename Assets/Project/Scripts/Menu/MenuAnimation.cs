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

        private void Start()
        {
            if (isMenu) StartCoroutine(MovementBullets());
        }

        private IEnumerator MovementBullets()
        {
            while (isMenu)
            {
                foreach (GameObject bullet in spawner)
                {
                    if (bullet == null || spawnPoint == null || endPoint == null) continue;

                    GameObject instance = Instantiate(bullet, spawnPoint.position, spawnPoint.rotation);
                    instance.transform
                        .DOMove(endPoint.position, Mathf.Max(0.01f, travelTime))
                        .SetEase(Ease.Linear)
                        .OnComplete(() => Destroy(instance));

                    yield return new WaitForSeconds(Mathf.Max(0.01f, timeBullets));
                }
            }
        }
    }
}
