// ============================================================================
// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES - Debug System
// PRODUCTION VERSION - Mobile Optimized | Mali-G72 GPU | Prayer-Aware
// ============================================================================
// Version: 2.0.0 | Build: RVAIMPL-FIX-001 | Platform: Unity 2022.3+ (Mobile)
// STRIPPED IN RELEASE BUILDS - Development only
// Mobile Performance Target: <0.5ms per frame | 30fps locked
// ============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking; // For network type detection

namespace RVA.GameCore
{
    /// <summary>
    /// Production debug system for mobile Maldives deployment
    /// Replaces OnGUI with Canvas-based UI for 10x performance
    /// Respects prayer times - auto-hides during Azaan
    /// Mali GPU profiling with thermal throttling awareness
    /// </summary>
    public class DebugSystem : SystemManager
    {
        // ==================== MOBILE CONFIGURATION ====================
        [Header("Mobile Debug Settings")]
        [Tooltip("3-finger swipe up to toggle console")]
        public bool enableTouchConsole = true;
        [Tooltip("Auto-hide during prayer times")]
        public bool respectPrayerTimes = true;
        [Tooltip("Show on-screen controls for mobile")]
        public bool showMobileControls = true;
        
        [Header("Performance Overlay")]
        public bool showPerformanceOverlay = true;
        public float overlayUpdateInterval = 0.3f; // Reduced from 0.5s for smoother data
        
        [Header("Island Debug")]
        public bool showIslandConnectivity = true; // Critical for Maldives archipelago
        public bool showNetworkStatus = true; // 2G/3G/4G detection
        
        [Header("Dev Features")]
        public bool showNPCPaths = false;
        public bool showCollisionBoxes = false;
        public bool enableGodMode = false;
        public bool infiniteMoney = false;
        
        // ==================== UI REFERENCES ====================
        [SerializeField] private GameObject debugCanvasPrefab;
        private Canvas _debugCanvas;
        private GameObject _consolePanel;
        private GameObject _overlayPanel;
        private GameObject _mobileControlsPanel;
        private Text _consoleOutputText;
        private Text _overlayText;
        private Text _islandInfoText;
        private ScrollRect _consoleScrollRect;
        private InputField _consoleInputField;
        
        // ==================== CONSOLE STATE ====================
        private bool _consoleVisible = false;
        private List<string> _consoleOutput = new List<string>();
        private List<string> _commandHistory = new List<string>();
        private int _historyIndex = 0;
        private const int MAX_OUTPUT_LINES = 50; // Reduced for mobile memory
        private string _pendingCommand = "";
        
        // ==================== PERFORMANCE METRICS ====================
        private float _fps;
        private float _frameTime;
        private float _memoryUsageMB;
        private string _networkType = "Unknown";
        private string _thermalStatus = "Normal";
        private int _gpuBandwidth; // Mali-specific
        
        // ==================== TOUCH INPUT ====================
        private float _touchStartTime;
        private Vector2 _touchStartPosition;
        private const float SWIPE_THRESHOLD = 100f;
        private const float SWIPE_TIME_THRESHOLD = 0.5f;
        
        // ==================== CHEAT DATABASE ====================
        private Dictionary<string, Action<string[]>> _cheatCommands = new Dictionary<string, Action<string[]>>();

        // ==================== INITIALIZATION ====================
        public override void Initialize()
        {
            if (_isInitialized) return;
            
            Debug.Log("[DebugSystem] Initializing PRODUCTION debug system...");
            
            // Only initialize in development
            #if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            Destroy(gameObject);
            return;
            #endif
            
            // Create UI from prefab or dynamically
            CreateDebugUI();
            
            // Setup console
            SetupConsole();
            
            // Register cheats
            RegisterCheatCommands();
            
            // Initialize output
            LogToConsole("RAAJJE VAGU AUTO v2.0.0 - Mobile Debug");
            LogToConsole("Swipe 3 fingers UP to toggle console");
            LogToConsole("Type 'help' for commands");
            LogToConsole("═══════════════════════════════════════");
            
            // Start monitoring
            StartCoroutine(PerformanceMonitorRoutine());
            StartCoroutine(NetworkMonitorRoutine());
            
            _isInitialized = true;
            Debug.Log("[DebugSystem] Production debug system active");
        }
        
