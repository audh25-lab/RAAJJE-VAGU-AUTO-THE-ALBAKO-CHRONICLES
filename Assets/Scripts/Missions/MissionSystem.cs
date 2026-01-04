using UnityEngine;
using System;

/// <summary>
/// MissionSystem is a component that represents a single, self-contained mission.
/// This production-ready version uses a robust objective tracking system and C# events
/// for decoupled communication with other game systems.
/// </summary>
public class MissionSystem : MonoBehaviour
{
    [Header("Mission Data")]
    public MissionData missionData;

    [Header("State")]
    public MissionState currentState = MissionState.Inactive;
    private int currentObjectiveIndex = 0;

    // --- Events ---
    public static event Action<MissionSystem> OnMissionStarted;
    public static event Action<MissionSystem> OnMissionCompleted;
    public static event Action<MissionSystem> OnMissionFailed;
    public static event Action<MissionSystem> OnObjectiveAdvanced;

    public enum MissionState { Inactive, Active, Completed, Failed }

    public void StartMission()
    {
        if (currentState != MissionState.Inactive) return;

        currentState = MissionState.Active;
        currentObjectiveIndex = 0;

        OnMissionStarted?.Invoke(this);
        StartCurrentObjective();
    }

    public void AdvanceObjective()
    {
        if (currentState != MissionState.Active) return;

        GetCurrentObjective().isComplete = true;
        currentObjectiveIndex++;

        if (currentObjectiveIndex >= missionData.objectives.Count)
        {
            CompleteMission();
        }
        else
        {
            StartCurrentObjective();
            OnObjectiveAdvanced?.Invoke(this);
        }
    }

    private void StartCurrentObjective()
    {
        // Logic to activate the objective (e.g., show a marker on the map)
        // For a GoTo objective, we can tell the MapSystem to show an icon.
        var objective = GetCurrentObjective();
        if (objective.type == ObjectiveType.GoTo && objective.targetLocation != null)
        {
            MapSystem.Instance?.SetObjectiveIcon(objective.targetLocation.position);
        }
    }

    private void CompleteMission()
    {
        currentState = MissionState.Completed;
        MapSystem.Instance?.HideObjectiveIcon();

        // Grant rewards via the RewardSystem
        if (missionData.reward != null)
        {
            RewardSystem.Instance.GrantReward(gameObject, missionData.reward); // Assuming player starts the mission
        }

        OnMissionCompleted?.Invoke(this);
    }

    public void FailMission()
    {
        if (currentState != MissionState.Active) return;

        currentState = MissionState.Failed;
        MapSystem.Instance?.HideObjectiveIcon();
        OnMissionFailed?.Invoke(this);
    }

    public MissionObjective GetCurrentObjective()
    {
        if (missionData != null && currentObjectiveIndex < missionData.objectives.Count)
        {
            return missionData.objectives[currentObjectiveIndex];
        }
        return null;
    }

    // This would be called by other systems, e.g., a trigger volume at a location.
    public void CheckObjectiveCompletion(ObjectiveType type, Transform target = null)
    {
        var objective = GetCurrentObjective();
        if(objective != null && objective.type == type)
        {
            if(type == ObjectiveType.GoTo && Vector3.Distance(transform.position, objective.targetLocation.position) < 3f)
            {
                AdvanceObjective();
            }
        }
    }
}
