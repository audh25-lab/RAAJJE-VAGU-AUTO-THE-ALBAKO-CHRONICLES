using UnityEngine;
using Unity.Burst;
using System.Collections.Generic;

namespace RVA.TAC.Core {
    /// <summary>
    /// Single entry point that initializes ALL 45 systems in correct order
    /// </summary>
    public class GameBootstrapper : MonoBehaviour {
        [Header("Startup Configuration")]
        public bool skipTutorial = false;
        public bool enableDebugMode = false;
        
        // Singleton instances (all static for runtime access)
        public static MainGameManager GameManager { get; private set; }
        public static SaveSystem SaveSystem { get; private set; }
        public static PlayerController Player { get; private set; }
        public static IslandGenerator World { get; private set; }
        public static MissionSystem Missions { get; private set; }
        public static AudioManager Audio { get; private set; }
        public static UIManager UI { get; private set; }
        
        // Full system registry
        private readonly Dictionary<string, MonoBehaviour> systems = new Dictionary<string, MonoBehaviour>();
        
        void Awake() {
            Debug.Log("[RVA:TAC] Bootstrapping 45 systems...");
            DontDestroyOnLoad(gameObject);
            
            // Phase 1: Core Infrastructure (must be first)
            InitializeCoreSystems();
            
            // Phase 2: World & Data
            InitializeWorldSystems();
            
            // Phase 3: Player & Input
            InitializePlayerSystems();
            
            // Phase 4: Gameplay Mechanics
            InitializeGameplaySystems();
            
            // Phase 5: AI & NPCs
            InitializeNPCSystems();
            
            // Phase 6: UI & UX
            InitializeUISystems();
            
            // Phase 7: Cultural Integration
            InitializeCulturalSystems();
            
            // Phase 8: Audio & VFX
            InitializeAudioVisualSystems();
            
            // Phase 9: Networking & Monetization
            InitializeOnlineSystems();
            
            // Generate procedural world data
            GenerateWorldData();
            
            // Final setup
            CompleteInitialization();
            
            Debug.Log("[RVA:TAC] ✨ ALL SYSTEMS INITIALIZED ✨");
        }
        
        void InitializeCoreSystems() {
            GameManager = AddSystem<MainGameManager>("MainGameManager");
            SaveSystem = AddSystem<SaveSystem>("SaveSystem");
            AddSystem<GameSceneManager>("GameSceneManager");
            AddSystem<VersionControlSystem>("VersionControlSystem");
            AddSystem<DebugSystem>("DebugSystem");
        }
        
        void InitializeWorldSystems() {
            World = AddSystem<IslandGenerator>("IslandGenerator");
            AddSystem<BuildingSystem>("BuildingSystem");
            AddSystem<OceanSystem>("OceanSystem");
            AddSystem<WeatherSystem>("WeatherSystem");
            AddSystem<FloraSystem>("FloraSystem");
            AddSystem<WildlifeSystem>("WildlifeSystem");
            AddSystem<LightingSystem>("LightingSystem");
            AddSystem<ShadowSystem>("ShadowSystem");
        }
        
        void InitializePlayerSystems() {
            Player = AddSystem<PlayerController>("PlayerController");
            AddSystem<TouchInputSystem>("TouchInputSystem");
            AddSystem<InputSystem>("InputSystem");
            AddSystem<CameraSystem>("CameraSystem");
        }
        
        void InitializeGameplaySystems() {
            AddSystem<CombatSystem>("CombatSystem");
            AddSystem<VehicleSystem>("VehicleSystem");
            AddSystem<InventorySystem>("InventorySystem");
            AddSystem<EconomySystem>("EconomySystem");
            AddSystem<SkillSystem>("SkillSystem");
            AddSystem<StealthSystem>("StealthSystem");
            AddSystem<FishingSystem>("FishingSystem");
            AddSystem<BoduberuSystem>("BoduberuSystem");
            AddSystem<PoliceSystem>("PoliceSystem");
        }
        
