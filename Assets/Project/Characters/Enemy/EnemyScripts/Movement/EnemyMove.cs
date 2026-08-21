using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.Characters.Enemy.EnemyScripts.Movement
{
    public class EnemyMove : MonoBehaviour
    {
        [Header("Burrow Spots")]
        [SerializeField] private Transform[] spots;

        [Header("Autonomous Movement")]
        [SerializeField] private float timeSpot = 1f;
        [SerializeField] private float pointClose = 0.2f;
        [SerializeField] private float speedBetweenPoints = 3f;

        [Header("References")]
        [SerializeField] private Transform enemy;
        [SerializeField] private Collider2D col;

        [SerializeField] private int currentSpot;

        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private Collider2D[] colliders;
        private bool isWaiting;
        private bool isMoving;
        private bool isUnderGround;
        private bool aiControlled;

        public bool IsWaiting => isWaiting && !isMoving;
        public bool IsUnderGround => isUnderGround;
        public bool HasValidSpots => spots != null && spots.Length > 0;

        private void Awake()
        {
            enemy = enemy != null ? enemy : transform;
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (col == null) col = GetComponent<Collider2D>();
            colliders = GetComponents<Collider2D>();

            if (HasValidSpots)
            {
                currentSpot = Mathf.Clamp(currentSpot, 0, spots.Length - 1);
                if (spots[currentSpot] != null) enemy.position = spots[currentSpot].position;
            }
        }

        private void Update()
        {
            if (aiControlled || !HasValidSpots) return;

            if (isMoving)
            {
                MoveTowardsCurrentSpot();
                if (ReachedCurrentSpot()) FinishMovement();
                return;
            }

            if (isWaiting) return;

            MoveTowardsCurrentSpot();
            if (ReachedCurrentSpot())
            {
                isWaiting = true;
                StartCoroutine(WaitAndMove());
            }
        }

        public void SetAiControlled(bool controlled)
        {
            aiControlled = controlled;
            if (!controlled) return;

            StopAllCoroutines();
            ForceEmerge();
        }

        public IEnumerator BurrowToRandomSpot(float hideDuration)
        {
            if (!HasValidSpots) yield break;

            isWaiting = true;
            isMoving = false;
            isUnderGround = true;
            SetCollidersEnabled(false);
            if (animator != null) animator.SetTrigger("Hide");

            yield return new WaitForSeconds(Mathf.Max(0.1f, hideDuration));
            if (spriteRenderer != null) spriteRenderer.enabled = false;

            currentSpot = SelectDifferentSpot();
            Transform destination = spots[currentSpot];
            if (destination != null)
            {
                while (Vector2.Distance(enemy.position, destination.position) > Mathf.Max(0.02f, pointClose))
                {
                    enemy.position = Vector2.MoveTowards(enemy.position, destination.position,
                        Mathf.Max(0.1f, speedBetweenPoints) * Time.deltaTime);
                    yield return null;
                }

                enemy.position = destination.position;
            }

            ForceEmerge();
        }

        public void ForceEmerge()
        {
            isMoving = false;
            isWaiting = false;
            isUnderGround = false;
            SetCollidersEnabled(true);
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            if (animator != null) animator.SetTrigger("Exit");
        }

        private IEnumerator WaitAndMove()
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, timeSpot));
            yield return BurrowToRandomSpot(1f);
        }

        private void MoveTowardsCurrentSpot()
        {
            Transform destination = spots[currentSpot];
            if (destination == null) return;
            enemy.position = Vector2.MoveTowards(enemy.position, destination.position,
                Time.deltaTime * Mathf.Max(0.1f, speedBetweenPoints));
        }

        private bool ReachedCurrentSpot()
        {
            Transform destination = spots[currentSpot];
            return destination == null || Vector2.Distance(enemy.position, destination.position) < Mathf.Max(0.02f, pointClose);
        }

        private int SelectDifferentSpot()
        {
            if (spots.Length <= 1) return 0;

            int selected = Random.Range(0, spots.Length - 1);
            if (selected >= currentSpot) selected++;
            return selected;
        }

        private void FinishMovement()
        {
            ForceEmerge();
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (colliders != null && colliders.Length > 0)
            {
                foreach (Collider2D bossCollider in colliders)
                {
                    if (bossCollider != null) bossCollider.enabled = enabled;
                }

                return;
            }

            if (col != null) col.enabled = enabled;
        }

        private void OnValidate()
        {
            timeSpot = Mathf.Max(0.1f, timeSpot);
            pointClose = Mathf.Max(0.02f, pointClose);
            speedBetweenPoints = Mathf.Max(0.1f, speedBetweenPoints);
        }
    }
}
