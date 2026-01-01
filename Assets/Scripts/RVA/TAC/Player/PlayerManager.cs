using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    private Health playerHealth;
    private Transform playerTransform;

    private void Start()
    {
        // Find player components. This assumes the player GameObject is tagged "Player".
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<Health>();
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("PlayerManager could not find a GameObject with the 'Player' tag.");
            return;
        }

        // Subscribe to the SaveSystem events.
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnSave += SavePlayerData;
            SaveSystem.Instance.OnLoad += LoadPlayerData;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks.
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnSave -= SavePlayerData;
            SaveSystem.Instance.OnLoad -= LoadPlayerData;
        }
    }

    public void SavePlayerData(SaveData data)
    {
        if (playerHealth != null)
        {
            data.playerHealth = playerHealth.GetCurrentHealth();
        }
        if (playerTransform != null)
        {
            data.playerPosition = playerTransform.position;
        }
    }

    public void LoadPlayerData(SaveData data)
    {
        if (playerHealth != null)
        {
            // This assumes the Health component has a method to directly set health.
            // If not, you'd need to add one. For now, we'll imagine it does.
            // playerHealth.SetHealth(data.playerHealth);
        }
        if (playerTransform != null)
        {
            // Using NavMeshAgent-safe teleporting if applicable.
            NavMeshAgent agent = playerTransform.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.Warp(data.playerPosition);
            }
            else
            {
                playerTransform.position = data.playerPosition;
            }
        }
    }
}
