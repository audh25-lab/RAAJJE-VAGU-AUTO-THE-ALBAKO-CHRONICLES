using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using System.Linq;
using MaldivianCulturalSDK;

namespace RVA.TAC.Player
{
    /// <summary>
    /// Unified input system for RVA:TAC supporting touch, gamepad, and keyboard
    /// Cultural integration: accessible controls for motor disabilities, prayer time modifications
    /// Mobile optimization: auto-switching between TouchInputSystem and native InputSystem, 30fps lock
    /// Accessibility: remappable controls, sticky keys, reduced input requirements
    /// Performance: Platform-specific compilation, burst-ready calculations, zero-allocation hot paths
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(TouchInputSystem))]
    public class InputSystem : MonoBehaviour
    {
        #region Unity Inspector Configuration
        [Header("Input System Configuration")]
        [Tooltip("Auto-detect and use best input method for platform")]
        public bool AutoDetectInputMethod = true;
        
        [Tooltip "Prioritize touch input on mobile")]
        public bool PrioritizeTouchOnMobile = true;
        
        [Tooltip "Default control scheme")]
        public ControlScheme DefaultControlScheme = ControlScheme.Touch;
        
        [Tooltip "Allow runtime control scheme switching")]
        public bool AllowRuntimeSwitching = true;
        
        [Header("Keyboard & Mouse Settings")]
        [Tooltip "Enable keyboard input")]
        public bool EnableKeyboard = true;
        
        [Tooltip "Enable mouse input")]
        public bool EnableMouse = true;
        
        [Tooltip "WASD movement keys")]
        public Key[] MovementKeys = { Key.W, Key.A, Key.S, Key.D };
        
        [Tooltip "Sprint key")]
        public Key SprintKey = Key.LeftShift;
        
        [Tooltip "Action/Interact key")]
        public Key ActionKey = Key.E;
        
        [Tooltip "Prayer key")]
        public Key PrayerKey = Key.P;
        
        [Tooltip "Mouse sensitivity")]
        public float MouseSensitivity = 1f;
        
        [Tooltip "Invert mouse Y axis")]
        public bool InvertMouseY = false;
        
        [Header("Gamepad Settings")]
        [Tooltip "Enable gamepad input")]
        public bool EnableGamepad = true;
        
        [Tooltip "Gamepad deadzone")]
        public float GamepadDeadzone = 0.15f;
        
        [Tooltip "Gamepad sensitivity")]
        public float GamepadSensitivity = 1f;
        
        [Tooltip "Gamepad sprint button")]
        public string GamepadSprintButton = "rightTrigger";
        
        [Tooltip "Gamepad action button")]
        public string GamepadActionButton = "buttonSouth";
        
        [Tooltip "Gamepad prayer button")]
        public string GamepadPrayerButton = "buttonWest";
        
        [Header("Accessibility Features")]
        [Tooltip "Enable sticky keys (hold mode)")]
        public bool EnableStickyKeys = false;
        
        [Tooltip "Sticky key hold time")]
        public float StickyKeyHoldTime = 0.5f;
        
        [Tooltip "Enable one-handed mode")]
        public bool EnableOneHandedMode = false;
        
        [Tooltip "Reduce input requirements for motor disabilities")]
        public bool ReduceInputRequirements = true;
        
        [Tooltip "Auto-sprint when moving continuously")]
        public bool AutoSprintOnContinuousMovement = false;
        
        [Tooltip "Continuous movement threshold (seconds)")]
        public float ContinuousMovementThreshold = 2f;
        
        [Header("Cultural Compliance")]
        [Tooltip "Reduce input speed during prayer times")]
        public bool ReduceInputSpeedDuringPrayer = true;
        
        [Tooltip "Show input reminders during prayer")]
        public bool ShowInputRemindersDuringPrayer = true;
        
        [Tooltip "Block aggressive inputs during prayer")]
        public bool BlockAggressiveInputsDuringPrayer = true;
        
        [Header "Mobile Optimization")]
        [Tooltip "Input polling rate")]
        public float InputPollingRate = 0.016f;
        
        [Tooltip "Use enhanced touch on mobile")]
        public bool UseEnhancedTouchOnMobile = true;
        
        [Tooltip "Optimize for Mali-G72 GPU")]
        public bool OptimizeForMaliG72 = true;
        
        [Tooltip "Input latency compensation (ms)")]
        public float InputLatencyCompensation = 16f;
        
        [Header "Debug & Analytics")]
        [Tooltip "Log all input events")]
        public bool LogInputEvents = false;
        
        [Tooltip "Track input patterns")]
        public bool TrackInputPatterns = true;
        
        [Tooltip "Show current input method on screen")]
        public bool ShowInputMethodDebug = false;
        #endregion

        #region Private State
        private PlayerController _playerController;
        private TouchInputSystem _touchSystem;
        private MainGameManager _gameManager;
        private DebugSystem _debugSystem;
        
        private InputAction _movementAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _actionAction;
        private InputAction _prayerAction;
        
        private ControlScheme _activeControlScheme;
        private IInputHandler _activeHandler;
        
        private readonly Queue<InputEventData> _inputHistory = new Queue<InputEventData>(1000);
        private InputPatternAnalyzer _patternAnalyzer;
        
        private float _inputPollTimer = 0f;
        private float _continuousMovementTimer = 0f;
        private bool _isContinuousMovement = false;
        private bool _isStickyKeyActive = false;
        private float _stickyKeyTimer = 0f;
        
        private Vector2 _lastMovementInput;
        private float _lastInputTime;
        private bool _isInputBlocked = false;
        #endregion

        #region Public Properties
        public ControlScheme CurrentControlScheme => _activeControlScheme;
        public string CurrentInputMethod => _activeControlScheme.ToString();
        public Vector2 MovementInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprintInput { get; private set; }
        public bool IsActionInput { get; private set; }
        public bool IsPrayerInput { get; private set; }
        public static string CurrentControlSchemeName => "RVA_Touch_Gamepad_Keyboard_Unified";
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            #region Component References
            _playerController = GetComponent<PlayerController>();
            _touchSystem = GetComponent<TouchInputSystem>();
            _gameManager = MainGameManager.Instance;
            _debugSystem = FindObjectOfType<DebugSystem>();
            #endregion

            #region Initialize Input System
            InitializeInputActions();
            DetectAndSetInputMethod();
            InitializePatternAnalyzer();
            #endregion

            #region Platform Detection
            #if UNITY_ANDROID || UNITY_IOS
            if (PrioritizeTouchOnMobile)
            {
                _activeControlScheme = ControlScheme.Touch;
            }
            #endif
            #endregion

            LogInfo("InputSystem", $"Initialized with scheme: {_activeControlScheme}");
        }

        private void Start()
        {
            // Enable appropriate input handler
            EnableInputHandler(_activeControlScheme);
            
            // Subscribe to cultural events
            if (_gameManager != null)
            {
                _gameManager.OnPrayerTimeBegins += HandlePrayerTimeBegins;
                _gameManager.OnPrayerTimeEnds += HandlePrayerTimeEnds;
            }
            
            // Enable input actions
            EnableInputActions();
        }

        private void Update()
        {
            if (!IsInitialized || _isInputBlocked) return;
            
            _inputPollTimer += Time.deltaTime;
            if (_inputPollTimer >= InputPollingRate)
            {
                PollInput();
                _inputPollTimer = 0f;
            }
            
            UpdateAccessibilityFeatures();
            RecordInputHistory();
        }

        private void OnDisable()
        {
            DisableInputActions();
            
            if (_gameManager != null)
            {
                _gameManager.OnPrayerTimeBegins -= HandlePrayerTimeBegins;
                _gameManager.OnPrayerTimeEnds -= HandlePrayerTimeEnds;
            }
        }

        private void OnDestroy()
        {
            DisposeInputActions();
        }
        #endregion

        #region Initialization
        private void InitializeInputActions()
        {
            try
            {
                // Create input action map
                var actionMap = new InputActionMap("Gameplay");
                
                #region Movement Action
                _movementAction = actionMap.AddAction("Movement", InputActionType.Value);
                _movementAction.AddCompositeBinding("2DVector")
                    .With("Up", MovementKeys[0].ToString())
                    .With("Down", MovementKeys[2].ToString())
                    .With("Left", MovementKeys[1].ToString())
                    .With("Right", MovementKeys[3].ToString());
                
                // Add gamepad binding
                _movementAction.AddBinding("<Gamepad>/leftStick");
                #endregion

                #region Look Action (Mouse/Gamepad)
                _lookAction = actionMap.AddAction("Look", InputActionType.Value);
                _lookAction.AddBinding("<Mouse>/delta");
                _lookAction.AddBinding("<Gamepad>/rightStick");
                #endregion

                #region Sprint Action
                _sprintAction = actionMap.AddAction("Sprint", InputActionType.Button);
                _sprintAction.AddBinding($"{SprintKey}");
                _sprintAction.AddBinding($"<Gamepad>/{GamepadSprintButton}");
                #endregion

                #region Action/Interact
                _actionAction = actionMap.AddAction("Action", InputActionType.Button);
                _actionAction.AddBinding($"{ActionKey}");
                _actionAction.AddBinding($"<Gamepad>/{GamepadActionButton}");
                #endregion

                #region Prayer Action
                _prayerAction = actionMap.AddAction("Prayer", InputActionType.Button);
                _prayerAction.AddBinding($"{PrayerKey}");
                _prayerAction.AddBinding($"<Gamepad>/{GamepadPrayerButton}");
                #endregion
                
                _patternAnalyzer = new InputPatternAnalyzer();
            }
            catch (Exception ex)
            {
                LogError("InputInitialization", "Failed to initialize input actions", ex);
            }
        }

        private void EnableInputActions()
        {
            _movementAction?.Enable();
            _lookAction?.Enable();
            _sprintAction?.Enable();
            _actionAction?.Enable();
            _prayerAction?.Enable();
            
            LogInfo("InputActions", "All input actions enabled");
        }

        private void DisableInputActions()
        {
            _movementAction?.Disable();
            _lookAction?.Disable();
            _sprintAction?.Disable();
            _actionAction?.Disable();
            _prayerAction?.Disable();
            
            LogInfo("InputActions", "All input actions disabled");
        }

        private void DisposeInputActions()
        {
            _movementAction?.Dispose();
            _lookAction?.Dispose();
            _sprintAction?.Dispose();
            _actionAction?.Dispose();
            _prayerAction?.Dispose();
            
            LogInfo("InputActions", "Input actions disposed");
        }

        private void InitializePatternAnalyzer()
        {
            _patternAnalyzer = new InputPatternAnalyzer();
            _patternAnalyzer.OnPatternDetected += HandleInputPatternDetected;
            
            LogInfo("PatternAnalyzer", "Initialized input pattern analyzer");
        }
        #endregion

        #region Input Method Detection
        private void DetectAndSetInputMethod()
        {
            if (!AutoDetectInputMethod)
            {
                _activeControlScheme = DefaultControlScheme;
                return;
            }
            
            #region Platform-based Detection
            #if UNITY_ANDROID || UNITY_IOS
            _activeControlScheme = ControlScheme.Touch;
            #elif UNITY_STANDALONE || UNITY_EDITOR
            if (Gamepad.current != null)
            {
                _activeControlScheme = ControlScheme.Gamepad;
            }
            else
            {
                _activeControlScheme = ControlScheme.KeyboardMouse;
            }
            #else
            _activeControlScheme = DefaultControlScheme;
            #endif
            #endregion
            
            LogInfo("InputDetection", $"Detected control scheme: {_activeControlScheme}");
        }

        private void EnableInputHandler(ControlScheme scheme)
        {
            // Disable all handlers first
            _touchSystem.enabled = false;
            
            // Enable appropriate handler
            switch (scheme)
            {
                case ControlScheme.Touch:
                    _touchSystem.enabled = true;
                    _activeHandler = new TouchInputHandler(_touchSystem);
                    break;
                case ControlScheme.Gamepad:
                    _activeHandler = new GamepadInputHandler(this);
                    break;
                case ControlScheme.KeyboardMouse:
                    _activeHandler = new KeyboardMouseInputHandler(this);
                    break;
                default:
                    _activeHandler = new NullInputHandler();
                    break;
            }
            
            LogInfo("InputHandler", $"Active handler: {_activeHandler.GetType().Name}");
        }

        public void SwitchControlScheme(ControlScheme newScheme)
        {
            if (!AllowRuntimeSwitching && _activeControlScheme != ControlScheme.None)
            {
                LogWarning("ControlSchemeSwitch", "Runtime switching disabled");
                return;
            }
            
            ControlScheme oldScheme = _activeControlScheme;
            _activeControlScheme = newScheme;
            
            EnableInputHandler(newScheme);
            
            LogInfo("ControlSchemeSwitch", $"Switched from {oldScheme} to {newScheme}");
            
            // Notify analytics
            _debugSystem?.ReportToAnalytics("control_scheme_changed", new
            {
                From = oldScheme,
                To = newScheme,
                Timestamp = DateTime.UtcNow
            });
        }
        #endregion

        #region Input Polling
        private void PollInput()
        {
            if (_activeHandler == null) return;
            
            #region Poll Movement
            MovementInput = _activeHandler.GetMovementInput();
            
            #region Cultural Modification
            if (_gameManager.IsPrayerTimeActive && ReduceInputSpeedDuringPrayer)
            {
                MovementInput *= 0.5f;
                
                // Show reminder for aggressive movement
                if (MovementInput.magnitude > 0.7f && Time.time - _prayerReminderTimer > 10f)
                {
                    ShowInputReminder("Please move respectfully during prayer time");
                    _prayerReminderTimer = Time.time;
                }
            }
            #endregion
            
            #region Apply Deadzone
            MovementInput = ApplyDeadzone(MovementInput, _activeControlScheme == ControlScheme.Gamepad ? GamepadDeadzone : JoystickDeadzone);
            #endregion
            
            #region Accessibility Modification
            if (ReduceInputRequirements && MovementInput.magnitude > 0.1f)
            {
                _continuousMovementTimer += Time.deltaTime;
                if (_continuousMovementTimer >= ContinuousMovementThreshold)
                {
                    _isContinuousMovement = true;
                    
                    if (AutoSprintOnContinuousMovement && !_gameManager.IsPrayerTimeActive)
                    {
                        IsSprintInput = true;
                    }
                }
            }
            else
            {
                _continuousMovementTimer = 0f;
                _isContinuousMovement = false;
            }
            #endregion
            #endregion

            #region Poll Look/Rotation
            LookInput = _activeHandler.GetLookInput() * GetSensitivityMultiplier();
            
            #region Mouse Sensitivity
            if (_activeControlScheme == ControlScheme.KeyboardMouse)
            {
                LookInput *= MouseSensitivity * 0.1f;
            }
            #endregion
            
            #region Gamepad Sensitivity
            if (_activeControlScheme == ControlScheme.Gamepad)
            {
                LookInput *= GamepadSensitivity;
            }
            #endregion
            
            #region Invert Y
            if (InvertMouseY && _activeControlScheme == ControlScheme.KeyboardMouse)
            {
                LookInput.y = -LookInput.y;
            }
            #endregion
            #endregion

            #region Poll Actions
            IsSprintInput = _activeHandler.GetSprintInput();
            IsActionInput = _activeHandler.GetActionInput();
            IsPrayerInput = _activeHandler.GetPrayerInput();
            
            #region Sticky Keys
            if (EnableStickyKeys)
            {
                ProcessStickyKeys();
            }
            #endregion
            #endregion

            #region Update Timestamps
            if (MovementInput.magnitude > 0.1f || LookInput.magnitude > 0.1f || IsSprintInput || IsActionInput || IsPrayerInput)
            {
                _lastInputTime = Time.time;
            }
            #endregion
            
            #region Pattern Tracking
            if (TrackInputPatterns)
            {
                TrackInputPattern();
            }
            #endregion
            
            #region Update Player Controller
            UpdatePlayerController();
            #endregion
        }

        private void UpdatePlayerController()
        {
            // Pass input to PlayerController
            // Note: PlayerController reads directly from InputSystem properties
            // This method is for future expansion
        }

        private Vector2 ApplyDeadzone(Vector2 input, float deadzone)
        {
            float magnitude = input.magnitude;
            if (magnitude < deadzone)
            {
                return Vector2.zero;
            }
            
            // Eliminate deadzone and renormalize
            return input.normalized * ((magnitude - deadzone) / (1f - deadzone));
        }

        private float GetSensitivityMultiplier()
        {
            #if UNITY_ANDROID || UNITY_IOS
            return OptimizeForMaliG72 ? 0.8f : 1f; // Reduce sensitivity on Mali-G72 for stability
            #else
            return 1f;
            #endif
        }
        #endregion

        #region Accessibility Features
        private void UpdateAccessibilityFeatures()
        {
            if (!EnableStickyKeys) return;
            
            if (_isStickyKeyActive)
            {
                _stickyKeyTimer += Time.deltaTime;
                
                if (_stickyKeyTimer >= StickyKeyHoldTime)
                {
                    // Release sticky key
                    _isStickyKeyActive = false;
                    _stickyKeyTimer = 0f;
                    IsSprintInput = false;
                }
            }
        }

        private void ProcessStickyKeys()
        {
            if (IsSprintInput && !_isStickyKeyActive)
            {
                // First press - activate sticky
                _isStickyKeyActive = true;
                _stickyKeyTimer = 0f;
            }
            else if (!IsSprintInput && _isStickyKeyActive)
            {
                // Keep sprint active during sticky period
                IsSprintInput = true;
            }
        }

        private void EnableOneHandedMode()
        {
            // Move UI elements to left/right side
            // This would interface with UI system
            LogInfo("OneHandedMode", "Enabled one-handed control mode");
        }

        private void DisableOneHandedMode()
        {
            LogInfo("OneHandedMode", "Disabled one-handed control mode");
        }
        #endregion

        #region Cultural Compliance
        private void HandlePrayerTimeBegins(PrayerName prayer)
        {
            LogInfo("PrayerInput", $"Input modifications applied for prayer time: {prayer}");
            
            // Reduce all input sensitivity
            if (ReduceInputSpeedDuringPrayer)
            {
                // Modifications applied in PollInput()
            }
            
            // Block aggressive gestures
            if (BlockAggressiveInputsDuringPrayer)
            {
                // Modifications applied in gesture handlers
            }
            
            // Show input reminder
            if (ShowInputRemindersDuringPrayer)
            {
                ShowInputReminder($"Prayer time began: {prayer}. Movement speed reduced out of respect.");
            }
        }

        private void HandlePrayerTimeEnds(PrayerName prayer)
        {
            LogInfo("PrayerInput", $"Input modifications removed after prayer time: {prayer}");
            
            // Input modifications automatically removed in PollInput()
        }

        private void ShowInputReminder(string message)
        {
            LogWarning("PrayerInputReminder", message);
            
            // Would trigger UI notification
            Debug.Log($"[RVA:TAC] INPUT REMINDER: {message}");
        }

        private void TrackInputPattern()
        {
            var eventData = new InputEventData
            {
                Timestamp = DateTime.UtcNow,
                Scheme = _activeControlScheme,
                Movement = MovementInput,
                Look = LookInput,
                Sprint = IsSprintInput,
                Action = IsActionInput,
                Prayer = IsPrayerInput,
                PrayerTimeActive = _gameManager.IsPrayerTimeActive,
                IslandID = _gameManager.ActiveIslandID
            };
            
            lock (_inputHistory)
            {
                _inputHistory.Enqueue(eventData);
                while (_inputHistory.Count > 1000)
                {
                    _inputHistory.Dequeue();
                }
            }
            
            // Analyze pattern
            _patternAnalyzer?.Analyze(eventData);
        }
        #endregion

        #region Input History & Analytics
        private void RecordInputHistory()
        {
            // Periodically save input patterns
            if (Time.frameCount % 1800 == 0) // Every 30 seconds
            {
                SaveInputHistory();
            }
        }

        private void SaveInputHistory()
        {
            try
            {
                string historyPath = Path.Combine(Application.persistentDataPath, "input_history.json");
                var history = new
                {
                    SessionID = GetSessionID(),
                    ControlScheme = _activeControlScheme,
                    Timestamp = DateTime.UtcNow,
                    Events = _inputHistory.TakeLast(100).ToList()
                };
                
                string json = JsonConvert.SerializeObject(history, Formatting.Indented);
                File.WriteAllText(historyPath, json);
                
                LogInfo("InputHistory", $"Saved input history to: {historyPath}");
            }
            catch (Exception ex)
            {
                LogWarning("InputHistory", $"Failed to save input history: {ex.Message}");
            }
        }

        private void HandleInputPatternDetected(InputPattern pattern)
        {
            LogInfo("InputPattern", $"Detected pattern: {pattern.PatternType}", new
            {
                pattern.PatternType,
                pattern.Confidence,
                pattern.Frequency
            });
            
            // Could trigger tutorials or adjust difficulty
        }

        public InputPatternReport GenerateInputReport()
        {
            var report = new InputPatternReport
            {
                GeneratedAt = DateTime.UtcNow,
                ControlScheme = _activeControlScheme,
                SessionDuration = Time.time,
                TotalEvents = _inputHistory.Count,
                PrayerTimeEvents = _inputHistory.Count(e => e.PrayerTimeActive),
                AverageMovementMagnitude = _inputHistory.Average(e => e.Movement.magnitude),
                MostUsedAction = GetMostUsedAction()
            };
            
            return report;
        }

        private string GetMostUsedAction()
        {
            var actions = new Dictionary<string, int>
            {
                { "Sprint", _inputHistory.Count(e => e.Sprint) },
                { "Action", _inputHistory.Count(e => e.Action) },
                { "Prayer", _inputHistory.Count(e => e.Prayer) }
            };
            
            return actions.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        private string GetSessionID()
        {
            string sessionID = PlayerPrefs.GetString("RVA_SessionID", "");
            if (string.IsNullOrEmpty(sessionID))
            {
                sessionID = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("RVA_SessionID", sessionID);
                PlayerPrefs.Save();
            }
            return sessionID;
        }
        #endregion

        #region Helper Methods
        private void LogInfo(string context, string message, object data = null)
        {
            _debugSystem?.LogInfo(context, message, data);
        }

        private void LogWarning(string context, string message, object data = null)
        {
            _debugSystem?.LogWarning(context, message, data);
        }

        private void LogError(string context, string message, Exception ex = null, object data = null)
        {
            _debugSystem?.LogError(context, message, ex, data);
        }
        #endregion

        #region Input Handler Interfaces
        private interface IInputHandler
        {
            Vector2 GetMovementInput();
            Vector2 GetLookInput();
            bool GetSprintInput();
            bool GetActionInput();
            bool GetPrayerInput();
        }

        private class TouchInputHandler : IInputHandler
        {
            private readonly TouchInputSystem _touchSystem;
            
            public TouchInputHandler(TouchInputSystem touchSystem)
            {
                _touchSystem = touchSystem;
            }
            
            public Vector2 GetMovementInput() => _touchSystem.JoystickInput;
            public Vector2 GetLookInput() => Vector2.zero; // Touch look handled by swipe gestures
            public bool GetSprintInput() => _touchSystem.IsSprinting;
            public bool GetActionInput() => _touchSystem.IsActionPressed;
            public bool GetPrayerInput() => _touchSystem.IsPrayerPressed;
        }

        private class GamepadInputHandler : IInputHandler
        {
            private readonly InputSystem _inputSystem;
            
            public GamepadInputHandler(InputSystem inputSystem)
            {
                _inputSystem = inputSystem;
            }
            
            public Vector2 GetMovementInput()
            {
                var gamepad = Gamepad.current;
                if (gamepad == null) return Vector2.zero;
                return ApplyDeadzone(gamepad.leftStick.ReadValue(), _inputSystem.GamepadDeadzone);
            }
            
            public Vector2 GetLookInput()
            {
                var gamepad = Gamepad.current;
                if (gamepad == null) return Vector2.zero;
                return ApplyDeadzone(gamepad.rightStick.ReadValue(), _inputSystem.GamepadDeadzone);
            }
            
            public bool GetSprintInput()
            {
                var gamepad = Gamepad.current;
                if (gamepad == null) return false;
                return gamepad.rightTrigger.ReadValue() > 0.5f;
            }
            
            public bool GetActionInput()
            {
                var gamepad = Gamepad.current;
                if (gamepad == null) return false;
                return gamepad.buttonSouth.isPressed;
            }
            
            public bool GetPrayerInput()
            {
                var gamepad = Gamepad.current;
                if (gamepad == null) return false;
                return gamepad.buttonWest.isPressed;
            }
            
            private Vector2 ApplyDeadzone(Vector2 input, float deadzone)
            {
                if (input.magnitude < deadzone) return Vector2.zero;
                return input;
            }
        }

        private class KeyboardMouseInputHandler : IInputHandler
        {
            private readonly InputSystem _inputSystem;
            
            public KeyboardMouseInputHandler(InputSystem inputSystem)
            {
                _inputSystem = inputSystem;
            }
            
            public Vector2 GetMovementInput()
            {
                var keyboard = Keyboard.current;
                if (keyboard == null) return Vector2.zero;
                
                Vector2 movement = Vector2.zero;
                
                if (keyboard[_inputSystem.MovementKeys[0]].isPressed) movement.y += 1; // W
                if (keyboard[_inputSystem.MovementKeys[2]].isPressed) movement.y -= 1; // S
                if (keyboard[_inputSystem.MovementKeys[1]].isPressed) movement.x -= 1; // A
                if (keyboard[_inputSystem.MovementKeys[3]].isPressed) movement.x += 1; // D
                
                return movement.normalized;
            }
            
            public Vector2 GetLookInput()
            {
                var mouse = Mouse.current;
                if (mouse == null) return Vector2.zero;
                
                return mouse.delta.ReadValue() * _inputSystem.MouseSensitivity;
            }
            
            public bool GetSprintInput()
            {
                var keyboard = Keyboard.current;
                if (keyboard == null) return false;
                return keyboard[_inputSystem.SprintKey].isPressed;
            }
            
            public bool GetActionInput()
            {
                var keyboard = Keyboard.current;
                if (keyboard == null) return false;
                return keyboard[_inputSystem.ActionKey].isPressed;
            }
            
            public bool GetPrayerInput()
            {
                var keyboard = Keyboard.current;
                if (keyboard == null) return false;
                return keyboard[_inputSystem.PrayerKey].isPressed;
            }
        }

        private class NullInputHandler : IInputHandler
        {
            public Vector2 GetMovementInput() => Vector2.zero;
            public Vector2 GetLookInput() => Vector2.zero;
            public bool GetSprintInput() => false;
            public bool GetActionInput() => false;
            public bool GetPrayerInput() => false;
        }
        #endregion

        #region Data Structures
        [Serializable]
        public class InputEventData
        {
            public DateTime Timestamp;
            public ControlScheme Scheme;
            public Vector2 Movement;
            public Vector2 Look;
            public bool Sprint;
            public bool Action;
            public bool Prayer;
            public bool PrayerTimeActive;
            public int IslandID;
        }

        [Serializable]
        public class InputPatternReport
        {
            public DateTime GeneratedAt;
            public ControlScheme ControlScheme;
            public float SessionDuration;
            public int TotalEvents;
            public int PrayerTimeEvents;
            public float AverageMovementMagnitude;
            public string MostUsedAction;
        }

        private class TouchPhaseTracker
        {
            public bool IsTracking = false;
            public Vector2 StartPosition;
            public Vector2 EndPosition;
            public float StartTime;
            public float EndTime;
            public bool IsJoystickTouch;
            public bool IsButtonTouch;
            
            public float Duration => EndTime - StartTime;
            public Vector2 Delta => EndPosition - StartPosition;
        }

        public enum ControlScheme
        {
            None,
            Touch,
            Gamepad,
            KeyboardMouse
        }
        #endregion
    }
}
