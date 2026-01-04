using UnityEngine;
using TMPro;

/// <summary>
/// UIManager is the central controller for all UI elements. This production-ready version
/// is event-driven and acts as a high-level manager that delegates specific UI tasks
/// (like HUD updates) to specialized sub-systems.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI Systems")]
    [SerializeField] private HUDSystem hudSystem;
    [SerializeField] private QuestLogSystem questLogSystem;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private MapSystem mapSystem;

    [Header("Core Panels")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject deathScreen;

    // --- Private State ---
    private PlayerController player;
    private WorldManager worldManager;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Cache references to the player and world manager for performance.
        player = FindObjectOfType<PlayerController>();
        worldManager = WorldManager.Instance;

        // Ensure all panels are in their correct default state.
        if (pauseMenu != null) pauseMenu.SetActive(false);
        if (deathScreen != null) deathScreen.SetActive(false);
    }

    void OnEnable()
    {
        // Subscribe to game events
        // EventManager.Instance.Subscribe("OnPlayerDeath", ShowDeathScreen);
    }

    void OnDisable()
    {
        // Unsubscribe from events
        // EventManager.Instance.Unsubscribe("OnPlayerDeath", ShowDeathScreen);
    }

    void Update()
    {
        // The UIManager's primary role in Update is to pass data to its sub-systems.
        if (hudSystem != null && hudSystem.gameObject.activeInHierarchy)
        {
            hudSystem.UpdateHUD(player, worldManager);
        }

        // Handle global UI inputs like the pause menu.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePauseMenu();
        }
    }

    /// <summary>
    /// Updates the on-screen mission display.
    /// </summary>
    public void UpdateMissionDisplay(string title, string objective)
    {
        // This is a responsibility of the UIManager, though it could be a separate component.
    }

    public void HideMissionDisplay()
    {
        // ...
    }

    public void SetHUDVisible(bool isVisible)
    {
        if (hudSystem != null)
        {
            hudSystem.SetVisible(isVisible);
        }
    }

    public void TogglePauseMenu()
    {
        bool isPaused = !pauseMenu.activeSelf;
        pauseMenu.SetActive(isPaused);
        MainGameManager.Instance.SetGameState(isPaused ? MainGameManager.GameState.Paused : MainGameManager.GameState.Playing);
    }

    private void ShowDeathScreen()
    {
        SetHUDVisible(false);
        if (deathScreen != null)
        {
            deathScreen.SetActive(true);
        }
    }
}
