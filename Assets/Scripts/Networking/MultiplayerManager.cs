using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MultiplayerManager handles the state of a multiplayer game session. It manages
/// player connections, and the spawning of networked players.
/// </summary>
public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance;

    [Header("Multiplayer Prefabs")]
    [SerializeField] private GameObject networkedPlayerPrefab;

    private Dictionary<string, GameObject> networkedPlayers = new Dictionary<string, GameObject>();

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
        NetworkingSystem.OnConnected += OnConnectedToServer;
        NetworkingSystem.OnDisconnected += OnDisconnectedFromServer;
        // In a real system, you'd subscribe to a message receiver:
        // NetworkingSystem.OnMessageReceived += HandleNetworkMessage;
    }

    void OnDisable()
    {
        NetworkingSystem.OnConnected -= OnConnectedToServer;
        NetworkingSystem.OnDisconnected -= OnDisconnectedFromServer;
    }

    private void OnConnectedToServer()
    {
        Debug.Log("MultiplayerManager: Connected to server. Ready to join a session.");
    }

    private void OnDisconnectedFromServer()
    {
        Debug.Log("MultiplayerManager: Disconnected. Cleaning up networked players.");
        foreach (var player in networkedPlayers.Values)
        {
            Destroy(player);
        }
        networkedPlayers.Clear();
    }

    /// <summary>
    /// Spawns a representation of another player in the world.
    /// </summary>
    public void SpawnNetworkedPlayer(string playerID, Vector3 position)
    {
        if (networkedPlayers.ContainsKey(playerID)) return;

        GameObject newPlayer = Instantiate(networkedPlayerPrefab, position, Quaternion.identity);
        networkedPlayers.Add(playerID, newPlayer);
    }

    /// <summary>
    /// Updates the position of a networked player.
    /// </summary>
    public void UpdateNetworkedPlayerPosition(string playerID, Vector3 position)
    {
        if (networkedPlayers.TryGetValue(playerID, out GameObject player))
        {
            // In a real game, you would smoothly interpolate to the new position
            player.transform.position = position;
        }
    }

    /// <summary>
    /// Removes a networked player from the world.
    /// </summary>
    public void DespawnNetworkedPlayer(string playerID)
    {
        if (networkedPlayers.TryGetValue(playerID, out GameObject player))
        {
            Destroy(player);
            networkedPlayers.Remove(playerID);
        }
    }
}
