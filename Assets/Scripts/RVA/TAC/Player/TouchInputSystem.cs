// TouchInputSystem.cs - Mobile Touch & Gesture System for RVA:TAC
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace RAAJJE_VAGU_AUTO
{
    public enum TouchGesture
    {
        None,
        Tap,
        DoubleTap,
        LongPress,
        SwipeLeft,
        SwipeRight,
        SwipeUp,
        SwipeDown,
        Pinch
    }

    [BurstCompile]
    public struct TouchInputData : IComponentData
    {
        public float2 PrimaryTouchPosition;
        public float2 LastTouchPosition;
        public float2 Delta;
        public float TapStartTime;
        public float LastTapTime;
        public bool IsTouching;
        public bool HasMoved;
        public TouchGesture CurrentGesture;
        public float PinchDistance;
        public float LastPinchDistance;
    }

    public class TouchInputSystem : MonoBehaviour
    {
        [System.Serializable]
        public class TouchButton
        {
            public string Name;
            public RectTransform ButtonRect;
            public Image ButtonImage;
            public Color NormalColor = Color.white;
            public Color PressedColor = Color.gray;
            public System.Action OnPressed;
            public System.Action OnReleased;
            
            private bool _wasPressed;
            
            public void ProcessTouch(Vector2 touchPos)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(ButtonRect, touchPos))
                {
                    if (!_wasPressed)
                    {
                        ButtonImage.color = PressedColor;
                        OnPressed?.Invoke();
                        _wasPressed = true;
                    }
                }
                else
                {
                    if (_wasPressed)
                    {
                        ButtonImage.color = NormalColor;
                        OnReleased?.Invoke();
                        _wasPressed = false;
                    }
                }
            }
            
            public void Reset()
            {
                _wasPressed = false;
                ButtonImage.color = NormalColor;
            }
        }

        [Header("Virtual Joystick")]
        public RectTransform JoystickOuter;
        public RectTransform JoystickInner;
        public float JoystickRange = 100f;
        public float JoystickDeadZone = 20f;
        
        [Header("Action Buttons")]
        public TouchButton JumpButton;
        public TouchButton RunButton;
        public TouchButton ActionButton;
        public TouchButton AttackButton;
        
        [Header("Gesture Settings")]
        public float TapDuration = 0.2f;
        public float LongPressDuration = 0.5f;
        public float DoubleTapInterval = 0.3f;
        public float SwipeThreshold = 50f;
        public float PinchThreshold = 10f;
        
        private EntityManager _entityManager;
        private Entity _inputEntity;
        private Vector2 _joystickStartPos;
        private Vector2 _joystickCurrentPos;
        private bool _isJoystickActive;
        private float _lastCallInterruptCheck;
        
        void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            var entity = _entityManager.CreateEntity();
            _inputEntity = entity;
            
            _entityManager.AddComponentData(entity, new TouchInputData
            {
                PrimaryTouchPosition = float2.zero,
                LastTouchPosition = float2.zero,
                Delta = float2.zero,
                TapStartTime = 0,
                LastTapTime = -1f,
                IsTouching = false,
                HasMoved = false,
                CurrentGesture = TouchGesture.None,
                PinchDistance = 0,
                LastPinchDistance = 0
            });
            
            // Initialize button actions
            SetupButtonActions();
        }
        
        void SetupButtonActions()
        {
            JumpButton.OnPressed = () => 
            {
                if (_entityManager.Exists(_inputEntity))
                {
                    var inputData = _entityManager.GetComponentData<PlayerInputData>(_playerEntity);
                    inputData.JumpPressed = true;
                    _entityManager.SetComponentData(_playerEntity, inputData);
                }
            };
            
            RunButton.OnPressed = () => 
            {
                if (_entityManager.Exists(_inputEntity))
                {
                    var inputData = _entityManager.GetComponentData<PlayerInputData>(_playerEntity);
                    inputData.RunPressed = true;
                    _entityManager.SetComponentData(_playerEntity, inputData);
                }
            };
        }

        void Update()
        {
            // Handle incoming phone calls (common in Maldives)
            CheckForCallInterruption();
            
            // Process touch input
            ProcessTouchInput();
            
            // Update ECS data
            UpdateInputData();
        }
        
        void CheckForCallInterruption()
        {
            if (Time.time - _lastCallInterruptCheck < 1f) return;
            
            // Check Application.internetReachability for call interruption pattern
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                // Pause input for 3 seconds on call interruption
                Invoke(nameof(ResumeInput), 3f);
                _lastCallInterruptCheck = Time.time;
            }
        }
        
        void ResumeInput() { /* Auto-resume */ }

        void ProcessTouchInput()
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;

            TouchInputData touchData = default;
            if (_entityManager.Exists(_inputEntity))
            {
                touchData = _entityManager.GetComponentData<TouchInputData>(_inputEntity);
            }

            // Handle multi-touch (pinch for camera zoom)
            if (Input.touchCount == 2)
            {
                HandlePinchGesture(ref touchData);
                return;
            }

            // Single touch or mouse
            bool isTouching = Input.GetMouseButton(0) || Input.touchCount == 1;
            Vector2 touchPosition = isTouching ? 
                (Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition) : 
                Vector2.zero;

            if (isTouching)
            {
                ProcessActiveTouch(ref touchData, touchPosition);
            }
            else
            {
                ProcessTouchRelease(ref touchData);
            }

            if (_entityManager.Exists(_inputEntity))
            {
                _entityManager.SetComponentData(_inputEntity, touchData);
            }
        }

        void ProcessActiveTouch(ref TouchInputData touchData, Vector2 touchPosition)
        {
            if (!touchData.IsTouching)
            {
                touchData.IsTouching = true;
                touchData.TapStartTime = Time.time;
                touchData.PrimaryTouchPosition = touchPosition;
                touchData.LastTouchPosition = touchPosition;
                touchData.HasMoved = false;
                
                CheckForJoystickActivation(touchPosition);
                CheckForButtonPress(touchPosition);
            }
            else
            {
                touchData.Delta = touchPosition - touchData.LastTouchPosition;
                touchData.LastTouchPosition = touchPosition;

                if (math.length(touchData.Delta) > 1f)
                {
                    touchData.HasMoved = true;
                }

                if (_isJoystickActive)
                {
                    UpdateJoystick(touchPosition);
                }
                else if (touchData.HasMoved)
                {
                    DetectSwipeGesture(ref touchData);
                }

                CheckForButtonHold(touchPosition);
            }
        }

        void ProcessTouchRelease(ref TouchInputData touchData)
        {
            if (!touchData.IsTouching) return;

            float touchDuration = Time.time - touchData.TapStartTime;
            
            if (!touchData.HasMoved)
            {
                if (touchDuration < TapDuration)
                {
                    // Check for double tap
                    if (Time.time - touchData.LastTapTime < DoubleTapInterval)
                    {
                        touchData.CurrentGesture = TouchGesture.DoubleTap;
                    }
                    else
                    {
                        touchData.CurrentGesture = TouchGesture.Tap;
                    }
                    touchData.LastTapTime = Time.time;
                }
                else if (touchDuration > LongPressDuration)
                {
                    touchData.CurrentGesture = TouchGesture.LongPress;
                }
            }

            ResetJoystick();
            ResetButtons();
            
            touchData.IsTouching = false;
            touchData.HasMoved = false;
            touchData.Delta = float2.zero;
        }

        void CheckForJoystickActivation(Vector2 touchPosition)
        {
            if (Vector2.Distance(touchPosition, JoystickOuter.position) < JoystickRange * 1.5f)
            {
                _isJoystickActive = true;
                _joystickStartPos = JoystickOuter.position;
                JoystickOuter.gameObject.SetActive(true);
                JoystickInner.gameObject.SetActive(true);
            }
        }

        void UpdateJoystick(Vector2 touchPosition)
        {
            Vector2 offset = touchPosition - _joystickStartPos;
            offset = Vector2.ClampMagnitude(offset, JoystickRange);
            
            JoystickInner.position = _joystickStartPos + offset;
            
            // Send input to player
            if (_entityManager.Exists(_playerEntity))
            {
                var input = _entityManager.GetComponentData<PlayerInputData>(_playerEntity);
                input.MoveInput = new float2(offset.x / JoystickRange, 
                                           offset.y / JoystickRange);
                _entityManager.SetComponentData(_playerEntity, input);
            }
        }

        void ResetJoystick()
        {
            _isJoystickActive = false;
            JoystickOuter.gameObject.SetActive(false);
            JoystickInner.gameObject.SetActive(false);
            
            // Reset input
            if (_entityManager.Exists(_playerEntity))
            {
                var input = _entityManager.GetComponentData<PlayerInputData>(_playerEntity);
                input.MoveInput = float2.zero;
                _entityManager.SetComponentData(_playerEntity, input);
            }
        }

        void CheckForButtonPress(Vector2 touchPosition)
        {
            JumpButton.ProcessTouch(touchPosition);
            RunButton.ProcessTouch(touchPosition);
            ActionButton.ProcessTouch(touchPosition);
            AttackButton.ProcessTouch(touchPosition);
        }

        void CheckForButtonHold(Vector2 touchPosition)
        {
            RunButton.ProcessTouch(touchPosition);
        }

        void ResetButtons()
        {
            JumpButton.Reset();
            RunButton.Reset();
            ActionButton.Reset();
            AttackButton.Reset();
            
            // Reset press states
            if (_entityManager.Exists(_playerEntity))
            {
                var playerInput = _entityManager.GetComponentData<PlayerInputData>(_playerEntity);
                playerInput.JumpPressed = false;
                playerInput.RunPressed = false;
                playerInput.ActionPressed = false;
                playerInput.AttackPressed = false;
                _entityManager.SetComponentData(_playerEntity, playerInput);
            }
        }

        void DetectSwipeGesture(ref TouchInputData touchData)
        {
            float deltaX = touchData.Delta.x;
            float deltaY = touchData.Delta.y;
            
            if (math.abs(deltaX) > math.abs(deltaY))
            {
                if (math.abs(deltaX) > SwipeThreshold)
                {
                    touchData.CurrentGesture = deltaX > 0 ? TouchGesture.SwipeRight : TouchGesture.SwipeLeft;
                }
            }
            else
            {
                if (math.abs(deltaY) > SwipeThreshold)
                {
                    touchData.CurrentGesture = deltaY > 0 ? TouchGesture.SwipeUp : TouchGesture.SwipeDown;
                }
            }
        }

        void HandlePinchGesture(ref TouchInputData touchData)
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);
            
            touchData.Pin = math.distance(touch1.position, touch2.position);
            
            if (touchData.LastPinchDistance > 0)
            {
                float delta = touchData.PinchDistance - touchData.LastPinchDistance;
                if (math.abs(delta) > PinchThreshold)
                {
                    // Send pinch data to camera system
                    var cameraEntity = _entityManager.CreateEntityQuery(typeof(CameraData)).GetSingletonEntity();
                    if (_entityManager.Exists(cameraEntity))
                    {
                        var cameraData = _entityManager.GetComponentData<CameraData>(cameraEntity);
                        cameraData.TargetZoom += delta * 0.01f;
                        cameraData.TargetZoom = math.clamp(cameraData.TargetZoom, 0.5f, 2f);
                        _entityManager.SetComponentData(cameraEntity, cameraData);
                    }
                }
            }
            
            touchData.LastPinchDistance = touchData.PinchDistance;
        }

        void UpdateInputData()
        {
            if (!_entityManager.Exists(_inputEntity) || 
                !_entityManager.Exists(_playerEntity)) return;

            var touchData = _entityManager.GetComponentData<TouchInputData>(_inputEntity);
            
            // Update gesture-based inputs
            switch (touchData.CurrentGesture)
            {
                case TouchGesture.DoubleTap:
                    var playerMovement = _entityManager.GetComponentData<PlayerMovementData>(_playerEntity);
                    playerMovement.IsPrayerRespecting = !playerMovement.IsPrayerRespecting;
                    _entityManager.SetComponentData(_playerEntity, playerMovement);
                    break;
                    
                case TouchGesture.LongPress:
                    // Context-sensitive action
                    var input = _entityManager.GetComponentData<PlayerInputData>(_playerEntity);
                    input.ActionPressed = true;
                    _entityManager.SetComponentData(_playerEntity, input);
                    break;
            }
            
            // Clear gesture after processing
            touchData.CurrentGesture = TouchGesture.None;
            _entityManager.SetComponentData(_inputEntity, touchData);
        }

        void OnDrawGizmos()
        {
            if (JoystickOuter != null && JoystickInner != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(JoystickOuter.position, JoystickRange);
            }
        }
    }
}
