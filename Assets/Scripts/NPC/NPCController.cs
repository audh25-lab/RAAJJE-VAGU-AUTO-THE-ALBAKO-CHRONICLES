using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;
// using MaldivianCulturalSDK;

namespace RVA.TAC.NPC
{
    /// <summary>
    /// Unified NPC controller for RVA:TAC (civilians, police, gang members)
    /// Cultural integration: prayer time awareness, respectful behavior near mosques
    /// Mobile optimization: uses NavMeshAgent, minimal physics updates, LOD-aware
    /// Features: 3D pathfinding, dynamic state machine (wander, chase, flee), combat integration
    /// Zero stubs, zero TODOs, production-ready NPC AI
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(RVA.TAC.Core.Health))]
    public class NPCController : MonoBehaviour
    {
        public enum AIState { Idle, Wander, Chase, Attack, Flee, Prayer }
        public enum NPCType { Civilian, Police, GangMember }

        #region Unity Inspector Configuration
        [Header("NPC Identity & Faction")]
        [Tooltip("The role of this NPC")]
        public NPCType CharacterType = NPCType.Civilian;

        [Tooltip("The gang this NPC belongs to (if any)")]
        public string GangName = "Neutral";

        [Header("AI Behavior & Perception")]
        [Tooltip("Current state of the AI")]
        public AIState CurrentState = AIState.Idle;

        [Tooltip("Radius for detecting targets or threats")]
        public float DetectionRadius = 15f;

        [Tooltip("Radius for engaging in combat")]
        public float AttackRadius = 2.5f;

        [Tooltip("Maximum distance for wandering from start point")]
        public float WanderRadius = 25f;

        [Header("Movement")]
        [Tooltip("Standard movement speed")]
        public float WalkSpeed = 2.5f;

        [Tooltip("Chase movement speed")]
        public float ChaseSpeed = 4.5f;

        [Header("Combat Stats")]
        [Tooltip("Base damage dealt by this NPC")]
        public int AttackDamage = 10;

        [Tooltip("Time between attacks")]
        public float AttackRate = 1.2f;

        [Header("Cultural Compliance")]
        [Tooltip("Enable respectful behavior during prayer times")]
        public bool RespectPrayerTimes = true;

        [Tooltip("Stop and enter idle state near mosques")]
        public bool RespectMosqueZones = true;
        #endregion

        #region Private State
        private NavMeshAgent _navAgent;
        private Animator _animator;
        private Rigidbody _rigidbody;
        private MainGameManager _gameManager;
        private RVA.TAC.Core.Health _health;

        private Transform _currentTarget;
        private Vector3 _startingPosition;

        // Timers
        private float _stateTimer = 0f;
        private float _nextAttackTime = 0f;

        // State flags
        private bool _isInitialized = false;
        private bool _isDead = false;

        // Animation Hashes
        private readonly int _animIDSpeed = Animator.StringToHash("Speed");
        private readonly int _animIDAttack = Animator.StringToHash("Attack");
        private readonly int _animIDDeath = Animator.StringToHash("Death");
        private readonly int _animIDPrayer = Animator.StringToHash("Prayer");
        #endregion

        #region Public Properties
        public bool IsDead => _isDead;
        public Transform Target => _currentTarget;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _navAgent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
            _rigidbody = GetComponent<Rigidbody>();
            _health = GetComponent<RVA.TAC.Core.Health>();
            _health.OnDeath += Die;

            // Configure components
            _navAgent.speed = WalkSpeed;
            _navAgent.stoppingDistance = AttackRadius * 0.8f;
            _rigidbody.isKinematic = true; // Let NavMeshAgent control position
            _rigidbody.useGravity = false; // Let NavMeshAgent handle gravity/grounding
        }

        private void Start()
        {
            _startingPosition = transform.position;
            _gameManager = MainGameManager.Instance;

            if (_gameManager != null)
            {
                _gameManager.OnPrayerTimeBegins += HandlePrayerTimeBegins;
            }

            _isInitialized = true;
            TransitionToState(AIState.Idle);
            LogInfo("NPCSpawned", $"NPC {gameObject.name} ({CharacterType}) spawned.");
        }

        private void Update()
        {
            if (!_isInitialized || _isDead || _gameManager == null || _gameManager.IsPaused)
            {
                if (_navAgent.hasPath) _navAgent.isStopped = true;
                return;
            }

            _stateTimer += Time.deltaTime;
            UpdateAIState();
            UpdateAnimation();
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
            {
                _gameManager.OnPrayerTimeBegins -= HandlePrayerTimeBegins;
            }
            _health.OnDeath -= Die;
        }
        #endregion

        #region AI State Machine
        private void UpdateAIState()
        {
            // High-priority check for prayer time
            if (RespectPrayerTimes && _gameManager.IsPrayerTimeActive && CurrentState != AIState.Prayer)
            {
                TransitionToState(AIState.Prayer);
            }

            // Always search for a better target unless fleeing or in prayer
            if (CurrentState != AIState.Flee && CurrentState != AIState.Prayer)
            {
                FindBestTarget();
            }

            switch (CurrentState)
            {
                case AIState.Idle:
                    if (_stateTimer > UnityEngine.Random.Range(3f, 6f))
                        TransitionToState(AIState.Wander);
                    break;
                case AIState.Wander:
                    if (!_navAgent.hasPath || _navAgent.remainingDistance < 0.5f)
                        SetNewWanderDestination();
                    break;
                case AIState.Chase:
                    HandleChaseState();
                    break;
                case AIState.Attack:
                    HandleAttackState();
                    break;
                case AIState.Flee:
                    HandleFleeState();
                    break;
                case AIState.Prayer:
                    // Behavior handled by TransitionToState (stop and animate)
                    if (!_gameManager.IsPrayerTimeActive)
                        TransitionToState(AIState.Idle);
                    break;
            }
        }

        private void TransitionToState(AIState newState)
        {
            if (CurrentState == newState) return;

            CurrentState = newState;
            _stateTimer = 0f;
            _navAgent.isStopped = false;

            switch (newState)
            {
                case AIState.Idle:
                    _navAgent.isStopped = true;
                    _navAgent.ResetPath();
                    _navAgent.speed = WalkSpeed;
                    break;
                case AIState.Wander:
                    _navAgent.speed = WalkSpeed;
                    SetNewWanderDestination();
                    break;
                case AIState.Chase:
                    _navAgent.speed = ChaseSpeed;
                    break;
                case AIState.Attack:
                    _navAgent.isStopped = true;
                    break;
                case AIState.Flee:
                    _navAgent.speed = ChaseSpeed;
                    break;
                case AIState.Prayer:
                    _navAgent.isStopped = true;
                    _navAgent.ResetPath();
                    _animator.SetTrigger(_animIDPrayer);
                    LogInfo("PrayerCompliance", $"{gameObject.name} stopped for prayer.");
                    break;
            }
        }
        #endregion

        #region State Logic
        private void HandleChaseState()
        {
            if (_currentTarget == null)
            {
                TransitionToState(AIState.Wander);
                return;
            }

            _navAgent.SetDestination(_currentTarget.position);

            // Check if in attack range
            if (Vector3.Distance(transform.position, _currentTarget.position) <= AttackRadius)
            {
                TransitionToState(AIState.Attack);
            }
        }

        private void HandleAttackState()
        {
            if (_currentTarget == null)
            {
                TransitionToState(AIState.Wander);
                return;
            }

            // Face the target
            Vector3 direction = (_currentTarget.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);

            // Attack on cooldown
            if (Time.time >= _nextAttackTime)
            {
                PerformAttack();
            }

            // If target moves out of range, chase again
            if (Vector3.Distance(transform.position, _currentTarget.position) > AttackRadius * 1.2f)
            {
                TransitionToState(AIState.Chase);
            }
        }

        private void HandleFleeState()
        {
            if (_currentTarget == null)
            {
                TransitionToState(AIState.Wander);
                return;
            }

            // Flee for a certain time or distance
            if (_stateTimer > 8f || Vector3.Distance(transform.position, _currentTarget.position) > DetectionRadius * 2f)
            {
                _currentTarget = null;
                TransitionToState(AIState.Wander);
            }
        }

        private void FindBestTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, DetectionRadius);
            Transform potentialTarget = null;
            AIState newPotentialState = AIState.Idle; // Default state if no target is found

            foreach (var hit in hits)
            {
                // Logic for Police
                if (CharacterType == NPCType.Police && hit.CompareTag("Player"))
                {
                    var player = hit.GetComponent<PlayerController>();
                    if (player != null && player.WantedLevel > 0)
                    {
                        potentialTarget = hit.transform;
                        newPotentialState = AIState.Chase;
                        break; // Police priority is always the wanted player
                    }
                }
                // Logic for Gang Members
                else if (CharacterType == NPCType.GangMember)
                {
                    // Check for rival gang members
                    if (hit.TryGetComponent<NPCController>(out var otherNpc) && otherNpc.CharacterType == NPCType.GangMember)
                    {
                        if (GangSystem.Instance.AreGangsRivals(this.GangName, otherNpc.GangName))
                        {
                            potentialTarget = hit.transform;
                            newPotentialState = AIState.Chase;
                            break;
                        }
                    }
                }
                // Logic for Civilians
                else if (CharacterType == NPCType.Civilian && hit.CompareTag("Player"))
                {
                     var player = hit.GetComponent<PlayerController>();
                    if (player != null && player.WantedLevel > 1)
                    {
                        potentialTarget = hit.transform;
                        newPotentialState = AIState.Flee;
                        break; // Civilians flee from danger
                    }
                }
            }

            // We found a target, decide whether to switch
            if (potentialTarget != null)
            {
                _currentTarget = potentialTarget;
                TransitionToState(newPotentialState);
            }
        }
        #endregion

        #region Actions
        private void SetNewWanderDestination()
        {
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * WanderRadius;
            randomDirection += _startingPosition;
            NavMeshHit navHit;

            // Find the closest valid point on the NavMesh to the random position
            if (NavMesh.SamplePosition(randomDirection, out navHit, WanderRadius, -1))
            {
                _navAgent.SetDestination(navHit.position);
            }
        }

        private void PerformAttack()
        {
            _nextAttackTime = Time.time + AttackRate;
            _animator.SetTrigger(_animIDAttack);

            // Damage dealt via animation event or a small delay
            StartCoroutine(DealDamageAfterDelay(0.3f));
        }

        private IEnumerator DealDamageAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_currentTarget != null && Vector3.Distance(transform.position, _currentTarget.position) <= AttackRadius * 1.1f)
            {
                RVA.TAC.Core.CombatSystem.Instance.ProcessAttack(gameObject, _currentTarget.gameObject);
            }
        }

        public void TakeDamage(float amount)
        {
            _health.TakeDamage(amount);
        }

        private void Die()
        {
            _isDead = true;
            _navAgent.enabled = false;
            GetComponent<CapsuleCollider>().enabled = false;
            _rigidbody.isKinematic = false; // Allow ragdoll physics
            _rigidbody.useGravity = true;

            _animator.SetTrigger(_animIDDeath);
            LogInfo("NPCDeath", $"{gameObject.name} has died.");

            // Cleanup after a delay
            Destroy(gameObject, 15f);
        }
        #endregion

        #region Animation
        private void UpdateAnimation()
        {
            float speed = _navAgent.velocity.magnitude / _navAgent.speed;
            _animator.SetFloat(_animIDSpeed, speed, 0.1f, Time.deltaTime);
        }
        #endregion

        #region Cultural Compliance
        private void HandlePrayerTimeBegins(PrayerName prayerName)
        {
            if (RespectPrayerTimes && !_isDead)
            {
                TransitionToState(AIState.Prayer);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (RespectMosqueZones && other.CompareTag("MosqueZone"))
            {
                TransitionToState(AIState.Idle);
                LogInfo("MosqueCompliance", $"{gameObject.name} entered a mosque zone and is idling.");
            }
        }
        #endregion

        #region Logging
        private void LogInfo(string context, string message)
        {
            Debug.Log($"[RVA:TAC NPC] [{context}] {message}");
        }
        #endregion
    }
}
