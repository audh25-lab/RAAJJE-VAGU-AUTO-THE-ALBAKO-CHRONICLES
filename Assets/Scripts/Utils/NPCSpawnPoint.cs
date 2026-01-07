using UnityEngine;

namespace RVA.TAC.Utils
{
    /// <summary>
    /// Defines a point in the world where NPCs can be spawned.
    /// </summary>
    public class NPCSpawnPoint : MonoBehaviour
    {
        public enum NPCType { Civilian, Police, GangMember }
        public NPCType spawnType;
        public string gangAffiliation = "Neutral"; // Only used if spawnType is GangMember
    }
}
