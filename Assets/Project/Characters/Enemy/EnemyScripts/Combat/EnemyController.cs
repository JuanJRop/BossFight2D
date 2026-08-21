using System;
using System.Collections;
using Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss;
using Project.Characters.Enemy.EnemyScripts.Combat.MoleBoss.Attacks;
using Project.Characters.Enemy.EnemyScripts.Core;
using Project.Characters.Enemy.EnemyScripts.Movement;
using Project.Scripts.Boss;
using Project.Scripts.Controller;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public sealed class EnemyAttackController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MoleBossCombatConfig config;
        [SerializeField] private Transform firePoint;
        [SerializeField] private Transform player;
        [SerializeField] private ObjectPool pool;
        [SerializeField] private Animator animator;
        [SerializeField] private Health bossHealth;
        [SerializeField] private BossPhaseController phaseController;
        [SerializeField] private EnemyMove movement;
        [SerializeField] private Rigidbody2D bossBody;

        private Coroutine attackRoutine;
        private MoleBossCombatContext context;
        private MoleBossAttackSelector selector;
        private MoleBossAttackRegistry registry;
        private MoleBossPhaseTransition phaseTransition;
        private MoleBossTelegraphService telegraphs;
        private int observedPhase;
        private bool phaseTransitionPending;
        private bool missingReferencesReported;

        public event Action<MoleBossState> OnStateChanged;
        public event Action<MoleBossAttack> OnAttackStarted;

        public MoleBossState CurrentState { get; private set; } = MoleBossState.Dormant;
        public MoleBossAttack CurrentAttack { get; private set; }
        public int CurrentPhase => phaseController != null ? phaseController.CurrentPhase : GetHealthPhase();

        private void Awake()
        {
            ResolveUnityReferences();
            ComposeCombatObjects();
            if (movement != null) movement.SetAiControlled(true);
        }

        private void OnEnable() => RestartAttacks();

        private void OnDisable()
        {
            StopAttackRoutine();
            telegraphs?.ReleaseAll();
            if (bossBody != null) bossBody.linearVelocity = Vector2.zero;
            SetState(bossHealth != null && !bossHealth.IsAlive ? MoleBossState.Defeated : MoleBossState.Dormant);
        }

        private void OnDestroy() => telegraphs?.Dispose();

        public void RestartAttacks()
        {
            if (!isActiveAndEnabled) return;
            ResolveUnityReferences();
            ComposeCombatObjects();
            int nextPhase = CurrentPhase;
            phaseTransitionPending = observedPhase > 0 && nextPhase != observedPhase;
            StopAttackRoutine();
            telegraphs.ReleaseAll();

            if (movement != null)
            {
                movement.SetAiControlled(true);
                movement.ForceEmerge();
            }
            if (bossBody != null) bossBody.linearVelocity = Vector2.zero;
            attackRoutine = StartCoroutine(AttackLoop());
        }

        private IEnumerator AttackLoop()
        {
            yield return null;
            if (!HasRequiredReferences())
            {
                ReportMissingReferences();
                SetState(MoleBossState.Dormant);
                attackRoutine = null;
                yield break;
            }

            missingReferencesReported = false;
            observedPhase = CurrentPhase;
            if (phaseTransitionPending)
            {
                phaseTransitionPending = false;
                yield return phaseTransition.Execute(context);
            }

            while (bossHealth == null || bossHealth.IsAlive)
            {
                int phase = CurrentPhase;
                if (phase != observedPhase)
                {
                    observedPhase = phase;
                    yield return phaseTransition.Execute(context);
                }

                yield return context.Wait(UnityEngine.Random.Range(config.MinDelay(phase), config.MaxDelay(phase)));
                if (ShouldBurrow(phase))
                {
                    SetState(MoleBossState.Burrowing);
                    yield return movement.BurrowToRandomSpot(config.BurrowHideTime);
                    SetState(MoleBossState.Emerging);
                    yield return context.Wait(0.2f);
                }

                float distance = Vector2.Distance(transform.position, context.Player.Position);
                CurrentAttack = selector.Select(phase, distance);
                OnAttackStarted?.Invoke(CurrentAttack);
                if (registry.TryGet(CurrentAttack, out IMoleBossAttack attack))
                    yield return attack.Execute(context, phase);

                SetState(MoleBossState.Recovering);
                yield return context.Wait(config.Recovery(phase));
            }

            SetState(MoleBossState.Defeated);
            attackRoutine = null;
        }

        private void ComposeCombatObjects()
        {
            telegraphs ??= new MoleBossTelegraphService();
            selector ??= new MoleBossAttackSelector();
            MoleBossPlayerTarget target = new(player);
            MoleBossProjectileEmitter projectiles = new(pool, config);
            context = new MoleBossCombatContext(transform, firePoint, animator, bossBody, movement, target,
                projectiles, telegraphs, config, SetState);

            IMoleBossAttack radial = new RadialBurstAttack();
            registry = new MoleBossAttackRegistry(new IMoleBossAttack[]
            {
                new AimedFanAttack(), radial, new SpiralAttack(), new CorridorAttack(),
                new CrossfireAttack(), new RockRainAttack(), new ChargeDashAttack()
            });
            phaseTransition = new MoleBossPhaseTransition(radial);
        }

        private void ResolveUnityReferences()
        {
            if (firePoint == null) firePoint = transform;
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) player = playerObject.transform;
            }
            if (pool == null) pool = FindFirstObjectByType<ObjectPool>();
            if (bossHealth == null) bossHealth = GetComponent<Health>();
            if (phaseController == null) phaseController = GetComponent<BossPhaseController>();
            if (movement == null) movement = GetComponent<EnemyMove>();
            if (bossBody == null) bossBody = GetComponent<Rigidbody2D>();
            if (animator == null) animator = GetComponent<Animator>();
        }

        private bool HasRequiredReferences()
        {
            return config != null && context != null && context.Player.IsValid && context.Projectiles.IsValid && firePoint != null;
        }

        private bool ShouldBurrow(int phase)
        {
            return movement != null && movement.HasValidSpots && UnityEngine.Random.value < config.BurrowChance(phase);
        }

        private int GetHealthPhase()
        {
            return bossHealth != null && bossHealth.NormalizedHealth <= 0.4f ? 2 : 1;
        }

        private void SetState(MoleBossState state)
        {
            if (CurrentState == state) return;
            CurrentState = state;
            OnStateChanged?.Invoke(state);
        }

        private void StopAttackRoutine()
        {
            if (attackRoutine == null) return;
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        private void ReportMissingReferences()
        {
            if (missingReferencesReported) return;
            Debug.LogError("Mole boss AI requires its combat config, player, projectile pool, fire point and projectile data.", this);
            missingReferencesReported = true;
        }
    }
}
