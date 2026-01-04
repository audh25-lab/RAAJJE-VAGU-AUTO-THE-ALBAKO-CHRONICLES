using UnityEngine;
using System;
using System.Collections;

namespace RVA.TAC.Networking
{
    public class NetworkingSystem : MonoBehaviour
    {
        public static NetworkingSystem Instance { get; private set; }

        public enum ConnectionStatus { Disconnected, Connecting, Connected }
        public ConnectionStatus CurrentStatus { get; private set; } = ConnectionStatus.Disconnected;

        public static event Action OnConnected;
        public static event Action OnDisconnected;

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

        public void Connect()
        {
            if (CurrentStatus == ConnectionStatus.Disconnected)
            {
                StartCoroutine(ConnectCoroutine());
            }
        }

        public void Disconnect()
        {
            if (CurrentStatus == ConnectionStatus.Connected)
            {
                CurrentStatus = ConnectionStatus.Disconnected;
                OnDisconnected?.Invoke();
            }
        }

        private IEnumerator ConnectCoroutine()
        {
            CurrentStatus = ConnectionStatus.Connecting;
            Debug.Log("Connecting to server...");
            yield return new WaitForSeconds(2f);
            CurrentStatus = ConnectionStatus.Connected;
            OnConnected?.Invoke();
            Debug.Log("Connected to server.");
        }

        public void SendMessage(string messageType, string payload)
        {
            if (CurrentStatus == ConnectionStatus.Connected)
            {
                Debug.Log($"Sending message to server -> Type: {messageType}, Payload: {payload}");
            }
        }
    }
}
