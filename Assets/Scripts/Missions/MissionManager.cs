using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MissionManager is the master controller for the game's narrative. It uses an
/// event-driven architecture to track mission progress, manage the main story flow,
/// and coordinate with the UI and Quest Log systems.
/// </summary>
public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance;

    [Header("Story Progression")]
    [SerializeField] private List<MissionData> mainStoryMissions;
    public int currentStoryMissionIndex { get; private set; } = 0;

    [Header("Active Missions")]
    private List<MissionSystem> activeMissions = new List<MissionSystem>();

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
        }
    }

    void OnEnable()
    {
        // Subscribe to events from the MissionSystem class
        MissionSystem.OnMissionStarted += OnMissionStarted;
        MissionSystem.OnMissionCompleted += OnMissionCompleted;
        MissionSystem.OnMissionFailed += OnMissionFailed;
        MissionSystem.OnObjectiveAdvanced += OnObjectiveAdvanced;
    }

    void OnDisable()
    {
        MissionSystem.OnMissionStarted -= OnMissionStarted;
        MissionSystem.OnMissionCompleted -= OnMissionCompleted;
        MissionSystem.OnMissionFailed -= OnMissionFailed;
        MissionSystem.OnObjectiveAdvanced -= OnObjectiveAdvanced;
    }

    public void StartNextStoryMission()
    {
        if (currentStoryMissionIndex < mainStoryMissions.Count)
        {
            MissionData nextMissionData = mainStoryMissions[currentStoryMissionIndex];

            // Find the MissionSystem component in the world that corresponds to this mission data.
            // This assumes that such a component exists and is ready.
            // A more robust implementation might use a dictionary to register all missions.
            foreach (var mission in FindObjectsOfType<MissionSystem>())
            {
                if (mission.missionData == nextMissionData)
                {
                    mission.StartMission();
                    return;
                }
            }
        }
        else
        {
            Debug.Log("Main story has been completed!");
        }
    }

    private void OnMissionStarted(MissionSystem mission)
    {
        if (!activeMissions.Contains(mission))
        {
            activeMissions.Add(mission);
            QuestLogSystem.Instance?.AddMission(mission.missionData);
            UpdateMissionUI(mission);
        }
    }

    private void OnMissionCompleted(MissionSystem mission)
    {
        if (activeMissions.Contains(mission))
        {
            activeMissions.Remove(mission);
            QuestLogSystem.Instance?.CompleteMission(mission.missionData);
        }

        // If the completed mission was part of the main story, advance the story.
        if (mainStoryMissions.Contains(mission.missionData))
        {
            currentStoryMissionIndex++;
            StartNextStoryMission();
        }

        // If no other missions are active, hide the UI.
        if(activeMissions.Count == 0)
        {
            UIManager.Instance?.HideMissionDisplay();
        }
    }

    private void OnMissionFailed(MissionSystem mission)
    {
        if (activeMissions.Contains(mission))
        {
            activeMissions.Remove(mission);
            // Optionally, update the quest log to show a failed state.
        }
    }

    private void OnObjectiveAdvanced(MissionSystem mission)
    {
        // Update the UI with the new objective description.
        UpdateMissionUI(mission);
    }

    private void UpdateMissionUI(MissionSystem mission)
    {
        if (UIManager.Instance != null && mission != null && mission.GetCurrentObjective() != null)
        {
            UIManager.Instance.UpdateMissionDisplay(mission.missionData.missionName, mission.GetCurrentObjective().description);
        }
    }
}
