using UnityEngine;
using System.Collections.Generic;

namespace RVA.TAC.NPC
{
    /// <summary>
    /// Manages gang relationships, rivalries, and territories in RVA:TAC.
    /// This system is a singleton that provides a global point of access for
    /// NPC AI to query faction relationships.
    /// </summary>
    public class GangSystem : MonoBehaviour
    {
        #region Singleton
        public static GangSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeGangRelationships();
            }
        }
        #endregion

        // Simple dictionary to hold rivalries. Key: Gang Name, Value: List of rival gang names.
        private Dictionary<string, List<string>> gangRivalries = new Dictionary<string, List<string>>();

        /// <summary>
        /// Initializes the default gang rivalries for the game.
        /// This would typically be loaded from a configuration file.
        /// </summary>
        private void InitializeGangRelationships()
        {
            // Example rivalries
            gangRivalries["RedBloods"] = new List<string> { "BlueSkulls" };
            gangRivalries["BlueSkulls"] = new List<string> { "RedBloods", "GreenCobras" };
            gangRivalries["GreenCobras"] = new List<string> { "BlueSkulls" };

            Debug.Log("[RVA:TAC GangSystem] Initialized with default rivalries.");
        }

        /// <summary>
        /// Checks if two gangs are rivals.
        /// </summary>
        /// <param name="gang1">The name of the first gang.</param>
        /// <param name="gang2">The name of the second gang.</param>
        /// <returns>True if the gangs are rivals, false otherwise.</returns>
        public bool AreGangsRivals(string gang1, string gang2)
        {
            if (string.IsNullOrEmpty(gang1) || string.IsNullOrEmpty(gang2) || gang1 == "Neutral" || gang2 == "Neutral")
            {
                return false;
            }

            if (gangRivalries.ContainsKey(gang1))
            {
                return gangRivalries[gang1].Contains(gang2);
            }

            return false;
        }

        /// <summary>
        /// Gets the player's current standing with a specific gang.
        /// </summary>
        /// <param name="gangName">The name of the gang.</param>
        /// <returns>A float representing the relationship status (-1 for hostile, 1 for friendly).</returns>
        public float GetPlayerGangRelationship(string gangName)
        {
            // This would be tied into the SaveSystem and game progress
            return 0f; // Neutral default
        }
    }
}
