using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A ScriptableObject that defines the daily schedule for an NPC.
/// </summary>
[CreateAssetMenu(fileName = "New NPC Schedule", menuName = "RVA/NPC Schedule")]
public class NPCSchedule : ScriptableObject
{
    public List<ScheduleEntry> entries;

    public ScheduleEntry GetEntryForTime(float time)
    {
        // Get the latest entry that is scheduled before or at the current time
        return entries.Where(e => e.hour <= time).OrderByDescending(e => e.hour).FirstOrDefault();
    }
}
