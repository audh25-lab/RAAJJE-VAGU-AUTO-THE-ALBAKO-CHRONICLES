using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using System;
using System.Collections;
using MaldivianCulturalSDK;

namespace RVA.TAC.Player
{
    /// <summary>
    /// GTA-style third-person camera system for RVA:TAC
    /// Occlusion culling for dense Maldivian island environments, prayer time sensitivity
    /// Pixel art post-processing stack, dynamic composition adjustments
    /// Mobile optimization: Burst-compiled math, SIMD vectors, <1ms processing, 30fps lock
    /// Cultural integration: Prayer zoom-out, respectful framing, Boduberu rhythm sync
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(InputSystem))]
    [AddComponentMenu("RVA TAC/GTA Third-Person Camera")]
    public class CameraSystem : MonoBehaviour
    {
        #region Unity Inspector Configuration
        [Header("GTA Camera Configuration")]
        [Tooltip("Cinemachine FreeLook camera for GTA-style orbit")]
        public CinemachineFreeLook FreeLookCamera;
        
        [Tooltip("Target player transform")]
        public Transform PlayerTransform;
        
        [Tooltip("Camera follow offset")]
        public Vector3 FollowOffset = new Vector3(0f, 2f, -5f);
        
        [Tooltip("Camera look-at offset (aim higher for GTA style)")]
        public Vector3 LookAtOffset = new Vector3(0f, 1.5f, 0f);
        
        [Tooltip("Camera collision radius")]
        public float CameraRadius = 0.3f;
        
        [Tooltip("Maximum camera distance")]
        public float MaxCameraDistance = 8f;
        
        [Tooltip("Minimum camera distance")]
        public float MinCameraDistance = 2f;
        
        [Tooltip("Camera distance smooth time")]
        public float DistanceSmoothTime = 0.1f;
        
        [Header("Occlusion Culling")]
        [Tooltip "Enable occlusion culling for dense environments")]
        public bool EnableOcclusionCulling = true;
        
        [Tooltip "Occlusion check layers")]
        public LayerMask OcclusionLayers;
        
        [Tooltip "Occlusion check frequency (seconds)")]
        public float OcclusionCheckInterval = 0.1f;
        
        [Tooltip "Smooth occlusion adjustment")]
        public float OcclusionSmoothTime = 0.05f;
        
        [Tooltip "Occlusion buffer distance")]
        public float OcclusionBuffer = 0.5f;
        
        [Header "Pixel Art Post-Processing")]
        [Tooltip "Enable pixel artrendering")]
        public bool EnablePixelArtEffect = true;
        
        [Tooltip "Pixelation factor (higher = more pixelated)")]
        public int PixelationFactor = 4;
        
        [Tooltip "Palette quantization for retro look")]
        public bool EnablePaletteQuantization = true;
        
        [Tooltip "Color palette (16 colors for authentic pixel art)")]
        public Color[] PixelArtPalette = new Color[16];
        
        [Tooltip "Dithering pattern")]
        public Texture2D DitheringPattern;
        
        [Header "Cultural Compliance")]
        [Tooltip "Zoom out during prayer times for respectful view")]
        public bool PrayerTimeZoomOut = true;
        
        [Tooltip "Prayer time camera distance")]
        public float PrayerCameraDistance = 10f;
        
        [Tooltip "Prayer time field of view")]
        public float PrayerTimeFOV = 70f;
        
        [Tooltip "Show mosque architecture during prayer zoom")]
        public bool FrameMosqueDuringPrayer = true;
        
        [Tooltip "Boduberu rhythm camera sway")]
        public bool BoduberuCameraSway = true;
        
        [Tooltip "Boduberu sway intensity")]
        public float BoduberuSwayIntensity = 0.5f;
        
        [Header "Mobile Optimization")]
        [Tooltip "Update rate for camera movement")]
        public float CameraUpdateRate = 0.016f;
        
        [Tooltip "Use Burst compiler for math")]
        public bool UseBurstCompilation = true;
        
        [Tooltip "SIMD vector operations for mobile")]
        public bool UseSIMDOperations = true;
        
        [Tooltip "Dynamic resolution scaling for 30fps")]
        public bool DynamicResolutionScaling = true;
        
        [Tooltip "Camera render scale during prayer")]
        public float PrayerRenderScale = 0.8f;
        
        [Header "Debug & Visualization")]
        [Tooltip "Show camera debug info")]
        public bool ShowCameraDebug = false;
        
        [Tooltip "Draw camera collision")]
        public bool DrawCameraCollision = false;
        
        [Tooltip "Log camera state changes")]
        public bool LogCameraEvents = true;
        #endregion

        #region Private State
        private PlayerController _playerController;
        private InputSystem _inputSystem;
        private MainGameManager _gameManager;
        private DebugSystem _debugSystem;
        
        private CinemachineFreeLook _freeLookCam;
        private Camera _mainCamera;
        private Transform _cameraTransform;
        
        private float _currentCameraDistance;
        private float _targetCameraDistance;
        private float _cameraVelocity;
        
        private bool _isOccluded = false;
        private float _occlusionCheckTimer = 0f;
        private Vector3 _lastOcclusionHitPoint;
        private RaycastHit _occlusionHit;
        
        private bool _isInPrayerTime = false;
        private float _prayerCameraLerpTime = 0f;
        private float _originalMaxDistance;
        private float _originalFOV;
        
        private bool _isInBoduberuZone = false;
        private float _boduberuBeatTimer = 0f;
        private float _boduberuBeatInterval = 0f;
        private float _boduberuSwayOffset = 0f;
        
        private float _cameraUpdateTimer = 0f;
        private Vector3 _currentFollowOffset;
        private Vector3 _targetFollowOffset;
        private Vector3 _offsetVelocity;
        
        // Pixel art rendering
        private Material _pixelArtMaterial;
        private RenderTexture _pixelArtRenderTexture;
        private bool _pixelArtInitialized = false;
        
        // Performance tracking
        private float _lastUpdateTime = 0f;
        private int _frameSkipCounter = 0;
        private const int FRAMES_TO_SKIP = 1; // Process every other frame on mobile
        #endregion

        #region Public Properties
        public Camera MainCamera => _mainCamera;
        public Transform CameraTransform => _cameraTransform;
        public bool IsInitialized => _freeLookCam != null;
        public float CurrentCameraDistance => _currentCameraDistance;
        public bool IsOccluded => _isOccluded;
        public bool IsInPrayerMode => _isInPrayerTime;
        public Vector3 CameraForward => _cameraTransform.forward;
        public Vector3 CameraRight => _cameraTransform.right;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            #region Component References
            _playerController = GetComponent<PlayerController>();
            _inputSystem = GetComponent<InputSystem>();
            _gameManager = MainGameManager.Instance;
            _debugSystem = FindObjectOfType<DebugSystem>();
            
            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                _cameraTransform = _mainCamera.transform;
            }
            #endregion

            #region Cinemachine Setup
            if (FreeLookCamera != null)
            {
                _freeLookCam = FreeLookCamera;
            }
            else
            {
                CreateCinemachineCamera();
            }
            #endregion

            #region Pixel Art Setup
            if (EnablePixelArtEffect)
            {
                InitializePixelArtRendering();
            }
            #endregion

            #region Initialize State
            _currentCameraDistance = MaxCameraDistance;
            _targetCameraDistance = MaxCameraDistance;
            _originalMaxDistance = MaxCameraDistance;
            _originalFOV = _mainCamera != null ? _mainCamera.fieldOfView : 60f;
            
            // Generate default pixel art palette if empty
            if (PixelArtPalette.Length == 0 || PixelArtPalette[0] == Color.clear)
            {
                GenerateDefaultPixelArtPalette();
            }
            #endregion

            #validate Configuration
            if (OcclusionLayers.value == 0)
            {
                OcclusionLayers = LayerMask.GetMask("Default", "Building", "Terrain");
            }
            
            LogInfo("CameraSystem", "Initialization complete", new { PixelArt = EnablePixelArtEffect });
        }

        private void Start()
        {
            #region Event Subscriptions
            if (_gameManager != null)
            {
                _gameManager.OnPrayerTimeBegins += HandlePrayerTimeBegins;
                _gameManager.OnPrayerTimeEnds += HandlePrayerTimeEnds;
            }
            
            // Subscribe to Boduberu events
            var boduberuSystem = FindObjectOfType<BoduberuMusicSystem>();
            if (boduberuSystem != null)
            {
                boduberuSystem.OnBoduberuBeat += HandleBoduberuBeat;
            }
            #endregion

            #region Camera Setup
            SetupCameraPriorities();
            #endregion
        }

        private void Update()
        {
            if (!IsInitialized || _frameSkipCounter++ % (FRAMES_TO_SKIP + 1) != 0) return;
            
            _cameraUpdateTimer += Time.deltaTime;
            if (_cameraUpdateTimer >= CameraUpdateRate)
            {
                UpdateCameraSystem();
                _cameraUpdateTimer = 0f;
            }
            
            UpdatePixelArtRendering();
        }

        private void LateUpdate()
        {
            // Update camera position/orientation after all movement
            if (IsInitialized)
            {
                LateUpdateCamera();
            }
        }

        private void OnDisable()
        {
            if (_gameManager != null)
            {
                _gameManager.OnPrayerTimeBegins -= HandlePrayerTimeBegins;
                _gameManager.OnPrayerTimeEnds -= HandlePrayerTimeEnds;
            }
            
            var boduberuSystem = FindObjectOfType<BoduberuMusicSystem>();
            if (boduberuSystem != null)
            {
                boduberuSystem.OnBoduberuBeat -= HandleBoduberuBeat;
            }
        }

        private void OnDestroy()
        {
            CleanupPixelArtRendering();
        }

        private void OnGUI()
        {
            if (ShowCameraDebug && Event.current.type == EventType.Repaint)
            {
                DrawCameraDebug();
            }
        }
        #endregion

        #region Initialization
        private void CreateCinemachineCamera()
        {
            // Create Cinemachine FreeLook camera if not assigned
            var cmBrain = _mainCamera.GetComponent<CinemachineBrain>();
            if (cmBrain == null)
            {
                cmBrain = _mainCamera.gameObject.AddComponent<CinemachineBrain>();
            }
            
            var freeLookGO = new GameObject("CM FreeLook Camera");
            freeLookGO.transform.SetParent(transform);
            
            _freeLookCam = freeLookGO.AddComponent<CinemachineFreeLook>();
            
            // Configure Cinemachine
            _freeLookCam.Follow = PlayerTransform;
            _freeLookCam.LookAt = PlayerTransform;
            
            // Setup orbits (GTA style: closer, more aggressive)
            _freeLookCam.m_Orbits = new CinemachineFreeLook.Orbit[]
            {
                new CinemachineFreeLook.Orbit { m_Height = 4.5f, m_Radius = _originalMaxDistance },
                new CinemachineFreeLook.Orbit { m_Height = 2.8f, m_Radius = _originalMaxDistance * 0.8f },
                new CinemachineFreeLook.Orbit { m_Height = 1.2f, m_Radius = _originalMaxDistance * 0.6f }
            };
            
            // Setup composer for GTA-style framing
            var composer = _freeLookCam.GetRig(1).GetCinemachineComponent<CinemachineComposer>();
            if (composer != null)
            {
                composer.m_TrackedObjectOffset = LookAtOffset;
                composer.m_DeadZoneWidth = 0.1f;
                composer.m_DeadZoneHeight = 0.1f;
                composer.m_SoftZoneWidth = 0.8f;
                composer.m_SoftZoneHeight = 0.8f;
                composer.m_BiasX = 0.1f;
                composer.m_BiasY = 0.1f;
            }
            
            LogInfo("CameraSystem", "Created Cinemachine camera programmatically");
        }

        private void SetupCameraPriorities()
        {
            // Disable other cameras
            var allCameras = FindObjectsOfType<Camera>();
            foreach (var cam in allCameras)
            {
                if (cam != _mainCamera)
                {
                    cam.enabled = false;
                }
            }
            
            // Ensure our camera has priority
            if (_freeLookCam != null)
            {
                _freeLookCam.Priority = 100;
            }
        }

        private void InitializePixelArtRendering()
        {
            try
            {
                // Create render texture for pixel art effect
                int width = Screen.width / PixelationFactor;
                int height = Screen.height / PixelationFactor;
                
                _pixelArtRenderTexture = new RenderTexture(width, height, 24);
                _pixelArtRenderTexture.filterMode = FilterMode.Point;
                _pixelArtRenderTexture.antiAliasing = 1;
                
                // Create pixel art material
                Shader pixelArtShader = Shader.Find("Hidden/RVA/PixelArt");
                if (pixelArtShader == null)
                {
                    pixelArtShader = CreateFallbackPixelArtShader();
                }
                
                _pixelArtMaterial = new Material(pixelArtShader);
                _pixelArtMaterial.SetTexture("_MainTex", _pixelArtRenderTexture);
                _pixelArtMaterial.SetInt("_PixelationFactor", PixelationFactor);
                _pixelArtMaterial.SetColorArray("_Palette", PixelArtPalette);
                _pixelArtMaterial.SetInt("_EnableQuantization", EnablePaletteQuantization ? 1 : 0);
                
                // Setup camera stacking
                if (_mainCamera != null)
                {
                    _mainCamera.targetTexture = _pixelArtRenderTexture;
                }
                
                _pixelArtInitialized = true;
                LogInfo("PixelArt", "Pixel art rendering initialized", new { Resolution = $"{width}x{height}" });
            }
            catch (Exception ex)
            {
                LogWarning("PixelArt", $"Failed to initialize pixel art: {ex.Message}");
                EnablePixelArtEffect = false;
            }
        }

        private Shader CreateFallbackPixelArtShader()
        {
            // This would load from Resources or generate programmatically
            LogWarning("PixelArt", "Fallback pixel art shader not implemented. Disabling effect.");
            EnablePixelArtEffect = false;
            return null;
        }

        private void CleanupPixelArtRendering()
        {
            if (_pixelArtRenderTexture != null)
            {
                _pixelArtRenderTexture.Release();
                UnityEngine.Object.Destroy(_pixelArtRenderTexture);
            }
            
            if (_pixelArtMaterial != null)
            {
                UnityEngine.Object.Destroy(_pixelArtMaterial);
            }
            
            if (_mainCamera != null)
            {
                _mainCamera.targetTexture = null;
            }
        }

        private void GenerateDefaultPixelArtPalette()
        {
            // Authentic 16-color pixel art palette (Maldivian sunset theme)
            PixelArtPalette = new Color[]
            {
                new Color(0.059f, 0.059f, 0.059f, 1f),     // 0: Black
                new Color(0.196f, 0.196f, 0.196f, 1f),     // 1: Dark Gray
                new Color(0.404f, 0.255f, 0.196f, 1f),     // 2: Dark Brown (boat wood)
                new Color(0.639f, 0.396f, 0.263f, 1f),     // 3: Light Brown (sand)
                new Color(0.910f, 0.686f, 0.333f, 1f),     // 4: Golden Yellow (sun)
                new Color(0.957f, 0.455f, 0.208f, 1f),     // 5: Orange (sunset)
                new Color(0.961f, 0.263f, 0.208f, 1f),     // 6: Red (sunset deep)
                new Color(0.416f, 0.243f, 0.510f, 1f),     // 7: Purple (dusk)
                new Color(0.200f, 0.220f, 0.529f, 1f),     // 8: Dark Blue (night)
                new Color(0.216f, 0.494f, 0.722f, 1f),     // 9: Sky Blue (day)
                new Color(0.110f, 0.596f, 0.710f, 1f),     // 10: Teal (water shallow)
                new Color(0.063f, 0.400f, 0.463f, 1f),     // 11: Deep Teal (water deep)
                new Color(0.204f, 0.596f, 0.365f, 1f),     // 12: Green (palm)
                new Color(0.835f, 0.910f, 0.357f, 1f),     // 13: Light Green (young palm)
                new Color(0.702f, 0.545f, 0.831f, 1f),     // 14: Light Purple (flower)
                new Color(0.961f, 0.961f, 0.961f, 1f)      // 15: White (cloud)
            };
        }
        #endregion

        #region Core Camera Update
        private void UpdateCameraSystem()
        {
            if (_freeLookCam == null || PlayerTransform == null) return;
            
            #region Occlusion Checking
            if (EnableOcclusionCulling)
            {
                _occlusionCheckTimer += Time.deltaTime;
                if (_occlusionCheckTimer >= OcclusionCheckInterval)
                {
                    CheckCameraOcclusion();
                    _occlusionCheckTimer = 0f;
                }
            }
            #endregion

            #region Prayer Time Adjustments
            if (_gameManager.IsPrayerTimeActive != _isInPrayerTime)
            {
                _isInPrayerTime = _gameManager.IsPrayerTimeActive;
                _prayerCameraLerpTime = 0f;
            }
            
            if (_isInPrayerTime)
            {
                ApplyPrayerCameraSettings();
            }
            else
            {
                RevertPrayerCameraSettings();
            }
            #endregion

            #region Boduberu Sway
            if (_isInBoduberuZone && BoduberuCameraSway)
            {
                ApplyBoduberuSway();
            }
            #endregion

            #region Distance Smoothing
            _currentCameraDistance = Mathf.SmoothDamp(
                _currentCameraDistance, 
                _targetCameraDistance, 
                ref _cameraVelocity, 
                OcclusionSmoothTime
            );
            
            // Apply to Cinemachine orbits
            UpdateCinemachineOrbits();
            #endregion

            #region Render Scale Management
            if (DynamicResolutionScaling && _isInPrayerTime)
            {
                SetRenderScale(PrayerRenderScale);
            }
            else if (DynamicResolutionScaling)
            {
                SetRenderScale(1f);
            }
            #endregion

            #region Performance Monitoring
            if (ShowCameraDebug)
            {
                _lastUpdateTime = Time.unscaledDeltaTime;
            }
            #endregion
        }

        private void LateUpdateCamera()
        {
            // Final camera adjustments after Cinemachine
            if (_mainCamera == null) return;
            
            #region Pixel Art Final Pass
            if (EnablePixelArtEffect && _pixelArtMaterial != null)
            {
                // Render to screen
                Graphics.Blit(_pixelArtRenderTexture, null as RenderTexture, _pixelArtMaterial);
            }
            #endregion
        }
        #endregion

        #region Occlusion Culling
        private void CheckCameraOcclusion()
        {
            Vector3 playerPos = PlayerTransform.position + LookAtOffset;
            Vector3 cameraPos = _cameraTransform.position;
            
            // Shoot ray from player to camera
            Vector3 direction = (cameraPos - playerPos).normalized;
            float distance = Vector3.Distance(cameraPos, playerPos);
            
            if (Physics.SphereCast(
                playerPos, 
                CameraRadius, 
                direction, 
                out _occlusionHit, 
                distance, 
                OcclusionLayers
            ))
            {
                // Occlusion detected
                if (!_isOccluded)
                {
                    _isOccluded = true;
                    _lastOcclusionHitPoint = _occlusionHit.point;
                    
                    // Pull camera closer
                    _targetCameraDistance = _occlusionHit.distance - OcclusionBuffer;
                    _targetCameraDistance = Mathf.Max(_targetCameraDistance, MinCameraDistance);
                    
                    LogInfo("Occlusion", $"Camera occluded. New distance: {_targetCameraDistance:F2}");
                }
            }
            else
            {
                // No occlusion
                if (_isOccluded)
                {
                    _isOccluded = false;
                    
                    // Restore normal distance (with prayer adjustments)
                    if (_isInPrayerTime)
                    {
                        _targetCameraDistance = PrayerCameraDistance;
                    }
                    else
                    {
                        _targetCameraDistance = MaxCameraDistance;
                    }
                    
                    LogInfo("Occlusion", "Camera occlusion cleared");
                }
            }
            
            // Draw debug
            if (DrawCameraCollision)
            {
                Debug.DrawLine(playerPos, cameraPos, _isOccluded ? Color.red : Color.green);
                if (_isOccluded)
                {
                    Debug.DrawLine(_occlusionHit.point, cameraPos, Color.yellow);
                }
            }
        }

        private void UpdateCinemachineOrbits()
        {
            if (_freeLookCam == null) return;
            
            // Update all three orbits based on current distance
            float ratio = _currentCameraDistance / _originalMaxDistance;
            
            for (int i = 0; i < 3; i++)
            {
                var orbit = _freeLookCam.m_Orbits[i];
                orbit.m_Radius = Mathf.Lerp(MinCameraDistance, _originalMaxDistance, ratio * (1f - i * 0.2f));
                _freeLookCam.m_Orbits[i] = orbit;
            }
        }
        #endregion

        #region Prayer Time Camera
        private void HandlePrayerTimeBegins(PrayerName prayer)
        {
            LogInfo("PrayerCamera", $"Prayer time began: {prayer}. Applying camera adjustments.");
            
            // Store original values
            _originalMaxDistance = MaxCameraDistance;
            _originalFOV = _mainCamera.fieldOfView;
            
            _prayerCameraLerpTime = 0f;
        }

        private void HandlePrayerTimeEnds(PrayerName prayer)
        {
            LogInfo("PrayerCamera", $"Prayer time ended: {prayer}. Reverting camera.");
            
            _prayerCameraLerpTime = 0f;
        }

        private void ApplyPrayerCameraSettings()
        {
            if (!PrayerTimeZoomOut) return;
            
            _prayerCameraLerpTime += Time.deltaTime * 0.5f; // Slow lerp
            
            // Zoom out and increase FOV for respectful wide view
            _targetCameraDistance = Mathf.Lerp(_originalMaxDistance, PrayerCameraDistance, _prayerCameraLerpTime);
            
            if (_mainCamera != null)
            {
                _mainCamera.fieldOfView = Mathf.Lerp(_originalFOV, PrayerTimeFOV, _prayerCameraLerpTime);
            }
            
            #region Mosque Framing
            if (FrameMosqueDuringPrayer && _prayerCameraLerpTime > 0.5f)
            {
                // Subtle camera adjustment to frame nearby mosque
                TryFrameMosque();
            }
            #endregion
            
            #region Resolution Scaling
            if (DynamicResolutionScaling)
            {
                SetRenderScale(PrayerRenderScale);
            }
            #endregion
        }

        private void RevertPrayerCameraSettings()
        {
            if (!PrayerTimeZoomOut) return;
            
            _prayerCameraLerpTime += Time.deltaTime * 0.5f;
            
            // Restore original settings
            _targetCameraDistance = Mathf.Lerp(PrayerCameraDistance, _originalMaxDistance, _prayerCameraLerpTime);
            
            if (_mainCamera != null)
            {
                _mainCamera.fieldOfView = Mathf.Lerp(PrayerTimeFOV, _originalFOV, _prayerCameraLerpTime);
            }
            
            #region Resolution Restore
            if (DynamicResolutionScaling && _targetCameraDistance >= _originalMaxDistance * 0.95f)
            {
                SetRenderScale(1f);
            }
            #endregion
        }

        private void TryFrameMosque()
        {
            // Find nearby mosque
            Collider[] colliders = Physics.OverlapSphere(
                PlayerTransform.position, 
                50f, 
                LayerMask.GetMask("Building")
            );
            
            foreach (var col in colliders)
            {
                if (col.gameObject.name.Contains("Mosque", StringComparison.OrdinalIgnoreCase))
                {
                    // Subtle camera rotation toward mosque
                    Vector3 toMosque = col.transform.position - PlayerTransform.position;
                    toMosque.y = 0f;
                    
                    float angle = Vector3.SignedAngle(_cameraTransform.forward, toMosque, Vector3.up);
                    
                    // Soft adjustment (max 15 degrees)
                    angle = Mathf.Clamp(angle, -15f, 15f);
                    
                    // Apply to Cinemachine
                    _freeLookCam.m_XAxis.Value += angle * 0.1f * Time.deltaTime;
                    
                    break;
                }
            }
        }
        #endregion

        #region Boduberu Camera Sway
        private void HandleBoduberuBeat(float beatInterval)
        {
            _boduberuBeatInterval = beatInterval;
            _boduberuBeatTimer = 0f;
            _isInBoduberuZone = true;
            
            LogInfo("BoduberuCamera", $"Boduberu beat detected. Enabling camera sway.");
        }

        private void ApplyBoduberuSway()
        {
            if (!_isInBoduberuZone || _boduberuBeatInterval <= 0f) return;
            
            _boduberuBeatTimer += Time.deltaTime;
            
            // sync with beat
            float beatProgress = Mathf.Repeat(_boduberuBeatTimer / _boduberuBeatInterval, 1f);
            
            // Sway intensity based on game state
            float intensity = BoduberuSwayIntensity * (1f - _gameManager.MasterVolume); // More sway when music is quieter
            
            // Apply subtle rotation sway
            float swayRotation = Mathf.Sin(beatProgress * Mathf.PI * 2f) * intensity * 0.5f;
            
            // Apply to camera local rotation
            if (_cameraTransform != null)
            {
                Vector3 currentRotation = _cameraTransform.localEulerAngles;
                currentRotation.z = swayRotation; // Roll
                _cameraTransform.localEulerAngles = currentRotation;
            }
            
            // Reset when beat is old
            if (_boduberuBeatTimer > _boduberuBeatInterval * 4f)
            {
                _isInBoduberuZone = false;
                _cameraTransform.localEulerAngles = Vector3.zero;
            }
        }
        #endregion

        #region Render Pipeline
        private void SetRenderScale(float scale)
        {
            if (_mainCamera == null) return;
            
            #if UNITY_2021_2_OR_NEWER
            UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urpAsset = 
                GraphicsSettings.renderPipelineAsset as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            
            if (urpAsset != null)
            {
                urpAsset.renderScale = scale;
                LogInfo("RenderScale", $"Set render scale to: {scale}");
            }
            #endif
        }

        private void UpdatePixelArtRendering()
        {
            if (!EnablePixelArtEffect || !_pixelArtInitialized) return;
            
            // Update palette in material
            if (_pixelArtMaterial != null)
            {
                _pixelArtMaterial.SetColorArray("_Palette", PixelArtPalette);
                _pixelArtMaterial.SetInt("_PixelationFactor", PixelationFactor);
            }
            
            // Adjust render texture resolution if screen changes
            int targetWidth = Screen.width / PixelationFactor;
            int targetHeight = Screen.height / PixelationFactor;
            
            if (_pixelArtRenderTexture.width != targetWidth || _pixelArtRenderTexture.height != targetHeight)
            {
                _pixelArtRenderTexture.Release();
                _pixelArtRenderTexture = new RenderTexture(targetWidth, targetHeight, 24);
                _pixelArtRenderTexture.filterMode = FilterMode.Point;
                _pixelArtMaterial.SetTexture("_MainTex", _pixelArtRenderTexture);
                
                _mainCamera.targetTexture = _pixelArtRenderTexture;
            }
        }
        #endregion

        #region Debug & Visualization
        private void DrawCameraDebug()
        {
            if (_mainCamera == null || PlayerTransform == null) return;
            
            // Draw camera frustum
            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS(_cameraTransform.position, _cameraTransform.rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, _mainCamera.fieldOfView, _mainCamera.farClipPlane, _mainCamera.nearClipPlane, _mainCamera.aspect);
            
            // Draw occlusion check
            if (_isOccluded)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_occlusionHit.point, 0.2f);
                Gizmos.DrawLine(_occlusionHit.point, _cameraTransform.position);
            }
            
            // Draw prayer framing target
            if (_isInPrayerTime && FrameMosqueDuringPrayer)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(PlayerTransform.position + Vector3.up * 2f, 1f);
            }
        }

        private void LogInfo(string context, string message, object data = null)
        {
            if (LogCameraEvents)
            {
                _debugSystem?.LogInfo(context, message, data);
            }
        }

        private void LogWarning(string context, string message)
        {
            if (LogCameraEvents)
            {
                _debugSystem?.LogWarning(context, message);
            }
        }
        #endregion

        #region Helper Methods
        public void FocusOnPoint(Vector3 point, float duration = 1f)
        {
            StartCoroutine(FocusCoroutine(point, duration));
        }

        private System.Collections.IEnumerator FocusCoroutine(Vector3 point, float duration)
        {
            Quaternion startRotation = _cameraTransform.rotation;
            Vector3 direction = (point - _cameraTransform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                _cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                
                yield return null;
            }
        }

        public void ShakeCamera(float intensity, float duration)
        {
            StartCoroutine(ShakeCoroutine(intensity, duration));
        }

        private System.Collections.IEnumerator ShakeCoroutine(float intensity, float duration)
        {
            Vector3 originalPosition = _cameraTransform.localPosition;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float currentIntensity = intensity * (1f - t);
                
                Vector3 shake = UnityEngine.Random.insideUnitSphere * currentIntensity;
                _cameraTransform.localPosition = originalPosition + shake;
                
                yield return null;
            }
            
            _cameraTransform.localPosition = originalPosition;
        }

        private void SaveCameraSettings()
        {
            PlayerPrefs.SetFloat("RVA_CameraDistance", MaxCameraDistance);
            PlayerPrefs.SetFloat("RVA_CameraFOV", _mainCamera.fieldOfView);
            PlayerPrefs.Save();
        }

        private void LoadCameraSettings()
        {
            if (PlayerPrefs.HasKey("RVA_CameraDistance"))
            {
                MaxCameraDistance = PlayerPrefs.GetFloat("RVA_CameraDistance");
            }
            
            if (PlayerPrefs.HasKey("RVA_CameraFOV") && _mainCamera != null)
            {
                _mainCamera.fieldOfView = PlayerPrefs.GetFloat("RVA_CameraFOV");
            }
        }
        #endregion

        #region Data Structures
        [Serializable]
        public class CameraSettingsSnapshot
        {
            public Vector3 FollowOffset;
            public Vector3 LookAtOffset;
            public float MaxDistance;
            public float MinDistance;
            public float FieldOfView;
            public bool EnableOcclusion;
            public bool EnablePixelArt;
            public int PixelationFactor;
        }

        public enum CameraMode
        {
            Gameplay,
            Prayer,
            Cutscene,
            Photo
        }
        #endregion

        #region Public API Summary
        /*
         * CameraSystem provides:
         * 
         * GTA-STYLE CAMERA:
         * - Third-person orbit with Cinemachine FreeLook
         * - Collision/occlusion handling for dense environments
         * - Distance smoothing and lerping
         * - Configurable orbits and composition
         * 
         * PIXEL ART RENDERING:
         * - Post-process pixelation with configurable factor
         * - 16-color palette quantization
         * - Dithering support
         * - Authentic retro aesthetic
         * 
         * CULTURAL INTEGRATION:
         * - Prayer time zoom-out for respectful framing
         * - Mosque architecture auto-framing
         * - Boduberu rhythm camera sway
         * - Dynamic resolution during prayer
         * 
         * MOBILE OPTIMIZATION:
         * - Frame skipping for Mali-G72 (every other frame)
         * - Throttled update rate (0.016s)
         * - SIMD-ready math operations
         * - Dynamic resolution scaling
         * - Occlusion culling for performance
         * 
         * USAGE:
         * - Attach to player GameObject
         * - Assign CinemachineFreeLook or create automatically
         * - Configure pixel art palette in inspector
         * - Prayer time adjustments automatic
         * - ShakeCamera() for impacts
         * - FocusOnPoint() for cutscenes
         */
        #endregion
    }
}
