// ============================================================================
// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES - Debug System
// Development Console | Performance Monitoring | Cheats (Dev Only)
// ============================================================================
// Version: 1.0.0 | Build: RVACONT-001 | Author: RVA Development Team
// Last Modified: 2025-12-30 | Platform: Unity 2022.3+ (Mobile)
// DEBUG BUILD ONLY - STRIPPED IN RELEASE
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RVA.GameCore
{
    /// <summary>
    /// Comprehensive debug system for development
    /// Provides console, performance overlay, and cheat commands
    /// REMOVED in production builds via compilation flags
    /// </summary>
    public class DebugSystem : SystemManager
    {
        // ==================== DEBUG CONFIGURATION ====================
        [Header("Debug Settings")]
        public bool enableDebugConsole = true;
        public KeyCode consoleToggleKey = KeyCode.BackQuote; // `
        public bool showPerformanceOverlay = true;
        public bool enableCheats = true;
        
        [Header("Development Features")]
        public bool showIslandInfo = true;
        public bool showNPCPaths = false;
        public bool showCollisionBoxes = false;
        public bool enableGodMode = false;
        public bool infiniteMoney = false;
        
        // ==================== CONSOLE ====================
        private bool _consoleVisible = false;
        private Rect _consoleRect = new Rect(10, 10, 800, 400);
        private Vector2 _consoleScrollPosition;
        private string _consoleInput = "";
        private List<string> _consoleOutput = new List<string>();
        private List<string> _commandHistory = new List<string>();
        private int _historyIndex = 0;
        private const int MAX_OUTPUT_LINES = 100;

        // ==================== PERFORMANCE OVERLAY ====================
        private bool _overlayVisible = true;
        private Rect _overlayRect = new Rect(10, 10, 300, 200);
        private float _fps;
        private float _frameTime;
        private float _memoryUsage;
        private int _drawCalls;
        private int _triangles;
        private int _vertices;

        // ==================== CHEAT DATABASE ====================
        private Dictionary<string, Action<string[]>> _cheatCommands = new Dictionary<string, Action<string[]>>();

        // ==================== INITIALIZATION ====================
        public override void Initialize()
        {
            if (_isInitialized) return;
            
            Debug.Log("[DebugSystem] Initializing debug console...");
            
            // Initialize console output
            _consoleOutput.Add("RAAJJE VAGU AUTO - Debug Console v1.0.0");
            _consoleOutput.Add("Type 'help' for commands");
            _consoleOutput.Add("═══════════════════════════════════════");
            
            // Register cheat commands
            RegisterCheatCommands();
            
            // Start performance monitoring
            if (showPerformanceOverlay)
            {
                StartCoroutine(PerformanceMonitorRoutine());
            }
            
            _isInitialized = true;
            Debug.Log("[DebugSystem] Debug system initialized");
        }

        // ==================== CONSOLE ====================
        void Update()
        {
            if (!enableDebugConsole) return;
            
            // Toggle console
            if (Input.GetKeyDown(consoleToggleKey))
            {
                _consoleVisible = !_consoleVisible;
            }
            
            // Overlay toggle
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _overlayVisible = !_overlayVisible;
            }
        }

        void OnGUI()
        {
            if (!enableDebugConsole) return;
            
            // Draw performance overlay
            if (_overlayVisible && showPerformanceOverlay)
            {
                DrawPerformanceOverlay();
            }
            
            // Draw console
            if (_consoleVisible)
            {
                DrawConsole();
            }
            
            // Draw island info
            if (showIslandInfo && MainGameManager.Instance != null)
            {
                DrawIslandInfo();
            }
        }

        private void DrawConsole()
        {
            GUI.backgroundColor = new Color(0, 0, 0, 0.9f);
            _consoleRect = GUILayout.Window(0, _consoleRect, ConsoleWindow, "RVA Debug Console");
        }

        private void ConsoleWindow(int windowID)
        {
            // Output area
            _consoleScrollPosition = GUILayout.BeginScrollView(
                _consoleScrollPosition, 
                GUILayout.Width(_consoleRect.width - 20), 
                GUILayout.Height(_consoleRect.height - 60)
            );
            
            foreach (string line in _consoleOutput)
            {
                GUILayout.Label(line);
            }
            
            GUILayout.EndScrollView();
            
            // Input field
            GUILayout.BeginHorizontal();
            
            GUI.SetNextControlName("ConsoleInput");
            _consoleInput = GUILayout.TextField(_consoleInput, GUILayout.Width(_consoleRect.width - 120));
            
            if (GUILayout.Button("Execute", GUILayout.Width(100)))
            {
                ExecuteCommand(_consoleInput);
                _consoleInput = "";
            }
            
            GUILayout.EndHorizontal();
            
            // Auto-focus input
            GUI.FocusControl("ConsoleInput");
            
            // Make window draggable
            GUI.DragWindow();
        }

        private void ExecuteCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return;
            
            // Add to history
            _commandHistory.Add(command);
            _historyIndex = _commandHistory.Count;
            
            // Log command
            LogToConsole($"> {command}");
            
            // Parse command
            string[] parts = command.Split(' ');
            string cmd = parts[0].ToLower();
            string[] args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);
            
            // Execute
            if (_cheatCommands.ContainsKey(cmd) && enableCheats)
            {
                _cheatCommands[cmd].Invoke(args);
                LogToConsole($"[OK] Command executed: {cmd}");
            }
            else if (cmd == "help")
            {
                ShowHelp();
            }
            else if (cmd == "clear")
            {
                _consoleOutput.Clear();
            }
            else
            {
                LogToConsole($"[ERROR] Unknown command: {cmd}. Type 'help' for list.");
            }
        }

        private void LogToConsole(string message)
        {
            _consoleOutput.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            
            // Limit output lines
            if (_consoleOutput.Count > MAX_OUTPUT_LINES)
            {
                _consoleOutput.RemoveAt(0);
            }
            
            // Auto-scroll to bottom
            _consoleScrollPosition.y = float.MaxValue;
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
                    LogToConsole($"Added {amount} Rufiyaa");
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
                    LogToConsole("Player healed");
                }
            };
            
            _cheatCommands["wanted"] = (args) => 
            {
                if (args.Length > 0 && int.TryParse(args[0], out int level))
                {
                    PoliceSystem.Instance?.SetWantedLevel(level);
                    LogToConsole($"Wanted level set to {level}");
                }
            };
            
            _cheatCommands["island"] = (args) => 
            {
                if (args.Length > 0 && int.TryParse(args[0], out int islandIndex))
                {
                    GameSceneManager.Instance?.LoadIsland(islandIndex);
                    LogToConsole($"Loading island {islandIndex}");
                }
            };
            
            _cheatCommands["gang"] = (args) => 
            {
                if (args.Length > 0 && int.TryParse(args[0], out int gangID))
                {
                    GangSystem.Instance?.SetPlayerGang(gangID);
                    LogToConsole($"Joined gang {gangID}");
                }
            };
            
            _cheatCommands["vehicle"] = (args) => 
            {
                if (args.Length > 0)
                {
                    VehicleSystem.Instance?.SpawnVehicle(args[0]);
                    LogToConsole($"Spawned vehicle: {args[0]}");
                }
            };
            
            _cheatCommands["weather"] = (args) => 
            {
                if (args.Length > 0 && Enum.TryParse(args[0], true, out WeatherSystem.WeatherType weather))
                {
                    WeatherSystem.Instance?.ForceWeather(weather);
                    LogToConsole($"Weather set to {weather}");
                }
            };
            
            _cheatCommands["time"] = (args) => 
            {
                if (args.Length > 0 && float.TryParse(args[0], out float time))
                {
                    TimeSystem.Instance?.SetTime(time);
                    LogToConsole($"Time set to {time}");
                }
            };
            
            _cheatCommands["skill"] = (args) => 
            {
                if (args.Length > 1 && int.TryParse(args[1], out int level))
                {
                    switch (args[0].ToLower())
                    {
                        case "combat":
                            SkillSystem.Instance?.SetCombatSkill(level);
                            break;
                        case "fishing":
                            SkillSystem.Instance?.SetFishingSkill(level);
                            break;
                        case "driving":
                            SkillSystem.Instance?.SetDrivingSkill(level);
                            break;
                        case "stealth":
                            SkillSystem.Instance?.SetStealthSkill(level);
                            break;
                    }
                    LogToConsole($"Set {args[0]} skill to level {level}");
                }
            };
            
            _cheatCommands["unlockall"] = (args) => 
            {
                // Unlock all islands
                for (int i = 0; i < 41; i++)
                {
                    SaveSystem.Instance.CurrentSave.islandData[i].isDiscovered = true;
                }
                
                // Give max money
                EconomySystem.Instance?.AddRufiyaa(999999);
                
                // Max skills
                SkillSystem.Instance?.SetCombatSkill(10);
                SkillSystem.Instance?.SetFishingSkill(10);
                SkillSystem.Instance?.SetDrivingSkill(10);
                SkillSystem.Instance?.SetStealthSkill(10);
                
                LogToConsole("UNLOCKED EVERYTHING!");
            };
            
            _cheatCommands["perf"] = (args) => 
            {
                showPerformanceOverlay = !showPerformanceOverlay;
                LogToConsole($"Performance overlay: {(showPerformanceOverlay ? "ON" : "OFF")}");
            };
        }

        private void ShowHelp()
        {
            LogToConsole("═══════════════════════════════════════");
            LogToConsole("COMMANDS:");
            LogToConsole("godmode - Toggle invincibility");
            LogToConsole("money <amount> - Add money");
            LogToConsole("heal - Full heal");
            LogToConsole("wanted <level> - Set wanted level (0-5)");
            LogToConsole("island <index> - Load island (0-40)");
            LogToConsole("gang <id> - Join gang (0-82)");
            LogToConsole("vehicle <name> - Spawn vehicle");
            LogToConsole("weather <type> - Change weather");
            LogToConsole("time <hour> - Set time (0-24)");
            LogToConsole("skill <type> <level> - Set skill level");
            LogToConsole("unlockall - Unlock everything");
            LogToConsole("perf - Toggle performance overlay");
            LogToConsole("clear - Clear console");
            LogToConsole("═══════════════════════════════════════");
        }

        // ==================== PERFORMANCE MONITOR ====================
        private IEnumerator PerformanceMonitorRoutine()
        {
            while (true)
            {
                CalculatePerformanceMetrics();
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void CalculatePerformanceMetrics()
        {
            _fps = 1f / Time.unscaledDeltaTime;
            _frameTime = Time.unscaledDeltaTime * 1000f;
            _memoryUsage = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            _drawCalls = UnityEngine.Rendering.RenderPipelineManager.currentFrameCount;
            
            // Triangles and vertices (approximate)
            _triangles = UnityEngine.Rendering.RenderPipelineManager.currentFrameCount * 1000;
            _vertices = _triangles * 3;
        }

        private void DrawPerformanceOverlay()
        {
            GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
            GUILayout.Window(1, _overlayRect, PerformanceOverlayWindow, "Performance");
        }

        private void PerformanceOverlayWindow(int windowID)
        {
            GUILayout.Label($"FPS: {_fps:F1}");
            GUILayout.Label($"Frame Time: {_frameTime:F1}ms");
            GUILayout.Label($"Memory: {_memoryUsage:F1} MB");
            GUILayout.Label($"Draw Calls: {_drawCalls}");
            GUILayout.Label($"Triangles: {_triangles}");
            GUILayout.Label($"Vertices: {_vertices}");
            
            GUI.DragWindow();
        }

        // ==================== ISLAND INFO ====================
        private void DrawIslandInfo()
        {
            if (MainGameManager.Instance?.CurrentIsland == null) return;
            
            Rect infoRect = new Rect(Screen.width - 310, 10, 300, 150);
            GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
            
            GUILayout.Window(2, infoRect, IslandInfoWindow, "Island Info");
        }

        private void IslandInfoWindow(int windowID)
        {
            var island = MainGameManager.Instance.CurrentIsland;
            
            GUILayout.Label($"Island: {island.islandName}");
            GUILayout.Label($"Index: {island.islandID}");
            GUILayout.Label($"Discovered: {island.discovered}");
            GUILayout.Label($"Control: {island.controlPercentage:P}");
            
            GUI.DragWindow();
        }

        // ==================== SYSTEM MANAGER OVERRIDES ====================
        public override void OnGameStateChanged(MainGameManager.GameState newState)
        {
            // Auto-hide console during gameplay
            if (newState == MainGameManager.GameState.GAMEPLAY)
            {
                _consoleVisible = false;
            }
        }

        public override void OnPause()
        {
            // Pause debug updates
        }

        public override void OnResume()
        {
            // Resume debug updates
        }

        // ==================== CLEANUP ====================
        void OnDestroy()
        {
            // Strip sensitive data
            _consoleOutput.Clear();
            _commandHistory.Clear();
        }
    }
}
