using UnityEngine;
using System.Collections.Generic;
using RVA.TAC.Networking;
using RVA.TAC.Player;
using RVA.TAC.Core;
using RVA.TAC.Missions;

namespace RVA.TAC.Monetization
{
    /// <summary>
    /// ServerSyncSystem is responsible for periodically synchronizing the client's game state
    /// with the backend server. This is crucial for preventing cheating and for enabling
    /// server-authoritative events and monetization.
    /// </summary>
    public class ServerSyncSystem : MonoBehaviour
    {
        public static ServerSyncSystem Instance;

        [Header("Sync Settings")]
        [Tooltip("How often (in seconds) to send a heartbeat sync to the server.")]
        [SerializeField] private float heartbeatInterval = 60.0f; // Sync every minute

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

        void OnEnable()
        {
            // Subscribe to network events
            NetworkingSystem.OnConnected += StartSyncing;
            NetworkingSystem.OnDisconnected += StopSyncing;
        }

        void OnDisable()
        {
            NetworkingSystem.OnConnected -= StartSyncing;
            NetworkingSystem.OnDisconnected -= StopSyncing;
        }

        /// <summary>
        /// Starts the periodic heartbeat sync when connected to the server.
        /// </summary>
        private void StartSyncing()
        {
            Debug.Log("Connected to server. Starting heartbeat sync...");
            InvokeRepeating(nameof(SendHeartbeat), heartbeatInterval, heartbeatInterval);
        }

        /// <summary>
        /// Stops the sync when disconnected.
        /// </summary>
        private void StopSyncing()
        {
            Debug.Log("Disconnected from server. Stopping heartbeat sync.");
            CancelInvoke(nameof(SendHeartbeat));
        }

        /// <summary>
        /// Gathers key game state data and sends it to the server for validation.
        /// </summary>
        private void SendHeartbeat()
        {
            if (NetworkingSystem.Instance.CurrentStatus != NetworkingSystem.ConnectionStatus.Connected)
            {
                return;
            }

            Debug.Log("Sending heartbeat to server...");

            PlayerController player = PlayerController.Instance;
            if (player == null) return;

            // Create a data packet with a summary of the current game state
            Dictionary<string, object> heartbeatData = new Dictionary<string, object>
        {
            { "timestamp", System.DateTime.UtcNow.ToString("o") },
            { "playerHealth", player.GetComponent<Health>().currentHealth },
            { "playerMoney", player.Money },
            { "currentMission", MissionManager.Instance.currentStoryMissionIndex },
            // In a real implementation, you'd include an inventory hash or other checksums
            // to validate the game state without sending the entire inventory.
        };

            // Serialize and send the data
            string jsonData = MiniJSON.Encode(heartbeatData);
            NetworkingSystem.Instance.SendMessage("PlayerHeartbeat", jsonData);
        }

        /// <summary>
        /// A method to be called when the server sends a message to the client,
        /// for example, to grant a promotional item.
        /// </summary>
        public void HandleServerMessage(string messageType, string payload)
        {
            Debug.Log($"Received message from server -> Type: {messageType}, Payload: {payload}");

            // In a real system, you would parse the payload (e.g., from JSON) and act on it.
            // Example:
            // if (messageType == "GrantPromotionalItem")
            // {
            //     ItemData itemToGrant = ItemDatabase.GetItem(payload);
            //     InventorySystem.Instance.AddItem(itemToGrant);
            // }
        }
    }
}
