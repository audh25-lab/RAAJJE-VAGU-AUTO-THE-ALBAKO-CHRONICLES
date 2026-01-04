using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MissionData is a ScriptableObject that holds all the static data for a single mission.
/// This data-driven approach allows for easy creation and balancing of quests.
/// </summary>
[CreateAssetMenu(fileName = "New Mission", menuName = "RVA/Mission Data")]
public class MissionData : ScriptableObject
{
    [Header("Mission Info")]
    public string missionID;
    public string missionName;
    [TextArea] public string description;
    public bool isStoryMission = true;

    [Header("Objectives")]
    public List<MissionObjective> objectives;

    [Header("Rewards")]
    public RewardData reward;
}

[System.Serializable]
public class MissionObjective
{
    public string description;
    public ObjectiveType type;
    public Transform targetLocation; // For "GoTo" objectives
    public ItemData itemToCollect;   // For "Collect" objectives
    public int quantityToCollect;

    [HideInInspector] public bool isComplete = false;
}

public enum ObjectiveType
{
    GoTo,
    Collect,
    Defeat
}