        void InitializeNPCSystems() {
            AddSystem<CharacterSystem>("CharacterSystem");
            AddSystem<GangSystem>("GangSystem");
            AddSystem<ReputationSystem>("ReputationSystem");
        }
        
        void InitializeUISystems() {
            UI = AddSystem<UIManager>("UIManager");
            AddSystem<UISystem>("UISystem");
            AddSystem<LocalizationSystem>("LocalizationSystem");
            AddSystem<AccessibilitySystem>("AccessibilitySystem");
        }
        
        void InitializeCulturalSystems() {
            AddSystem<PrayerTimeSystem>("PrayerTimeSystem");
            AddSystem<IslamicCalendar>("IslamicCalendar");
            AddSystem<TimeSystem>("TimeSystem");
            AddSystem<AnalyticsSystem>("AnalyticsSystem");
        }
        
        void InitializeAudioVisualSystems() {
            Audio = AddSystem<AudioManager>("AudioManager");
            AddSystem<AudioSystem>("AudioSystem");
            AddSystem<MissionSystem>("MissionSystem");
            AddSystem<DialogueSystem>("DialogueSystem");
            AddSystem<TutorialSystem>("TutorialSystem");
            AddSystem<AchievementSystem>("AchievementSystem");
            AddSystem<ParticleSystem>("ParticleSystem");
        }
        
        void InitializeOnlineSystems() {
            AddSystem<NetworkingSystem>("NetworkingSystem");
            AddSystem<MonetizationSystem>("MonetizationSystem");
        }
        
        T AddSystem<T>(string name) where T : MonoBehaviour {
            var obj = new GameObject(name);
            obj.transform.parent = transform;
            var system = obj.AddComponent<T>();
            systems[name] = system;
            return system;
        }
        
        void GenerateWorldData() {
            Debug.Log("[RVA:TAC] Generating procedural Maldives world...");
            var worldData = MaldivesDataGenerator.GenerateCompleteMaldivesWorld();
            
            // Pass data to systems
            World.SetWorldData(worldData);
            GetSystem<GangSystem>().InitializeGangs(worldData.gangs);
            GetSystem<BuildingSystem>().InitializeBuildings(worldData.buildings);
            
            Debug.Log($"[RVA:TAC] Generated {worldData.islands.Length} islands, {worldData.gangs.Length} gangs, {worldData.buildings.Length} buildings");
        }
        
        void CompleteInitialization() {
            // Cross-system references
            GameManager.SetPlayer(Player);
            UI.SetPlayer(Player);
            Audio.SetPlayer(Player);
            Missions.SetGangSystem(GetSystem<GangSystem>());
            
            // Load or start new game
            if (SaveSystem.SaveExists()) {
                SaveSystem.LoadGame();
            } else {
                StartNewGame();
            }
            
            // Begin main loop
            GameManager.StartMainLoop();
        }
        
        void StartNewGame() {
            Debug.Log("[RVA:TAC] Starting new game...");
            
            // Set starting island (tutorial island)
            int tutorialIslandIndex = 0; // First island
            World.SetCurrentIsland(tutorialIslandIndex);
            
            // Spawn player
            var spawnPos = World.GetPlayerSpawnPosition();
            Player.TeleportTo(spawnPos);
            
            // Initialize tutorial
            if (!skipTutorial) {
                GetSystem<TutorialSystem>().StartPhase(TutorialSystem.TutorialPhase.Movement);
            }
            
            // Unlock first achievement
            GetSystem<AchievementSystem>().UnlockAchievement("GAME_STARTED");
        }
        
        public T GetSystem<T>() where T : MonoBehaviour {
            foreach (var system in systems.Values) {
                if (system is T t) return t;
            }
            return null;
        }
        
        void OnDestroy() {
            SaveSystem.SaveGame(); // Auto-save on exit
        }
    }
}
