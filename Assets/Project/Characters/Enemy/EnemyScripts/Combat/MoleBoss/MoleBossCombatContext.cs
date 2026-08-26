using System;
using System.Collections;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Enemy.EnemyScripts.Movement;
using Project.Scripts.Arena;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss
{
    public sealed class MoleBossCombatContext
    {
        private readonly Transform boss;
        private readonly Transform firePoint;
        private readonly Animator animator;
        private readonly AudioSource audioSource;
        private readonly SpriteRenderer bossRenderer;
        private readonly Health bossHealth;
        private readonly Collider2D[] bossColliders;
        private readonly Action<MoleBossState> changeState;
        private readonly Vector3 phaseOneScale;
        private readonly Color phaseOneColor;

        public MoleBossCombatContext(Transform boss, Transform firePoint, Animator animator, Rigidbody2D body,
            EnemyMove movement, MoleBossPlayerTarget player, MoleBossProjectileEmitter projectiles,
            MoleBossTelegraphService telegraphs, MoleBossCombatConfig config, AudioSource audioSource,
            Action<MoleBossState> changeState, Vector3 phaseOneScale, Color phaseOneColor)
        {
            this.boss = boss;
            this.firePoint = firePoint;
            this.animator = animator;
            this.changeState = changeState;
            this.audioSource = audioSource;
            bossRenderer = boss != null ? boss.GetComponentInChildren<SpriteRenderer>() : null;
            bossHealth = boss != null ? boss.GetComponentInChildren<Health>() : null;
            bossColliders = boss != null ? boss.GetComponentsInChildren<Collider2D>(true) : Array.Empty<Collider2D>();
            this.phaseOneScale = phaseOneScale;
            this.phaseOneColor = phaseOneColor;
            Body = body;
            Movement = movement;
            Player = player;
            Projectiles = projectiles;
            Telegraphs = telegraphs;
            Config = config;
        }

        public Rigidbody2D Body { get; }
        public EnemyMove Movement { get; }
        public MoleBossPlayerTarget Player { get; }
        public MoleBossProjectileEmitter Projectiles { get; }
        public MoleBossTelegraphService Telegraphs { get; }
        public MoleBossCombatConfig Config { get; }
        public Vector2 BossPosition => boss != null ? boss.position : Vector2.zero;
        public Vector2 FirePosition => firePoint != null ? firePoint.position : BossPosition;
        public Sprite BossSprite => bossRenderer != null ? bossRenderer.sprite : null;
        public Color BossColor => bossRenderer != null ? bossRenderer.color : phaseOneColor;

        public void SetState(MoleBossState state) => changeState?.Invoke(state);

        public void SetBossHidden(bool hidden)
        {
            if (bossRenderer != null) bossRenderer.enabled = !hidden;
            foreach (Collider2D collider in bossColliders)
            {
                if (collider != null) collider.enabled = !hidden;
            }
            if (bossHealth != null) bossHealth.SetExternalInvulnerable(hidden);
            if (hidden && Body != null) Body.linearVelocity = Vector2.zero;
            if (!hidden && Movement != null && (bossHealth == null || bossHealth.IsAlive)) Movement.ForceEmerge();
        }

        public void ApplyPhasePresentation(int phase, float progress = 1f)
        {
            if (boss == null || Config == null) return;
            float blend = phase == 2 ? Mathf.Clamp01(progress) : 0f;
            boss.localScale = Vector3.Lerp(phaseOneScale, phaseOneScale * Config.PhaseTwoBossScale, blend);
            Color color = Color.Lerp(phaseOneColor, Config.PhaseTwoBossColor, blend);
            if (bossHealth != null) bossHealth.SetBaseColor(color);
            else if (bossRenderer != null) bossRenderer.color = color;
        }

        public void PlaySound(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (audioSource == null || clip == null) return;
            audioSource.pitch = Mathf.Clamp(pitch, 0.5f, 1.8f);
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public void TriggerAttackAnimation()
        {
            if (animator != null) animator.SetTrigger("Attack");
        }

        public IEnumerator Wait(float duration)
        {
            float elapsed = 0f;
            while (elapsed < Mathf.Max(0f, duration))
            {
                if (!IsPaused) elapsed += Time.deltaTime;
                yield return null;
            }
        }

        public bool IsPaused => UIManager.instance != null && UIManager.instance.IsPaused;

        public void GetArenaBounds(out Vector2 minimum, out Vector2 maximum)
        {
            if (ArenaBounds.TryGet(out ArenaBounds bounds))
            {
                bounds.GetInnerBounds(out minimum, out maximum, 0.05f);
                return;
            }

            Camera camera = Camera.main;
            if (camera != null && camera.orthographic)
            {
                float distance = Mathf.Abs(camera.transform.position.z);
                minimum = camera.ViewportToWorldPoint(new Vector3(0.04f, 0.06f, distance));
                maximum = camera.ViewportToWorldPoint(new Vector3(0.96f, 0.94f, distance));
                return;
            }

            minimum = new Vector2(-9f, -5f);
            maximum = new Vector2(9f, 5f);
        }

        public static Vector2 DirectionFromAngle(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }
    }
}
