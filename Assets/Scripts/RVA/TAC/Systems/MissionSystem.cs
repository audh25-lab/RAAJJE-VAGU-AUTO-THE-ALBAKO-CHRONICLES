using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

// --- Data Structures for Missions ---

public enum ObjectiveStatus { Inactive, Active, Completed }

[System.Serializable]
public abstract class MissionObjective
{
    public string description;
    [HideInInspector]
    public ObjectiveStatus status = ObjectiveStatus.Inactive;

    public abstract void Activate();
    public abstract bool IsComplete();
}

// Example concrete objective types
public class ReachLocationObjective : MissionObjective
{
    public Transform targetLocation;
    public float completionRadius = 5f;
    private Transform playerTransform;

    public override void Activate()
    {
        status = ObjectiveStatus.Active;
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        Debug.Log($"New Objective: {description}");
    }

    public override bool IsComplete()
    {
        if (playerTransform == null) return false;
        return Vector3.Distance(playerTransform.position, targetLocation.position) <= completionRadius;
    }
}

public class CollectItemObjective : MissionObjective
{
    public int itemID;
    public int requiredAmount;
    // Requires an InventorySystem to check item count.

    public override void Activate()
    {
        status = ObjectiveStatus.Active;
        Debug.Log($"New Objective: {description}");
    }

    public override bool IsComplete()
    {
        // return InventorySystem.Instance.GetItemCount(itemID) >= requiredAmount;
        return false; // Placeholder
    }
}

// ScriptableObject to define a mission.
[CreateAssetMenu(fileName = "New Mission", menuName = "RVA/Mission")]
public class MissionData : ScriptableObject
{
    public int missionID;
    public string missionTitle;
    [TextArea(3, 5)]
    public string missionDescription;
    public List<MissionObjective> objectives;
    public int rewardCurrency;
    // Could also have item rewards, reputation changes, etc.
}

// The main MissionSystem
public class MissionSystem : MonoBehaviour
{
    public static MissionSystem Instance { get; private set; }

    public event Action<MissionData> OnMissionStarted;
    public event Action<MissionData> OnMissionCompleted;
    public event Action<MissionObjective> OnObjectiveCompleted;

    [Header("Mission Database")]
    public List<MissionData> allMissions;

    private Dictionary<int, MissionData> missionDatabase = new Dictionary<int, MissionData>();
    private List<int> completedMissionIDs = new List<int>();
    private MissionData activeMission;
    private int currentObjectiveIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
        InitializeMissionDatabase();
    }

    private void Start()
    {
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnSave += SaveMissionData;
            SaveSystem.Instance.OnLoad += LoadMissionData;
        }
    }

    private void OnDestroy()
    {
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnSave -= SaveMissionData;
            SaveSystem.Instance.OnLoad -= LoadMissionData;
        }
    }

    private void InitializeMissionDatabase()
    {
        foreach (var mission in allMissions)
        {
            if (!missionDatabase.ContainsKey(mission.missionID))
            {
                missionDatabase.Add(mission.missionID, mission);
            }
        }
    }

    private void Update()
    {
        if (activeMission != null && currentObjectiveIndex >= 0)
        {
            MissionObjective currentObjective = activeMission.objectives[currentObjectiveIndex];
            if (currentObjective.status == ObjectiveStatus.Active && currentObjective.IsComplete())
            {
                CompleteObjective(currentObjective);
            }
        }
    }

    public void StartMission(int id)
    {
        if (activeMission != null)
        {
            Debug.LogWarning("Cannot start a new mission, one is already active.");
            return;
        }

        if (missionDatabase.TryGetValue(id, out MissionData mission))
        {
            activeMission = mission;
            currentObjectiveIndex = -1;
            
            // Reset all objective statuses
            foreach(var obj in activeMission.objectives)
            {
                obj.status = ObjectiveStatus.Inactive;
            }
            
            Debug.Log($"Starting mission: {mission.missionTitle}");
            OnMissionStarted?.Invoke(mission);
            ActivateNextObjective();
        }
        else
        {
            Debug.LogError($"Mission with ID {id} not found.");
        }
    }

    private void ActivateNextObjective()
    {
        currentObjectiveIndex++;
        if (currentObjectiveIndex < activeMission.objectives.Count)
        {
            activeMission.objectives[currentObjectiveIndex].Activate();
        }
        else
        {
            // All objectives are done, complete the mission
            CompleteMission();
        }
    }

    private void CompleteObjective(MissionObjective objective)
    {
        objective.status = ObjectiveStatus.Completed;
        Debug.Log($"Objective completed: {objective.description}");
        OnObjectiveCompleted?.Invoke(objective);
        ActivateNextObjective();
    }

    private void CompleteMission()
    {
        Debug.Log($"Mission completed: {activeMission.missionTitle}!");

        // Grant rewards
        if (EconomySystem.Instance != null)
        {
            EconomySystem.Instance.AddCurrency(activeMission.rewardCurrency);
        }

        OnMissionCompleted?.Invoke(activeMission);
        activeMission = null;
        currentObjectiveIndex = -1;
    }

    public MissionData GetActiveMission()
    {
        return activeMission;
    }

    // --- Save and Load Integration ---

    public void SaveMissionData(SaveData data)
    {
        if (activeMission != null)
        {
            data.activeMissionID = activeMission.missionID;
            data.currentObjectiveIndex = currentObjectiveIndex;
        }
        else
        {
            data.activeMissionID = -1; // No active mission
        }
        data.completedMissionIDs = new List<int>(completedMissionIDs);
    }

    public void LoadMissionData(SaveData data)
    {
        completedMissionIDs = new List<int>(data.completedMissionIDs);

        if (data.activeMissionID != -1 && missionDatabase.ContainsKey(data.activeMissionID))
        {
            activeMission = missionDatabase[data.activeMissionID];
            currentObjectiveIndex = data.currentObjectiveIndex;

            // Reactivate objectives up to the current one.
            for (int i = 0; i < activeMission.objectives.Count; i++)
            {
                if (i < currentObjectiveIndex)
                {
                    activeMission.objectives[i].status = ObjectiveStatus.Completed;
                }
                else if (i == currentObjectiveIndex)
                {
                    activeMission.objectives[i].Activate();
                }
                else
                {
                    activeMission.objectives[i].status = ObjectiveStatus.Inactive;
                }
            }
            OnMissionStarted?.Invoke(activeMission); // Notify UI to update.
            Debug.Log($"Loaded active mission: {activeMission.missionTitle}");
        }
        else
        {
            activeMission = null;
        }
    }
}