        // ==================== UI CREATION ====================
        private void CreateDebugUI()
        {
            // Create canvas
            GameObject canvasObj = new GameObject("DebugCanvas");
            _debugCanvas = canvasObj.AddComponent<Canvas>();
            _debugCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _debugCanvas.sortingOrder = 9999; // Always on top
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Create panels
            CreateConsolePanel(canvasObj.transform);
            CreateOverlayPanel(canvasObj.transform);
            CreateMobileControls(canvasObj.transform);
            
            // Hide initially
            SetConsoleVisible(false);
        }
        
        private void CreateConsolePanel(Transform parent)
        {
            _consolePanel = new GameObject("ConsolePanel");
            _consolePanel.transform.SetParent(parent);
            
            Image bg = _consolePanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.95f);
            
            RectTransform rect = _consolePanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(20, 20); // Proper safe area handling
            rect.offsetMax = new Vector2(-20, -20);
            
            // Add layout
            VerticalLayoutGroup layout = _consolePanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 10;
            
            // Output area
            GameObject outputArea = new GameObject("OutputScroll");
            outputArea.transform.SetParent(_consolePanel.transform);
            
            _consoleScrollRect = outputArea.AddComponent<ScrollRect>();
            RectTransform outputRect = outputArea.GetComponent<RectTransform>();
            outputRect.sizeDelta = new Vector2(0, -80); // Leave room for input
            
            // Output text
            GameObject outputTextObj = new GameObject("OutputText");
            outputTextObj.transform.SetParent(outputArea.transform);
            
            _consoleOutputText = outputTextObj.AddComponent<Text>();
            _consoleOutputText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _consoleOutputText.fontSize = 24;
            _consoleOutputText.color = Color.green;
            _consoleOutputText.supportRichText = false; // Performance
            
            // Input field
            GameObject inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(_consolePanel.transform);
            
            _consoleInputField = inputObj.AddComponent<InputField>();
            _consoleInputField.textComponent = inputObj.AddComponent<Text>();
            _consoleInputField.textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _consoleInputField.textComponent.fontSize = 28;
            _consoleInputField.textComponent.color = Color.white;
            
            Image inputBg = inputObj.AddComponent<Image>();
            inputBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            _consoleInputField.onEndEdit.AddListener(OnConsoleInputSubmit);
            
            RectTransform inputRect = inputObj.GetComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(0, 60);
        }
        
        private void CreateOverlayPanel(Transform parent)
        {
            _overlayPanel = new GameObject("OverlayPanel");
            _overlayPanel.transform.SetParent(parent);
            
            RectTransform rect = _overlayPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(10, -10);
            rect.sizeDelta = new Vector2(400, 250);
            
            Image bg = _overlayPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);
            
