using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RVA.TAC.Networking
{
    /// <summary>
    /// LeaderboardSystem manages the fetching and display of in-game leaderboards.
    /// It communicates with a backend service to submit scores and retrieve top rankings.
    /// This implementation simulates the backend with a local list.
    /// </summary>
    public class LeaderboardSystem : MonoBehaviour
    {
        public static LeaderboardSystem Instance;

        // A simulated backend database of scores.
        private Dictionary<string, List<LeaderboardEntry>> leaderboards = new Dictionary<string, List<LeaderboardEntry>>();

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

        /// <summary>
        /// Submits a new score to a specific leaderboard.
        /// </summary>
        /// <param name="leaderboardID">The ID of the leaderboard (e.g., "RichestPlayers").</param>
        /// <param name="playerName">The name of the player.</param>
        /// <param name="score">The score to submit.</param>
        public void SubmitScore(string leaderboardID, string playerName, int score)
        {
            // In a real system, this would send a message to the backend via NetworkingSystem.
            // NetworkingSystem.Instance.SendMessage("SubmitScore", "{...}");

            // --- Simulated Backend Logic ---
            if (!leaderboards.ContainsKey(leaderboardID))
            {
                leaderboards[leaderboardID] = new List<LeaderboardEntry>();
            }

            // Remove any existing entry for this player
            leaderboards[leaderboardID].RemoveAll(entry => entry.playerName == playerName);

            // Add the new score
            leaderboards[leaderboardID].Add(new LeaderboardEntry(playerName, score));

            Debug.Log($"Score of {score} submitted for {playerName} to leaderboard '{leaderboardID}'.");
        }

        /// <summary>
        /// Fetches the top entries from a leaderboard.
        /// </summary>
        /// <param name="leaderboardID">The ID of the leaderboard to fetch.</param>
        /// <param name="count">The number of top entries to retrieve.</param>
        /// <param name="onComplete">Callback invoked with the list of top scores.</param>
        public void FetchLeaderboard(string leaderboardID, int count, System.Action<List<LeaderboardEntry>> onComplete)
        {
            // In a real system, this would request data from the backend.
            // For now, we'll retrieve from our local simulation.

            List<LeaderboardEntry> results = new List<LeaderboardEntry>();
            if (leaderboards.TryGetValue(leaderboardID, out List<LeaderboardEntry> entries))
            {
                // Sort the scores descending and take the top 'count'
                results = entries.OrderByDescending(e => e.score).Take(count).ToList();
            }

            onComplete?.Invoke(results);
        }
    }


    /// <summary>
    /// A simple data structure representing a single entry on a leaderboard.
    /// </summary>
    [System.Serializable]
    public class LeaderboardEntry
    {
        public string playerName;
        public int score;
        public int rank;

        public LeaderboardEntry(string name, int score)
        {
            this.playerName = name;
            this.score = score;
        }
    }
}
