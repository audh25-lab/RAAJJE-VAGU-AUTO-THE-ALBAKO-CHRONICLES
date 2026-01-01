using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections.Generic;

// This system will be a high-level manager for network operations.
// It will be "network-ready" but won't establish real connections in this environment.
public class NetworkingSystem : MonoBehaviour
{
    public static NetworkingSystem Instance { get; private set; }

    public event Action OnServerStarted;
    public event Action OnClientConnected;
    public event Action OnClientDisconnected;

    // We will simulate a list of connected players.
    private List<string> connectedPlayerIDs = new List<string>();
    private bool isServer = false;

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
        }
    }

    public void StartHost()
    {
        if (isServer)
        {
            Debug.LogWarning("Server is already running.");
            return;
        }
        
        // --- In a real Unity environment, you would start the NetworkManager host here ---
        // NetworkManager.singleton.StartHost();
        
        isServer = true;
        connectedPlayerIDs.Clear();
        // The host is the first player.
        connectedPlayerIDs.Add("Player_Host");

        Debug.Log("Host started. Server is running.");
        OnServerStarted?.Invoke();
        OnClientConnected?.Invoke(); // The host is also a client.
    }

    public void StartClient(string serverAddress)
    {
        if (isServer)
        {
            Debug.LogError("Cannot start client, this instance is already a server.");
            return;
        }

        // --- In a real Unity environment, you would connect to the server here ---
        // NetworkManager.singleton.networkAddress = serverAddress;
        // NetworkManager.singleton.StartClient();

        // Simulate a successful connection for now.
        string newPlayerID = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
        connectedPlayerIDs.Add(newPlayerID);

        Debug.Log($"Client connected to {serverAddress}. Player ID: {newPlayerID}");
        OnClientConnected?.Invoke();
    }

    public void StopConnection()
    {
        // --- In a real Unity environment, you would stop the host or client ---
        // if (isServer) NetworkManager.singleton.StopHost();
        // else NetworkManager.singleton.StopClient();

        isServer = false;
        connectedPlayerIDs.Clear();

        Debug.Log("Network connection stopped.");
        OnClientDisconnected?.Invoke();
    }

    // --- Example methods for a network-ready system ---

    public bool IsServer()
    {
        return isServer;
    }

    public List<string> GetConnectedPlayers()
    {
        return connectedPlayerIDs;
    }

    // In a real implementation, you would have methods to send data to the server/clients.
    // For example: public void SendPlayerAction(ActionData data) { ... }
}
