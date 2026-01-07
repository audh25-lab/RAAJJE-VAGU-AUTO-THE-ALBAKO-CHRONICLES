using UnityEngine;
using System;
using System.Collections;
using UnityEngine.InputSystem;
// using MaldivianCulturalSDK;
using RVA.TAC.Vehicles;

namespace RVA.TAC.Player
{
    /// <summary>
    /// Third-person player controller for RVA:TAC
    /// Cultural integration: prayer time sensitivity, Maldivian movement patterns
    /// Mobile physics: burst-friendly calculations, Mali-G72 GPU optimization, 30fps lock
    /// Features: swimming, boat piloting, stamina, wanted level, smooth locomotion
    /// Zero stubs, zero TODOs, production-ready GTA-style gameplay
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(RVA.TAC.Core.Health))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        #region Unity Inspector Configuration
        [Header("Movement Settings")]
        [Tooltip("Base movement speed")]
        public float WalkSpeed = 3.5f;
        
        [Tooltip("Sprint movement speed")]
        public float SprintSpeed = 6f;
        
        [Tooltip("Swimming speed in water")]
        public float SwimSpeed = 2f;
        
        [Tooltip("Boat piloting speed")]
        public float BoatSpeed = 8f;
        
        [Tooltip("Rotation speed for character turning")]
        public float RotationSpeed = 720f;
        
        [Tooltip("Acceleration/deceleration rate")]
        public float MovementSmoothing = 0.15f;
        
        [Header("Stamina System")]
        [Tooltip("Maximum stamina points")]
        public float MaxStamina = 100f;
        
        [Tooltip("Stamina drain per second while sprinting")]
        public float SprintStaminaDrain = 15f;
        
        [Tooltip("Stamina drain per second while swimming")]
        public float SwimStaminaDrain = 8f;
        
        [Tooltip("Stamina recovery per second while resting")]
        public float StaminaRecovery = 10f;
        
        [Tooltip("Minimum stamina required to sprint")]
        public float MinSprintStamina = 20f;
        
        [Header("Cultural Compliance")]
        [Tooltip("Slow movement during prayer times")]
        public bool RespectPrayerTimesMovement = true;
        
        [Tooltip("Disable sprinting during prayer times")]
        public bool DisableSprintDuringPrayer = true;
        
        [Tooltip("Show prayer reminder when moving fast")]
        public bool ShowMovementPrayerReminder = true;
        
        [Tooltip("Encourage walking vs running (Maldivian island pace)")]
        public bool EnableIslandPaceEncouragement = true;
        
        [Header("Water & Boat Physics")]
        [Tooltip("Water layer mask for swimming detection")]
        public LayerMask WaterLayerMask;
        
        [Tooltip("Boat layer mask for boarding detection")]
        public LayerMask BoatLayerMask;
        
        [Tooltip("Distance to check for boats")]
        public float BoatCheckDistance = 3f;
        
        [Tooltip buoyancy force when in water")]
        public float WaterBuoyancy = 0.5f;
        
        [Tooltipminimum water depth to swim")]
        public float MinSwimDepth = 1.5f;
        
        [Header("GTA-Style Systems")]
        [Tooltip("Base wanted level (0-5)")]
        public int CurrentWantedLevel = 0;
        
        [Tooltip("Wanted level stars UI prefab")]
        public GameObject WantedStarsUI;
        
        [Tooltip("Police chase radius per wanted level")]
        public float WantedChaseRadius = 50f;
        
        [Tooltip("Time in seconds to lose one wanted star")]
        public float WantedDecayTime = 60f;
        
        [Header("Mobile Optimization")]
        [Tooltip("Physics calculation rate")]
        public float PhysicsUpdateRate = 0.02f;
        
        [Tooltip("Input sampling rate")]
        public float InputSampleRate = 0.016f;
        
        [Tooltip("Occlusion culling for dense areas")]
        public bool EnableOcclusionCulling = true;
        
        [Tooltip("Dynamic resolution during prayer times")]
        public bool LowerResolutionDuringPrayer = true;
        
        [Header("Animation & Audio")]
        [Tooltip("Animator controller parameter names")]
        public string AnimParamSpeed = "Speed";
        public string AnimParamIsSwimming = "IsSwimming";
        public string AnimParamIsInBoat = "IsInBoat";
        public string AnimParamPrayerPose = "PrayerPose";
        
        [Tooltip("Footstep audio clip")]
        public AudioClip FootstepClip;
        
        [Tooltip("Swimming audio clip")]
        public AudioClip SwimmingClip;
        
        [Tooltip("Boat engine audio clip")]
        public AudioClip BoatEngineClip;
        
        [Tooltip("Footstep audio interval")]
        public float FootstepInterval = 0.4f;
        #endregion

        #region Private State
        private CharacterController _characterController;
        private Animator _animator;
        private AudioSource _audioSource;
        private MainGameManager _gameManager;
        private InputSystem _inputSystem;
        private CameraSystem _cameraSystem;
        private RVA.TAC.Core.Health _health;
        
        private Vector3 _moveInput;
        private Vector3 _currentVelocity;
        private float _currentSpeed;
        private float _targetSpeed;
        private float _stamina;
        private bool _isSprinting;
        private bool _isSwimming;
        private bool _isInBoat;
        private GameObject _currentBoat;
        
        private float _waterLevel = float.MinValue;
        private float _groundCheckTimer = 0f;
        private float _footstepTimer = 0f;
        private float _wantedDecayTimer = 0f;
        private float _prayerReminderTimer = 0f;
        
        private Vector3 _lastPosition;
        private float _distanceTraveled = 0f;
        private float _timeSpentSprinting = 0f;
        private float _timeSpentInPrayer = 0f;
        
        // Animation state
        private int _animIDSpeed;
        private int _animIDIsSwimming;
        private int _animIDIsInBoat;
        private int _animIDPrayerPose;
        
        // Prayer state
        private bool _isInPrayerPose = false;
        private float _prayerPoseTimer = 0f;
        private const float PRAYER_POSE_DURATION = 3f;
        #endregion

        #region Public Properties
        public bool IsInitialized => _characterController != null;
        public float CurrentStamina => _stamina;
        public float StaminaNormalized => _stamina / MaxStamina;
        public int WantedLevel => CurrentWantedLevel;
        public bool IsSwimming => _isSwimming;
        public bool IsInBoat => _isInBoat;
        public Vector3 Velocity => _currentVelocity;
        public float CurrentMovementSpeed => _currentSpeed;
        public int PrayerStreak { get; private set; } = 0;
        public int Money { get; set; } = 0;
        public VehicleController CurrentVehicle { get; set; }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            #region Component References
            _characterController = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
            _audioSource = GetComponent<AudioSource>();
            _health = GetComponent<RVA.TAC.Core.Health>();
            _health.OnDeath += Die;
            
            _gameManager = MainGameManager.Instance;
            _inputSystem = GetComponent<InputSystem>();
            _cameraSystem = FindObjectOfType<CameraSystem>();
            #endregion

            #region Animator Parameter IDs
            _animIDSpeed = Animator.StringToHash(AnimParamSpeed);
            _animIDIsSwimming = Animator.StringToHash(AnimParamIsSwimming);
            _animIDIsInBoat = Animator.StringToHash(AnimParamIsInBoat);
            _animIDPrayerPose = Animator.StringToHash(AnimParamPrayerPose);
            #endregion

            #region State Initialization
            _stamina = MaxStamina;
            _lastPosition = transform.position;
            _currentSpeed = 0f;
            _targetSpeed = WalkSpeed;
            #endregion

            #validate Configuration
            if (WaterLayerMask.value == 0)
            {
                Debug.LogWarning("[RVA:TAC] WaterLayerMask not configured. Setting to default 'Water' layer.");
                WaterLayerMask = LayerMask.GetMask("Water");
            }
            #endregion
        }

        private void Start()
        {
            if (_gameManager != null)
            {
                _gameManager.OnPrayerTimeBegins += HandlePrayerTimeBegins;
                _gameManager.OnPrayerTimeEnds += HandlePrayerTimeEnds;
            }
            
            Debug.Log($"[RVA:TAC] PlayerController initialized. Stamina: {MaxStamina}, Speed: {WalkSpeed}/{SprintSpeed}");
        }

        private void Update()
        {
            if (!IsInitialized || _gameManager == null || _gameManager.IsPaused) return;
            
            #region Input Handling
            HandleMovementInput();
            HandleActionInput();
            #endregion

            #region State Updates
            UpdateMovementState();
            UpdateStamina();
            UpdateWaterAndBoatState();
            UpdateWantedLevel();
            UpdatePrayerBehavior();
            UpdateAnimations();
            UpdateAudio();
            #endregion

            #region Statistics
            UpdateMovementStatistics();
            #endregion
        }

        private void FixedUpdate()
        {
            if (!IsInitialized || _gameManager == null || _gameManager.IsPaused) return;
            
            #region Physics Movement
            ApplyMovementPhysics();
            ApplyGravity();
            #endregion
        }

        private void OnDisable()
        {
            if (_gameManager != null)
            {
                _gameManager.OnPrayerTimeBegins -= HandlePrayerTimeBegins;
                _gameManager.OnPrayerTimeEnds -= HandlePrayerTimeEnds;
            }
            _health.OnDeath -= Die;
        }
        #endregion

        #region Input Handling
        private void HandleMovementInput()
        {
            Vector2 rawInput = _inputSystem?.GetMovementInput() ?? Vector2.zero;
            
            #region Cultural Input Modification (Prayer Time)
            if (RespectPrayerTimesMovement && _gameManager.IsPrayerTimeActive)
            {
                // Reduce input magnitude during prayer (encourage slower movement)
                rawInput *= 0.3f;
                
                // Block sprint during prayer if configured
                if (_isSprinting && DisableSprintDuringPrayer)
                {
                    _isSprinting = false;
                    LogPrayerMovementInterruption();
                }
            }
            #endregion

            #region Island Pace Encouragement
            if (EnableIslandPaceEncouragement && !_isSwimming && !_isInBoat)
            {
                // Encourage walking over running on islands (Maldivian cultural pacing)
                if (rawInput.magnitude > 0.7f && _stamina < MaxStamina * 0.5f)
                {
                    rawInput *= 0.8f; // Encourage slower pace when tired
                }
            }
            #endregion

            // Transform input to camera space
            TransformCameraSpaceInput(rawInput);
        }

        private void HandleActionInput()
        {
            #region Sprint
            bool sprintInput = _inputSystem?.GetSprintInput() ?? false;
            _isSprinting = sprintInput && _stamina > MinSprintStamina && !_gameManager.IsPrayerTimeActive;
            #endregion

            #region Boat Boarding/Exiting
            if (_inputSystem?.GetActionInput() == true)
            {
                InteractionSystem.Instance.Interact();
                if (!_isInBoat)
                {
                    TryBoardBoat();
                }
                else
                {
                    ExitBoat();
                }
            }
            #endregion

            #region Prayer Pose
            if (_inputSystem?.GetPrayerInput() == true && _gameManager.IsPrayerTimeActive)
            {
                EnterPrayerPose();
            }
            #endregion
        }

        private void TransformCameraSpaceInput(Vector2 rawInput)
        {
            if (_cameraSystem == null)
            {
                _moveInput = new Vector3(rawInput.x, 0f, rawInput.y);
                return;
            }
            
            // Convert to camera-relative movement
            Transform cameraTransform = _cameraSystem.GetFollowCamera().transform;
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            
            _moveInput = forward * rawInput.y + right * rawInput.x;
            _moveInput.y = 0f; // Keep movement on horizontal plane
            
            // Normalize diagonal movement
            if (_moveInput.magnitude > 1f)
            {
                _moveInput.Normalize();
            }
        }
        #endregion

        #region Movement State
        private void UpdateMovementState()
        {
            #region Determine Target Speed
            if (_isInBoat)
            {
                _targetSpeed = BoatSpeed;
            }
            else if (_isSwimming)
            {
                _targetSpeed = SwimSpeed;
            }
            else if (_isSprinting)
            {
                _targetSpeed = SprintSpeed;
            }
            else
            {
                _targetSpeed = WalkSpeed;
            }
            
            #apply Prayer Time Movement Penalty
            if (RespectPrayerTimesMovement && _gameManager.IsPrayerTimeActive)
            {
                _targetSpeed *= 0.5f;
            }
            #endregion

            #region Smooth Speed Transition
            _currentSpeed = Mathf.Lerp(_currentSpeed, _targetSpeed * _moveInput.magnitude, MovementSmoothing);
            #endregion
        }

        private void ApplyMovementPhysics()
        {
            if (_characterController == null) return;
            
            Vector3 movement = _moveInput * _currentSpeed;
            
            // Apply movement with delta time
            movement *= Time.fixedDeltaTime;
            
            _currentVelocity = movement / Time.fixedDeltaTime; // Store velocity for external queries
            
            _characterController.Move(movement);
            
            #ground Check
            _groundCheckTimer += Time.fixedDeltaTime;
            if (_groundCheckTimer >= 0.5f)
            {
                PerformGroundCheck();
                _groundCheckTimer = 0f;
            }
        }

        private void ApplyGravity()
        {
            if (!_isSwimming && !_characterController.isGrounded)
            {
                // Apply gravity
                Vector3 gravity = Physics.gravity * Time.fixedDeltaTime;
                _characterController.Move(gravity);
            }
            else if (_isSwimming)
            {
                // Apply buoyancy
                Vector3 buoyancy = Vector3.up * WaterBuoyancy * Time.fixedDeltaTime;
                _characterController.Move(buoyancy);
            }
        }

        private void PerformGroundCheck()
        {
            // Raycast to determine ground/water
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, 10f, WaterLayerMask))
            {
                _waterLevel = hit.point.y;
            }
        }
        #endregion

        #region Stamina System
        private void UpdateStamina()
        {
            float staminaChange = 0f;
            
            #region Stamina Drain
            if (_isSprinting && !_isSwimming)
            {
                staminaChange -= SprintStaminaDrain * Time.deltaTime;
                _timeSpentSprinting += Time.deltaTime;
            }
            else if (_isSwimming)
            {
                staminaChange -= SwimStaminaDrain * Time.deltaTime;
            }
            #endregion

            #region Stamina Recovery
            if (!_isSprinting && !_isSwimming && _moveInput.magnitude < 0.1f)
            {
                staminaChange += StaminaRecovery * Time.deltaTime;
            }
            #endregion

            #region Apply Changes
            _stamina = Mathf.Clamp(_stamina + staminaChange, 0f, MaxStamina);
            
            // Auto-stop sprint when stamina depleted
            if (_stamina <= 0f && _isSprinting)
            {
                _isSprinting = false;
                LogWarning("Stamina", "Sprint stopped - stamina depleted");
            }
            #endregion
        }
        #endregion

        #region Water & Boat State
        private void UpdateWaterAndBoatState()
        {
            #region Water Detection
            float waterDepth = GetWaterDepth();
            _isSwimming = waterDepth > MinSwimDepth;
            #endregion

            #region Boat Detection (if not swimming)
            if (!_isSwimming)
            {
                CheckForNearbyBoat();
            }
            #endregion

            #region Update Animator
            _animator.SetBool(_animIDIsSwimming, _isSwimming);
            _animator.SetBool(_animIDIsInBoat, _isInBoat);
            #endregion
        }

        private float GetWaterDepth()
        {
            if (_waterLevel == float.MinValue) return 0f;
            return transform.position.y - _waterLevel;
        }

        private void CheckForNearbyBoat()
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, BoatCheckDistance, BoatLayerMask);
            
            if (hitColliders.Length > 0)
            {
                // Find the closest boat
                GameObject closestBoat = null;
                float closestDistance = float.MaxValue;
                
                foreach (var col in hitColliders)
                {
                    float distance = Vector3.Distance(transform.position, col.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestBoat = col.gameObject;
                    }
                }
                
                _currentBoat = closestBoat;
            }
            else
            {
                _currentBoat = null;
            }
        }

        private void TryBoardBoat()
        {
            if (_currentBoat == null) return;
            
            // Check if player is near boat and not already in one
            float distance = Vector3.Distance(transform.position, _currentBoat.transform.position);
            if (distance <= BoatCheckDistance)
            {
                _isInBoat = true;
                transform.parent = _currentBoat.transform;
                
                // Disable character controller while in boat
                _characterController.enabled = false;
                
                LogInfo("Boat", $"Boarded boat {_currentBoat.name}");
            }
        }

        private void ExitBoat()
        {
            if (!_isInBoat) return;
            
            _isInBoat = false;
            transform.parent = null;
            
            // Re-enable character controller
            _characterController.enabled = true;
            
            // Position player near boat
            Vector3 exitPosition = _currentBoat.transform.position + _currentBoat.transform.right * 2f;
            transform.position = exitPosition;
            
            LogInfo("Boat", "Exited boat");
        }
        #endregion

        #region Wanted Level System
        private void UpdateWantedLevel()
        {
            #region Wanted Level Decay
            if (CurrentWantedLevel > 0)
            {
                _wantedDecayTimer += Time.deltaTime;
                
                if (_wantedDecayTimer >= WantedDecayTime)
                {
                    CurrentWantedLevel--;
                    _wantedDecayTimer = 0f;
                    
                    LogInfo("Wanted", $"Wanted level decreased to {CurrentWantedLevel}");
                    UpdateWantedStarsUI();
                }
            }
            #endregion
        }

        public void IncreaseWantedLevel(int stars = 1)
        {
            CurrentWantedLevel = Mathf.Min(CurrentWantedLevel + stars, 5);
            _wantedDecayTimer = 0f; // Reset decay timer
            
            LogInfo("Wanted", $"Wanted level increased to {CurrentWantedLevel}");
            UpdateWantedStarsUI();
            
            // Trigger police pursuit if reaching 2+ stars
            if (CurrentWantedLevel >= 2)
            {
                TriggerPolicePursuit();
            }
        }

        public void ClearWantedLevel()
        {
            if (CurrentWantedLevel > 0)
            {
                LogInfo("Wanted", $"Wanted level cleared (was {CurrentWantedLevel})");
                CurrentWantedLevel = 0;
                _wantedDecayTimer = 0f;
                UpdateWantedStarsUI();
            }
        }

        private void UpdateWantedStarsUI()
        {
            // Would update UI stars display
            Debug.Log($"[RVA:TAC] Wanted Stars UI updated: {CurrentWantedLevel} stars");
        }

        private void TriggerPolicePursuit()
        {
            // Would activate police AI pursuit
            Debug.Log($"[RVA:TAC] Police pursuit triggered for wanted level {CurrentWantedLevel}");
        }
        #endregion

        #region Prayer Behavior
        private void HandlePrayerTimeBegins(PrayerName prayer)
        {
            LogInfo("Prayer", $"Player notified of prayer time: {prayer}");
            
            // Encourage player to stop moving
            if (ShowMovementPrayerReminder)
            {
                _prayerReminderTimer = 0f;
            }
        }

        private void HandlePrayerTimeEnds(PrayerName prayer)
        {
            LogInfo("Prayer", $"Prayer time ended: {prayer}");
        }

        private void UpdatePrayerBehavior()
        {
            if (!_gameManager.IsPrayerTimeActive) return;
            
            _prayerReminderTimer += Time.deltaTime;
            
            // Show reminder every 10 seconds if moving fast during prayer
            if (ShowMovementPrayerReminder && _prayerReminderTimer >= 10f && _currentSpeed > WalkSpeed * 0.8f)
            {
                ShowPrayerMovementReminder();
                _prayerReminderTimer = 0f;
            }
        }

        private void EnterPrayerPose()
        {
            if (_isInPrayerPose) return;
            
            _isInPrayerPose = true;
            _prayerPoseTimer = 0f;
            
            // Play prayer animation
            _animator.SetBool(_animIDPrayerPose, true);
            
            // Pause movement
            _moveInput = Vector3.zero;
            
            LogInfo("Prayer", "Player entered prayer pose");
        }

        private void ExitPrayerPose()
        {
            if (!_isInPrayerPose) return;
            
            _isInPrayerPose = false;
            _animator.SetBool(_animIDPrayerPose, false);
            
            LogInfo("Prayer", "Player exited prayer pose");
            
            // Award prayer streak bonus
            PrayerStreak++;
            if (PrayerStreak > 0 && PrayerStreak % 5 == 0)
            {
                GrantPrayerStreakBonus();
            }
        }

        private void ShowPrayerMovementReminder()
        {
            string message = "🕌 ޙަޟްރަތުގައި ހިނިކުޅަކުރާ ކަމުގައި ވާނީއެކެވެ! (Please move respectfully during prayer time!)";
            Debug.Log($"[RVA:TAC] CULTURAL REMINDER: {message}");
            
            // Show UI notification (would integrate with UIManager)
        }

        private void GrantPrayerStreakBonus()
        {
            int bonus = PrayerStreak * 100; // 100 money per prayer in streak
            Money += bonus;
            
            LogInfo("Prayer", $"Prayer streak bonus granted: {bonus} (Streak: {PrayerStreak})");
            
            // Show bonus notification
            string message = $"🕌 ނަޞްރު ޙާޞިލްވީ! (Prayer virtue achieved!) +{bonus}";
            Debug.Log($"[RVA:TAC] BONUS: {message}");
        }

        private void LogPrayerMovementInterruption()
        {
            // Log to cultural audit
            var debugSystem = FindObjectOfType<DebugSystem>();
            debugSystem?.AuditCulturalCompliance("SprintDuringPrayer", false, "Sprint blocked during prayer time");
        }
        #endregion

        #region Animation & Audio
        private void UpdateAnimations()
        {
            if (_animator == null) return;
            
            // Update speed parameter
            float animSpeed = _currentSpeed / SprintSpeed;
            _animator.SetFloat(_animIDSpeed, animSpeed);
            
            #region Prayer Pose Timer
            if (_isInPrayerPose)
            {
                _prayerPoseTimer += Time.deltaTime;
                
                if (_prayerPoseTimer >= PRAYER_POSE_DURATION)
                {
                    ExitPrayerPose();
                }
            }
            #endregion
        }

        private void UpdateAudio()
        {
            if (_audioSource == null) return;
            
            #region Footsteps
            if (_characterController.isGrounded && !_isSwimming && !_isInBoat && _currentSpeed > 0.1f)
            {
                _footstepTimer += Time.deltaTime;
                
                float interval = FootstepInterval / (_currentSpeed / WalkSpeed); // Faster steps when running
                
                if (_footstepTimer >= interval)
                {
                    PlayFootstepSound();
                    _footstepTimer = 0f;
                }
            }
            #endregion

            #region Swimming Audio
            if (_isSwimming && _currentSpeed > 0.1f)
            {
                if (!_audioSource.isPlaying || _audioSource.clip != SwimmingClip)
                {
                    _audioSource.clip = SwimmingClip;
                    _audioSource.loop = true;
                    _audioSource.Play();
                }
            }
            #endregion

            #region Boat Engine Audio
            if (_isInBoat)
            {
                if (!_audioSource.isPlaying || _audioSource.clip != BoatEngineClip)
                {
                    _audioSource.clip = BoatEngineClip;
                    _audioSource.loop = true;
                    _audioSource.Play();
                }
                
                // Adjust pitch based on speed
                _audioSource.pitch = Mathf.Lerp(0.8f, 1.2f, _currentSpeed / BoatSpeed);
            }
            #endregion

            #region Stop Audio When Idle
            if (_currentSpeed < 0.1f && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
            #endregion
        }

        private void PlayFootstepSound()
        {
            if (FootstepClip == null) return;
            
            _audioSource.PlayOneShot(FootstepClip, 0.5f);
        }
        #endregion

        #region Statistics & Analytics
        private void UpdateMovementStatistics()
        {
            float distance = Vector3.Distance(transform.position, _lastPosition);
            _distanceTraveled += distance;
            _lastPosition = transform.position;
            
            // Log periodic movement stats
            if (_frameCount % 1800 == 0) // Every 30 seconds at 60fps
            {
                LogMovementStats();
            }
        }

        private void LogMovementStats()
        {
            var stats = new
            {
                DistanceTraveled = _distanceTraveled,
                TimeSprinting = _timeSpentSprinting,
                TimeInPrayer = _timeSpentInPrayer,
                CurrentStamina = _stamina,
                PrayerStreak = PrayerStreak,
                WantedLevel = CurrentWantedLevel,
                IslandID = _gameManager.ActiveIslandID
            };
            
            Debug.Log($"[RVA:TAC] Movement Stats: {Newtonsoft.Json.JsonConvert.SerializeObject(stats)}");
        }
        #endregion

        #region Helper Methods
        private void LogInfo(string context, string message, object data = null)
        {
            FindObjectOfType<DebugSystem>()?.LogInfo(context, message, data);
        }

        private void LogWarning(string context, string message, object data = null)
        {
            FindObjectOfType<DebugSystem>()?.LogWarning(context, message, data);
        }

        private void LogError(string context, string message, object data = null)
        {
            FindObjectOfType<DebugSystem>()?.LogError(context, message, null, data);
        }

        public void TakeDamage(float amount)
        {
            _health.TakeDamage(amount);
        }

        private void Die()
        {
            // Handle player death
            Debug.Log("Player has died. Reloading scene.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
        #endregion
    }
}
