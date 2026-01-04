using UnityEngine;
using System.Collections.Generic;
using RVA.TAC.Missions;

namespace RVA.TAC.UI
{
    public class QuestLogSystem : MonoBehaviour
    {
        public static QuestLogSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }
        }

        public void AddMission(MissionData missionData)
        {
            Debug.Log($"QuestLogSystem: Added mission {missionData.missionName}");
        }

        public void CompleteMission(MissionData missionData)
        {
            Debug.Log($"QuestLogSystem: Completed mission {missionData.missionName}");
        }
    }
}
