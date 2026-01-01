// InputSystem.cs - Unified Input Abstraction for RVA:TAC
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;

namespace RAAJJE_VAGU_AUTO
{
    public enum InputMode
    {
        Touch,
        Gamepad,
        KeyboardMouse
    }

    [BurstCompile]
    public struct InputSettingsData : IComponentData
    {
        public InputMode CurrentMode;
        public float SensitivityX;
        public float SensitivityY;
        public bool InvertY;
        public bool VibrationEnabled;
        public bool AccessibilityMode;
        public int FontScale;
        public float InputDelay; // For motor accessibility
    }

    [BurstCompile]
    public struct InputEventData : IComponentData
    {
        public NativeList<InputEvent> EventQueue;
        public int CurrentEventIndex;
    }

    [BurstCompile]
    public struct InputEvent
    {
        public InputActionType ActionType;
        public float2 Value;
        public double Timestamp;
        public bool IsPressed;
    }

    public enum InputActionType
    {
        Move,
        Look,
        Jump,
        Run,
        Action,
        Attack,
        VehicleEnter,
        Map,
        Inventory,
        Pause,
        PrayerNotificationAcknowledge
    }

    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InputSystemGroup))]
    [BurstCompile]
    public partial struct InputSystem : ISystem, IInputActionCollection
    {
        private InputActionAsset _inputActions;
        private InputMode _currentMode;
        private bool _isPausedForInterruption;
        private float _pauseEndTime;
        private bool _batterySaverMode;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InputSettingsData>();
            state.RequireForUpdate<PlayerInputData>();
            
            InitializeInputActions();
            DetectInputMode();
            SetupInterruptionHandling();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_inputActions != null)
            {
                _inputActions.Dispose();
            }
        }

        void InitializeInputActions()
        {
            _inputActions = InputActionAsset.FromJson(@"
            {
                ""name"": ""RVA_InputActions"",
                ""maps"": [
                    {
                        ""name"": ""Gameplay"",
                        ""actions"": [
                            { ""name"": ""Move"", ""type"": ""Value"", ""expectedControlType"": ""Vector2"" },
                            { ""name"": ""Look"", ""type"": ""Value"", ""expectedControlType"": ""Vector2"" },
                            { ""name"": ""Jump"", ""type"": ""Button"" },
                            { ""name"": ""Run"", ""type"": ""Button"" },
                            { ""name"": ""Action"", ""type"": ""Button"" },
                            { ""name"": ""Attack"", ""type"": ""Button"" }
                        ],
                        ""bindings"": [
                            { ""action"": ""Move"", ""path"": ""<Gamepad>/leftStick"" },
                            { ""action"": ""Move"", ""path"": ""<Keyboard>/w<Keyboard>/s<Keyboard>/a<Keyboard>/d"" },
                            { ""action"": ""Look"", ""path"": ""<Gamepad>/rightStick"" },
                            { ""action"": ""Look"", ""path"": ""<Mouse>/delta"" },
                            { ""action"": ""Jump"", ""path"": ""<Keyboard>/space"" },
                            { ""action"": ""Jump"", ""path"": ""<Gamepad>/buttonSouth"" }
                        ]
                    }
                ]
            }");
            
            _inputActions.Enable();
        }

        void DetectInputMode()
        {
            if (Input.touchSupported && Application.isMobilePlatform)
                _currentMode = InputMode.Touch;
            else if (Gamepad.current != null)
                _currentMode = InputMode.Gamepad;
            else
                _currentMode = InputMode.KeyboardMouse;
        }

        void SetupInterruptionHandling()
        {
            // Subscribe to system events
            Application.lowMemory += OnLowMemory;
            Application.focusChanged += OnFocusChanged;
            
            // Battery saver detection
            _batterySaverMode = (int)SystemInfo.batteryLevel < 20;
        }

        void OnLowMemory()
        {
            // Pause input processing for 2 seconds to prevent cascading errors
            _isPausedForInterruption = true;
            _pauseEndTime = Time.time + 2f;
        }

        void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                _isPausedForInterruption = true;
                _pauseEndTime = Time.time + 1f;
            }
            else
            {
                _isPausedForInterruption = false;
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_isPausedForInterruption && Time.time < _pauseEndTime) return;
            _isPausedForInterruption = false;

            var settings = SystemAPI.GetSingleton<InputSettingsData>();
            settings.CurrentMode = _currentMode;
            
            // Battery saver adjustment
            if (_batterySaverMode)
            {
                settings.SensitivityX *= 0.8f;
                settings.SensitivityY *= 0.8f;
            }
            
            SystemAPI.SetSingleton(settings);

            // Update player input based on mode
            if (_currentMode == InputMode.Touch)
            {
                UpdateTouchInput(ref state);
            }
            else
            {
                UpdateTraditionalInput(ref state);
            }

            // Queue input events
            ProcessInputQueue(ref state);
        }

        void UpdateTouchInput(ref SystemState state)
        {
            var touchQuery = SystemAPI.QueryBuilder()
                .WithAll<TouchInputData>()
                .Build();
            
            if (touchQuery.IsEmpty) return;

            foreach (var touch in SystemAPI.Query<TouchInputData>())
            {
                var playerInput = SystemAPI.GetSingleton<PlayerInputData>();
                
                playerInput.MoveInput = touch.MoveInput;
                playerInput.LookInput = touch.LookInput;
                playerInput.IsTouching = touch.IsTouching;
                
                // Map gestures to actions
                switch (touch.CurrentGesture)
                {
                    case TouchGesture.Tap:
                        playerInput.ActionPressed = true;
                        break;
                    case TouchGesture.DoubleTap:
                        playerInput.JumpPressed = true;
                        break;
                    case TouchGesture.LongPress:
                        playerInput.AttackPressed = true;
                        break;
                    case TouchGesture.SwipeUp:
                        playerInput.JumpPressed = true;
                        break;
                }

                SystemAPI.SetSingleton(playerInput);
            }
        }

        void UpdateTraditionalInput(ref SystemState state)
        {
            var moveAction = _inputActions.FindAction("Move");
            var lookAction = _inputActions.FindAction("Look");
            var jumpAction = _inputActions.FindAction("Jump");
            var runAction = _inputActions.FindAction("Run");
            var actionAction = _inputActions.FindAction("Action");

            var playerInput = SystemAPI.GetSingleton<PlayerInputData>();
            
            // Apply sensitivity and deadzone
            var settings = SystemAPI.GetSingleton<InputSettingsData>();
            float2 moveInput = ApplyDeadzone(moveAction.ReadValue<Vector2>(), 0.15f);
            float2 lookInput = ApplySensitivity(
                ApplyDeadzone(lookAction.ReadValue<Vector2>(), 0.1f),
                settings
            );

            playerInput.MoveInput = moveInput;
            playerInput.LookInput = lookInput;
            playerInput.JumpPressed = jumpAction.WasPressedThisFrame();
            playerInput.RunPressed = runAction.IsPressed();
            playerInput.ActionPressed = actionAction.WasPressedThisFrame();
            playerInput.AttackPressed = _inputActions.FindAction("Attack")?.WasPressedThisFrame() ?? false;
            
            SystemAPI.SetSingleton(playerInput);
        }

        float2 ApplyDeadzone(float2 input, float deadzone)
        {
            float magnitude = math.length(input);
            if (magnitude < deadzone) return float2.zero;
            
            float normalizedMag = math.unlerp(deadzone, 1f, magnitude);
            return math.normalize(input) * normalizedMag;
        }

        float2 ApplySensitivity(float2 input, InputSettingsData settings)
        {
            return new float2(
                input.x * settings.SensitivityX,
                input.y * settings.SensitivityY * (settings.InvertY ? -1f : 1f)
            );
        }

        void ProcessInputQueue(ref SystemState state)
        {
            var inputEventJob = new InputEventProcessingJob
            {
                CurrentTime = SystemAPI.Time.ElapsedTime,
                Settings = SystemAPI.GetSingleton<InputSettingsData>()
            };
            
            inputEventJob.Schedule();
        }

        [BurstCompile]
        public partial struct InputEventProcessingJob : IJobEntity
        {
            public double CurrentTime;
            public InputSettingsData Settings;

            void Execute(ref PlayerInputData playerInput)
            {
                // Apply input delay for accessibility
                if (Settings.AccessibilityMode && Settings.InputDelay > 0)
                {
                    // Simple input smoothing for motor disabilities
                    playerInput.MoveInput = math.lerp(
                        playerInput.MoveInput, 
                        playerInput.MoveInput, 
                        (float)CurrentTime * Settings.InputDelay
                    );
                }
            }
        }

        // IInputActionCollection implementation
        public InputActionAsset asset => _inputActions;
        public IEnumerable<InputBinding> bindings => _inputActions.bindings;
        public InputAction FindAction(string actionName) => _inputActions.FindAction(actionName);
        public int FindBinding(InputBinding binding, out InputAction action) => _inputActions.FindBinding(binding, out action);
    }

    // Settings manager for input configuration
    public class InputSettingsManager : MonoBehaviour
    {
        [Header("Sensitivity")]
        public float LookSensitivityX = 1.5f;
        public float LookSensitivityY = 1.5f;
        public bool InvertYAxis = false;
        
        [Header("Accessibility")]
        public bool EnableAccessibilityMode = false;
        public float InputDelay = 0.1f;
        public int FontScale = 100;
        public bool EnableVibration = true;
        
        [Header("Cultural")]
        public bool AutoPauseDuringPrayer = true;
        public bool VibrateOnPrayerNotification = true;

        private EntityManager _entityManager;

        void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            ApplySettings();
        }

        public void ApplySettings()
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, new InputSettingsData
            {
                CurrentMode = Application.isMobilePlatform ? InputMode.Touch : InputMode.KeyboardMouse,
                SensitivityX = LookSensitivityX,
                SensitivityY = LookSensitivityY,
                InvertY = InvertYAxis,
                VibrationEnabled = EnableVibration,
                AccessibilityMode = EnableAccessibilityMode,
                FontScale = FontScale,
                InputDelay = InputDelay
            });
            
            SystemAPI.SetSingleton(new InputSettingsData
            {
                CurrentMode = Application.isMobilePlatform ? InputMode.Touch : InputMode.KeyboardMouse,
                SensitivityX = LookSensitivityX,
                SensitivityY = LookSensitivityY,
                InvertY = InvertYAxis,
                VibrationEnabled = EnableVibration,
                AccessibilityMode = EnableAccessibilityMode,
                FontScale = FontScale,
                InputDelay = InputDelay
            });
        }

        void OnPrayerTimeStarted(PrayerType prayerType)
        {
            if (AutoPauseDuringPrayer && prayerType != PrayerType.None)
            {
                // Pause input for prayer notification
                Time.timeScale = 0.3f; // Slow motion, not full pause
            }
        }
        
        void OnPrayerTimeEnded()
        {
            Time.timeScale = 1f;
        }
    }
}