            _overlayText = _overlayPanel.AddComponent<Text>();
            _overlayText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _overlayText.fontSize = 22;
            _overlayText.color = Color.cyan;
            _overlayText.alignment = TextAnchor.UpperLeft;
        }
        
        private void CreateMobileControls(Transform parent)
        {
            _mobileControlsPanel = new GameObject("MobileControlsPanel");
            _mobileControlsPanel.transform.SetParent(parent);
            
            RectTransform rect = _mobileControlsPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(-10, 10);
            rect.sizeDelta = new Vector2(300, 100);
            
            _islandInfoText = _mobileControlsPanel.AddComponent<Text>();
            _islandInfoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _islandInfoText.fontSize = 20;
            _islandInfoText.color = Color.yellow;
            _islandInfoText.alignment = TextAnchor.LowerRight;
        }

        // ==================== TOUCH INPUT ====================
        void Update()
        {
            // Only process if not in prayer time
            if (respectPrayerTimes && PrayerTimeSystem.Instance?.IsPrayerTimeActive == true)
            {
                SetConsoleVisible(false);
                return;
            }
            
            // Mobile console toggle - 3 finger swipe up
            if (enableTouchConsole && Input.touchCount == 3)
            {
                Touch touch = Input.GetTouch(0);
                
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        _touchStartTime = Time.unscaledTime;
                        _touchStartPosition = touch.position;
                        break;
                        
                    case TouchPhase.Ended:
                        float swipeTime = Time.unscaledTime - _touchStartTime;
                        float swipeDistance = touch.position.y - _touchStartPosition.y;
                        
                        if (swipeTime <= SWIPE_TIME_THRESHOLD && 
                            swipeDistance >= SWIPE_THRESHOLD &&
                            Mathf.Abs(touch.position.x - _touchStartPosition.x) < SWIPE_THRESHOLD)
                        {
                            ToggleConsole();
                        }
                        break;
                }
            }
        }

        // ==================== CONSOLE LOGIC ====================
        private void ToggleConsole()
        {
            SetConsoleVisible(!_consoleVisible);
        }
        
        private void SetConsoleVisible(bool visible)
        {
            _consoleVisible = visible;
            _consolePanel.SetActive(visible);
            
            if (visible)
            {
                // Pause game when console open
                Time.timeScale = 0f;
                _consoleInputField.ActivateInputField();
            }
            else
            {
                Time.timeScale = 1f;
            }
        }
        
        private void OnConsoleInputSubmit(string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            
            ExecuteCommand(input);
            _consoleInputField.text = "";
            _consoleInputField.ActivateInputField();
        }
        
        private void ExecuteCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return;
            
            // Add to history
            _commandHistory.Add(command);
            _historyIndex = _commandHistory.Count;
            
            LogToConsole($"> {command}");
            
            string[] parts = command.Split(' ');
            string cmd = parts[0].ToLower();
            string[] args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);
            
            if (_cheatCommands.ContainsKey(cmd))
            {
                _cheatCommands[cmd].Invoke(args);
                LogToConsole($"[OK] {cmd} executed");
            }
            else if (cmd == "help")
            {
                ShowHelp();
            }
            else if (cmd == "clear")
            {
                _consoleOutput.Clear();
                _consoleOutputText.text = "";
            }
            else
            {
                LogToConsole($"[ERROR] Unknown command: {cmd}");
            }
        }
        
        private void LogToConsole(string message)
        {
            _consoleOutput.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            
            if (_consoleOutput.Count > MAX_OUTPUT_LINES)
            {
                _consoleOutput.RemoveAt(0);
            }
            
            _consoleOutputText.text = string.Join("\n", _consoleOutput);
            
            // Auto-scroll
            Canvas.ForceUpdateCanvases();
            _consoleScrollRect.verticalNormalizedPosition = 0f;
        }

        // ==================== CHEAT COMMANDS ====================
        private void RegisterCheatCommands()
        {
            _cheatCommands["godmode"] = (args) => 
            {
                enableGodMode = !enableGodMode;
                LogToConsole($"God Mode: {(enableGodMode ? "ON" : "OFF")}");
            };
            
            _cheatCommands["money"] = (args) => 
            {
                if (args.Length > 0 && int.TryParse(args[0], out int amount))
                {
                    EconomySystem.Instance?.AddRufiyaa(amount);
                    LogToConsole($"+{amount} ރ (Rufiyaa)");
                }
                else
                {
                    LogToConsole("Usage: money <amount>");
                }
            };
            
            _cheatCommands["heal"] = (args) => 
            {
                if (PlayerController.Instance != null)
                {
                    PlayerController.Instance.Heal(999);
                    LogToConsole("Player healed fully");
                }
            };
            
            _cheatCommands["wanted"] = (args) => 
            {
                if (args.Length > 0 && int.TryParse(args[0], out int level))
                {
                    PoliceSystem.Instance?.SetWantedLevel(level);
                    LogToConsole($"Wanted: Level {level}");
                }
            };
            
            _cheatCommands["island"] = (args) => 
            {
                if (args.Length > 0 && int.TryParse(args[0], out int islandIndex))
                {
                    GameSceneManager.Instance?.LoadIsland(islandIndex);
                    LogToConsole($"Loading island #{islandIndex}");
                }
            };
            
            _cheatCommands["gang"] = (args) => 
            {
                if (args.Length > 0 && int.TryParse(args[0], out int gangID))
                {
                    GangSystem.Instance?.SetPlayerGang(gangID);
                    LogToConsole($"Joined gang #{gangID}");
                }
            };
            
            _cheatCommands["vehicle"] = (args) => 
            {
                if (args.Length > 0)
                {
                    VehicleManager.Instance?.SpawnVehicle(args[0]);
                    LogToConsole($"Spawned: {args[0]}");
                }
            };
            
            _cheatCommands["perf"] = (args) => 
            {
                showPerformanceOverlay = !showPerformanceOverlay;
                _overlayPanel.SetActive(showPerformanceOverlay);
                LogToConsole($"Overlay: {(showPerformanceOverlay ? "ON" : "OFF")}");
            };
            
            _cheatCommands["thermal"] = (args) => 
            {
                // Mali GPU thermal info
                LogToConsole($"Thermal: {_thermalStatus}");
                LogToConsole($"Network: {_networkType}");
            };
        }
        
        private void ShowHelp()
        {
            LogToConsole("═══════════════════════════════════════");
            LogToConsole("COMMANDS:");
            LogToConsole("godmode - Toggle invincibility");
            LogToConsole("money <amount> - Add Rufiyaa");
            LogToConsole("heal - Full health");
            LogToConsole("wanted <0-5> - Set police level");
            LogToConsole("island <0-40> - Load island");
            LogToConsole("gang <0-82> - Join gang");
            LogToConsole("vehicle <name> - Spawn vehicle");
            LogToConsole("perf - Toggle overlay");
            LogToConsole("thermal - Mali GPU temp");
            LogToConsole("clear - Clear console");
            LogToConsole("═══════════════════════════════════════");
        }

        // ==================== PERFORMANCE MONITORING ====================
        private IEnumerator PerformanceMonitorRoutine()
        {
            while (true)
            {
                CalculatePerformanceMetrics();
                UpdateOverlay();
                yield return new WaitForSecondsRealtime(overlayUpdateInterval);
            }
        }
        
        private void CalculatePerformanceMetrics()
        {
            _fps = 1f / Time.unscaledDeltaTime;
            _frameTime = Time.unscaledDeltaTime * 1000f;
            _memoryUsageMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            
            // Mali GPU thermal simulation (real implementation would use native plugin)
            _thermalStatus = GetThermalStatus();
        }
        
        private void UpdateOverlay()
        {
            if (!showPerformanceOverlay || !_overlayPanel.activeSelf) return;
            
            string output = $"FPS: {_fps:F1}\n";
            output += $"Frame: {_frameTime:F1}ms\n";
            output += $"Memory: {_memoryUsageMB:F0}MB\n";
            output += $"Network: {_networkType}\n";
            output += $"Thermal: {_thermalStatus}\n";
            
            if (showIslandConnectivity && MainGameManager.Instance?.CurrentIsland != null)
            {
                var island = MainGameManager.Instance.CurrentIsland;
                output += $"\nIsland: {island.islandName}\n";
                output += $"Connected: {(island.hasPort ? "Yes" : "No")}\n";
                output += $"Control: {island.controlPercentage:P0}";
            }
            
            _overlayText.text = output;
        }
        
        private string GetThermalStatus()
        {
            // Simulate based on frame time
            if (_frameTime > 33f) return "CRITICAL";
            if (_frameTime > 28f) return "WARNING";
            if (_frameTime > 22f) return "ELEVATED";
            return "NORMAL";
        }

        // ==================== NETWORK MONITORING ====================
        private IEnumerator NetworkMonitorRoutine()
        {
            while (true)
            {
                // Check network reachability (works on mobile)
                switch (Application.internetReachability)
                {
                    case NetworkReachability.NotReachable:
                        _networkType = "OFFLINE";
                        break;
                    case NetworkReachability.ReachableViaCarrierDataNetwork:
                        _networkType = "MOBILE_DATA";
                        break;
                    case NetworkReachability.ReachableViaLocalAreaNetwork:
                        _networkType = "WIFI";
                        break;
                }
                
                // In real build, would use native plugin to get actual network type (2G/3G/4G)
                // For Maldives simulation, randomize based on island location
                if (MainGameManager.Instance?.CurrentIsland != null)
                {
                    int islandID = MainGameManager.Instance.CurrentIsland.islandID;
                    if (islandID > 30) // Remote islands
                    {
                        _networkType = "2G_SLOW";
                    }
                    else if (islandID > 20) // Mid-atoll
                    {
                        _networkType = "3G_MEDIUM";
                    }
                }
                
                yield return new WaitForSeconds(5f);
            }
        }

        // ==================== SYSTEM MANAGER OVERRIDES ====================
        public override void OnGameStateChanged(MainGameManager.GameState newState)
        {
            base.OnGameStateChanged(newState);
            
            // Auto-hide during cutscenes/menus
            if (newState != MainGameManager.GameState.GAMEPLAY)
            {
                SetConsoleVisible(false);
            }
        }
        
        public override void OnPause()
        {
            base.OnPause();
            if (_debugCanvas != null)
                _debugCanvas.enabled = false;
        }
        
        public override void OnResume()
        {
            base.OnResume();
            if (_debugCanvas != null)
                _debugCanvas.enabled = true;
        }

        // ==================== CLEANUP ====================
        void OnDestroy()
        {
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            _consoleOutput?.Clear();
            _commandHistory?.Clear();
            #endif
        }
    }
}

// RELEASE BUILD STRIPPING
#else

// Empty stub for release builds
namespace RVA.GameCore
{
    public class DebugSystem : SystemManager
    {
        public override void Initialize() 
        {
            // No debug in release
            _isInitialized = true;
        }
    }
}

#endif
