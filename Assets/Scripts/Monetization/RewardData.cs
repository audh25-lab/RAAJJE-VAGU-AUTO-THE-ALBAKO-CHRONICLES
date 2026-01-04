using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A ScriptableObject that defines a specific reward that can be granted to the player.
/// This data-driven approach allows for easy creation and balancing of rewards.
/// </summary>
[CreateAssetMenu(fileName = "New Reward", menuName = "RVA/Reward Data")]
public class RewardData : ScriptableObject
{
    public string rewardName;

    [Header("Rewards")]
    public int moneyReward;
    public List<ItemReward> itemRewards;
    public int reputationReward;
    public string reputationGang;
}

/// <summary>
/// Represents a single item and its quantity within a reward.
/// </summary>
[System.Serializable]
public class ItemReward
{
    public ItemData item; // This would be another ScriptableObject defining an item
    public int quantity;
}
