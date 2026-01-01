using UnityEngine;
using System;
using System.Collections.Generic;

// --- Data Structures for LiveOps ---

[System.Serializable]
public class BattlePassTier
{
    public int xpRequired;
    public string rewardDescription; // In a real game, this would be an item, currency, etc.
}

[CreateAssetMenu(fileName = "New Battle Pass", menuName = "RVA/Battle Pass")]
public class BattlePassData : ScriptableObject
{
    public int battlePassID;
    public string title;
    public List<BattlePassTier> tiers;
}

[CreateAssetMenu(fileName = "New Live Event", menuName = "RVA/Live Event")]
public class LiveOpsEventData : ScriptableObject
{
    public int eventID;
    public string eventName;
    [TextArea(3, 5)]
    public string eventDescription;

    // In a real system, you would use proper DateTime, but we'll use in-game days for simplicity.
    public int startDay;
    public int endDay;
    public int startMonth;
    public int endMonth;
}

// The main LiveOpsSystem
public class LiveOpsSystem : MonoBehaviour
{
    public static LiveOpsSystem Instance { get; private set; }

    public event Action<LiveOpsEventData> OnEventStarted;
    public event Action<LiveOpsEventData> OnEventEnded;
    public event Action<int, int> OnBattlePassXPChanged; // currentXP, currentTier

    [Header("Databases")]
    public List<LiveOpsEventData> allEvents;
    public BattlePassData activeBattlePass;

    private List<LiveOpsEventData> activeEvents = new List<LiveOpsEventData>();
    private IslamicCalendarSystem calendarSystem;

    // Battle Pass State
    private int currentBattlePassXP = 0;
    private int currentBattlePassTier = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        calendarSystem = FindObjectOfType<IslamicCalendarSystem>();
        if (calendarSystem == null)
        {
            Debug.LogError("LiveOpsSystem requires an IslamicCalendarSystem to function.");
            this.enabled = false;
            return;
        }

        // Check for events on each new day.
        calendarSystem.OnNewDay += (date) => CheckForEvents(date);
    }

    private void CheckForEvents(IslamicCalendarSystem.IslamicDate date)
    {
        // Check for new events starting today.
        foreach (var ev in allEvents)
        {
            if (!activeEvents.Contains(ev) && date.Day == ev.startDay && date.Month == ev.startMonth)
            {
                StartEvent(ev);
            }
        }

        // Check for events ending today.
        // We iterate backwards because we might modify the list.
        for(int i = activeEvents.Count - 1; i >= 0; i--)
        {
            var ev = activeEvents[i];
            if (date.Day == ev.endDay && date.Month == ev.endMonth)
            {
                EndEvent(ev);
            }
        }
    }

    private void StartEvent(LiveOpsEventData eventData)
    {
        activeEvents.Add(eventData);
        OnEventStarted?.Invoke(eventData);
        Debug.Log($"Live event started: {eventData.eventName}");
    }

    private void EndEvent(LiveOpsEventData eventData)
    {
        activeEvents.Remove(eventData);
        OnEventEnded?.Invoke(eventData);
        Debug.Log($"Live event ended: {eventData.eventName}");
    }

    public List<LiveOpsEventData> GetActiveEvents()
    {
        return activeEvents;
    }

    // --- Battle Pass Logic ---

    public void AddBattlePassXP(int amount)
    {
        if (activeBattlePass == null) return;

        currentBattlePassXP += amount;
        Debug.Log($"Gained {amount} BP XP. Total: {currentBattlePassXP}");

        // Check for tier up
        if (currentBattlePassTier < activeBattlePass.tiers.Count &&
            currentBattlePassXP >= activeBattlePass.tiers[currentBattlePassTier].xpRequired)
        {
            currentBattlePassTier++;
            Debug.Log($"Battle Pass Tier Up! Reached Tier {currentBattlePassTier}.");
            // GrantReward(activeBattlePass.tiers[currentBattlePassTier - 1]);
        }

        OnBattlePassXPChanged?.Invoke(currentBattlePassXP, currentBattlePassTier);
    }

    public int GetCurrentBattlePassXP()
    {
        return currentBattlePassXP;
    }

    public int GetCurrentBattlePassTier()
    {
        return currentBattlePassTier;
    }
}
