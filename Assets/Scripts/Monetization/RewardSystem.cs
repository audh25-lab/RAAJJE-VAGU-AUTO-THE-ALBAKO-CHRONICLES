using UnityEngine;

/// <summary>
/// RewardSystem is a centralized manager for granting rewards to the player.
/// It uses a data-driven approach with ScriptableObjects to define rewards,
/// keeping the reward logic separate from the systems that grant them.
/// </summary>
public class RewardSystem : MonoBehaviour
{
    public static RewardSystem Instance;

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
    /// Grants a reward to a specific player GameObject.
    /// This refactored version avoids using FindObjectOfType for better performance.
    /// </summary>
    /// <param name="playerObject">The GameObject of the player receiving the reward.</param>
    /// <param name="rewardData">The ScriptableObject defining the reward to be granted.</param>
    public void GrantReward(GameObject playerObject, RewardData rewardData)
    {
        if (rewardData == null)
        {
            Debug.LogWarning("RewardData is null. Cannot grant reward.");
            return;
        }
        if (playerObject == null)
        {
            Debug.LogWarning("Player object is null. Cannot grant reward.");
            return;
        }

        Debug.Log($"Granting reward: {rewardData.rewardName} to {playerObject.name}");
        PlayerController player = playerObject.GetComponent<PlayerController>();
        if (player == null) return;

        // --- Grant Currency ---
        if (rewardData.moneyReward > 0)
        {
            player.money += rewardData.moneyReward;
            NotificationSystem.Instance?.ShowNotification($"+{rewardData.moneyReward:N0} MVR");
        }

        // --- Grant Items ---
        foreach (var itemReward in rewardData.itemRewards)
        {
            if (itemReward.item != null)
            {
                InventorySystem.Instance?.AddItem(itemReward.item, itemReward.quantity);
                NotificationSystem.Instance?.ShowNotification($"Acquired: {itemReward.item.itemName}");
            }
        }

        // --- Grant Reputation ---
        if (rewardData.reputationReward != 0 && !string.IsNullOrEmpty(rewardData.reputationGang))
        {
            GangSystem.Instance?.ModifyReputation(rewardData.reputationGang, rewardData.reputationReward);
        }
    }
}
