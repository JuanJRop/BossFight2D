using Project.Characters.Player.PlayerScripts.Controller;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Movement
{
    public class PlayerMove : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float speed = 5f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 8f;

        [Header("Footsteps")]
        [SerializeField] private float walkStepRate = 0.5f;
        [SerializeField] private float runStepRate = 0.3f;
        [SerializeField] private AudioClip walkStep;
        [SerializeField] private AudioClip runStep;
        [SerializeField, Range(0f, 0.5f)] private float volume;

        private PlayerSoundController playerSoundController;
        private PlayerDodge playerDodge;
        private Rigidbody2D rb;
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private Vector2 moveInput;
        private Vector2 lastMove;
        private Vector2 knockbackVelocity;
        private float currentSpeed;
        private float stepTimer;
        private float knockbackTimer;
        private float stunTimer;
        private bool isMoving = true;
        private PlayerElectricStunFeedback stunFeedback;

        public Vector2 MoveInput => moveInput;
        public bool IsBeingKnockedBack => knockbackTimer > 0f;
        public bool IsStunned => stunTimer > 0f;

        private void Awake()
        {
            speed *= GameLoadout.MoveSpeedMultiplier;
            playerSoundController = GetComponent<PlayerSoundController>();
            playerDodge = GetComponent<PlayerDodge>();
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            stunFeedback = GetComponent<PlayerElectricStunFeedback>();
            if (stunFeedback == null) stunFeedback = gameObject.AddComponent<PlayerElectricStunFeedback>();
            PlayerSkinController.Attach(gameObject, animator, spriteRenderer);
        }

        private void Update()
        {
            if (!isMoving) return;

            if (stunTimer > 0f)
            {
                stunTimer = Mathf.Max(0f, stunTimer - Time.deltaTime);
                moveInput = Vector2.zero;
                currentSpeed = 0f;
                UpdateAnimations();
                return;
            }

            InputMovement();
            HandleSpeed();
            UpdateAnimations();
            HandleFlip();
            HandleFootsteps();
        }

        private void FixedUpdate()
        {
            if (rb == null) return;
            if (IsStunned)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }
            if (playerDodge != null && playerDodge.IsDashing) return;

            if (knockbackTimer > 0f)
            {
                knockbackTimer = Mathf.Max(0.05f, knockbackTimer - Time.fixedDeltaTime);
                rb.linearVelocity = knockbackVelocity;
                knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero,
                    deceleration * Time.fixedDeltaTime);
                return;
            }

            rb.linearVelocity = moveInput * speed;
        }

        public void ApplyKnockback(Vector2 velocity, float duration)
        {
            if (playerDodge != null && playerDodge.IsInvulnerable) return;

            knockbackVelocity = velocity;
            knockbackTimer = Mathf.Max(0.05f, duration);
            if (rb != null) rb.linearVelocity = knockbackVelocity;
        }

        public void ApplyStun(float duration)
        {
            if (playerDodge != null && playerDodge.IsInvulnerable) return;

            stunTimer = Mathf.Max(stunTimer, Mathf.Max(0.05f, duration));
            knockbackTimer = 0f;
            knockbackVelocity = Vector2.zero;
            moveInput = Vector2.zero;
            currentSpeed = 0f;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (stunFeedback != null) stunFeedback.Show(stunTimer);
        }

        private void InputMovement()
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");
            moveInput = new Vector2(moveX, moveY).normalized;

            if (moveInput != Vector2.zero) lastMove = moveInput;
        }

        private void HandleSpeed()
        {
            float target = moveInput != Vector2.zero ? 1f : 0f;
            float rate = target > currentSpeed ? acceleration : deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.deltaTime);
        }

        private void HandleFootsteps()
        {
            if (moveInput == Vector2.zero || playerSoundController == null) return;

            stepTimer -= Time.deltaTime;
            if (stepTimer > 0f) return;

            bool isRunning = currentSpeed > 0.6f;
            playerSoundController.PlayWalk(isRunning ? runStep : walkStep, volume);
            stepTimer = Mathf.Max(0.05f, isRunning ? runStepRate : walkStepRate);
        }

        private void UpdateAnimations()
        {
            if (animator == null) return;
            animator.SetFloat("Horizontal", lastMove.x);
            animator.SetFloat("Vertical", lastMove.y);
            animator.SetFloat("Speed", currentSpeed);
        }

        private void HandleFlip()
        {
            if (spriteRenderer == null) return;

            if (moveInput.x < 0f)
            {
                spriteRenderer.flipX = true;
                lastMove.x = -lastMove.x;
            }
            else if (moveInput.x > 0f)
            {
                spriteRenderer.flipX = false;
            }
        }

        private void OnDisable()
        {
            knockbackTimer = 0f;
            stunTimer = 0f;
            knockbackVelocity = Vector2.zero;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            walkStepRate = Mathf.Max(0.05f, walkStepRate);
            runStepRate = Mathf.Max(0.05f, runStepRate);
        }
    }
}
