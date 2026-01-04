using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using RVA.TAC.Player;
using RVA.TAC.Core;
using RVA.TAC.Missions;

namespace RVA.TAC.Networking
{
    /// <summary>
    /// TelemetrySystem collects high-frequency, low-level data about player behavior and
    /// game performance in real-time. This continuous stream of data is useful for
    /// generating heatmaps and analyzing player navigation patterns.
    /// </summary>
    public class TelemetrySystem : MonoBehaviour
    {
        public static TelemetrySystem Instance;

        [Header("Telemetry Settings")]
        [Tooltip("How often (in seconds) to send a telemetry data packet.")]
        [SerializeField] private float telemetryInterval = 5.0f;

        // --- Private State ---
        private Transform playerTransform;
        private PlayerController playerController;
        private Health playerHealth;

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

        void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerController = player.GetComponent<PlayerController>();
                playerHealth = player.GetComponent<Health>();
            }
            else
            {
                Debug.LogError("TelemetrySystem could not find the player. System is disabled.");
                return;
            }

            // Start the continuous data collection coroutine
            StartCoroutine(TelemetryCoroutine());
        }

        /// <summary>
        /// The main coroutine that periodically gathers and sends telemetry data.
        /// </summary>
        private IEnumerator TelemetryCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(telemetryInterval);

                if (NetworkingSystem.Instance.CurrentStatus == NetworkingSystem.ConnectionStatus.Connected)
                {
                    GatherAndSendTelemetry();
                }
            }
        }

        /// <summary>
        /// Gathers relevant data points and sends them to the backend.
        /// </summary>
        private void GatherAndSendTelemetry()
        {
            if (playerTransform == null || playerController == null || playerHealth == null) return;

            // Create a data packet with the current player state
            Dictionary<string, object> telemetryData = new Dictionary<string, object>
        {
            { "timestamp", System.DateTime.UtcNow.ToString("o") },
            { "positionX", playerTransform.position.x },
            { "positionY", playerTransform.position.y },
            { "positionZ", playerTransform.position.z },
            { "currentHealth", playerHealth.currentHealth },
            { "currentWantedLevel", playerController.WantedLevel },
            { "currentMission", MissionManager.Instance.currentStoryMissionIndex }, // Example
            { "fps", 1.0f / Time.unscaledDeltaTime } // Simple FPS calculation
        };

            // Serialize the data to JSON
            string jsonData = MiniJSON.Encode(telemetryData);

            // Send the data via the NetworkingSystem
            NetworkingSystem.Instance.SendMessage("TelemetryUpdate", jsonData);
        }
    }
}
