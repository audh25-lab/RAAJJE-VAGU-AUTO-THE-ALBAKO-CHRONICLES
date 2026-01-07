using UnityEngine;
using RVA.TAC.NPC;

namespace RVA.TAC.Utils
{
    /// <summary>
    /// A component to be placed on an NPC to make them managed by the NPCScheduleSystem.
    /// It holds a reference to the NPC's schedule and executes the required actions.
    /// </summary>
    [RequireComponent(typeof(AINavigationSystem))]
    public class SchedulableNPC : MonoBehaviour
    {
        public NPCSchedule schedule;
        public ScheduleAction currentScheduleAction { get; private set; }

        private AINavigationSystem navigation;

        void Awake()
        {
            navigation = GetComponent<AINavigationSystem>();
        }

        public void ExecuteScheduleEntry(ScheduleEntry entry)
        {
            currentScheduleAction = entry.action;
            Debug.Log($"{gameObject.name} is now performing action: {entry.action}");

            switch (entry.action)
            {
                case ScheduleAction.GoToWork:
                case ScheduleAction.GoHome:
                case ScheduleAction.WanderInArea:
                    if (entry.targetLocation != null)
                    {
                        navigation.SetDestination(entry.targetLocation.position);
                    }
                    break;
                case ScheduleAction.Work:
                case ScheduleAction.Sleep:
                case ScheduleAction.Idle:
                    navigation.Stop();
                    break;
            }
        }
    }
}
