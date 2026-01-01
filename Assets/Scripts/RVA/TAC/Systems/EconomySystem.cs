using UnityEngine;
using System;
using System.Collections.Generic;

// ScriptableObject to define an item in the economy.
[CreateAssetMenu(fileName = "New Market Item", menuName = "RVA/Market Item")]
public class MarketItem : ScriptableObject
{
    public int itemID;
    public string itemName;
    public float basePrice;
    public enum ItemCategory { Food, Fuel, Luxuries, Industrial, General }
    public ItemCategory category;
}

public class EconomySystem : MonoBehaviour
{
    public static EconomySystem Instance { get; private set; }

    public event Action<float> OnCurrencyChanged;

    [Header("Player Wallet")]
    private float playerCurrency = 1000f; // Starting money

    [Header("Market Data")]
    public List<MarketItem> allMarketItems;

    // The current market prices, keyed by itemID.
    private Dictionary<int, float> currentMarketPrices = new Dictionary<int, float>();
    // A multiplier to simulate supply and demand, keyed by itemID.
    private Dictionary<int, float> priceMultipliers = new Dictionary<int, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            InitializeMarket();
        }
    }

    private void Start()
    {
        // Subscribe to SaveSystem events
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnSave += SaveEconomyData;
            SaveSystem.Instance.OnLoad += LoadEconomyData;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnSave -= SaveEconomyData;
            SaveSystem.Instance.OnLoad -= LoadEconomyData;
        }
    }

    private void InitializeMarket()
    {
        foreach (var item in allMarketItems)
        {
            if (!currentMarketPrices.ContainsKey(item.itemID))
            {
                currentMarketPrices.Add(item.itemID, item.basePrice);
                priceMultipliers.Add(item.itemID, 1.0f);
            }
        }
    }

    // --- Player Wallet Management ---

    public float GetPlayerCurrency()
    {
        return playerCurrency;
    }

    public void AddCurrency(float amount)
    {
        if (amount <= 0) return;
        playerCurrency += amount;
        OnCurrencyChanged?.Invoke(playerCurrency);
        Debug.Log($"Added {amount}. New balance: {playerCurrency}");
    }

    public bool RemoveCurrency(float amount)
    {
        if (amount <= 0 || playerCurrency < amount)
        {
            return false;
        }
        playerCurrency -= amount;
        OnCurrencyChanged?.Invoke(playerCurrency);
        Debug.Log($"Removed {amount}. New balance: {playerCurrency}");
        return true;
    }

    // --- Market Transactions ---

    public float GetItemPrice(int itemID)
    {
        if (currentMarketPrices.TryGetValue(itemID, out float basePrice) && priceMultipliers.TryGetValue(itemID, out float multiplier))
        {
            return basePrice * multiplier;
        }
        return -1f; // Item not found
    }

    public bool BuyItem(int itemID, int quantity = 1)
    {
        float price = GetItemPrice(itemID);
        if (price < 0)
        {
            Debug.LogError($"Item with ID {itemID} not found in market.");
            return false;
        }

        float totalCost = price * quantity;
        if (RemoveCurrency(totalCost))
        {
            Debug.Log($"Player bought {quantity} of item {itemID} for {totalCost}.");
            // In a real game, you would add the item to the player's inventory here.

            // Buying increases demand, slightly increasing the price.
            AdjustPriceMultiplier(itemID, 0.05f * quantity);
            return true;
        }
        else
        {
            Debug.LogWarning("Not enough currency to buy item.");
            return false;
        }
    }

    public bool SellItem(int itemID, int quantity = 1)
    {
        float price = GetItemPrice(itemID);
        if (price < 0)
        {
            Debug.LogError($"Item with ID {itemID} not found in market.");
            return false;
        }

        // For simplicity, we sell at the current market price. Some games have a lower sell price.
        float totalValue = price * quantity;
        AddCurrency(totalValue);

        Debug.Log($"Player sold {quantity} of item {itemID} for {totalValue}.");
        // In a real game, you would remove the item from the player's inventory here.

        // Selling increases supply, slightly decreasing the price.
        AdjustPriceMultiplier(itemID, -0.05f * quantity);
        return true;
    }

    // --- Dynamic Economy Simulation ---

    // This can be called by other systems (e.g., missions, events) to influence the economy.
    public void TriggerEconomicEvent(MarketItem.ItemCategory category, float impactMultiplier)
    {
        Debug.Log($"Economic event triggered for {category} with an impact of {impactMultiplier}.");
        foreach (var item in allMarketItems)
        {
            if (item.category == category)
            {
                AdjustPriceMultiplier(item.itemID, impactMultiplier);
            }
        }
    }

    private void AdjustPriceMultiplier(int itemID, float change)
    {
        if (priceMultipliers.ContainsKey(itemID))
        {
            priceMultipliers[itemID] += change;
            // Clamp the multiplier to prevent extreme prices.
            priceMultipliers[itemID] = Mathf.Clamp(priceMultipliers[itemID], 0.2f, 5.0f);
        }
    }

    // --- Save and Load Integration ---

    public void SaveEconomyData(SaveData data)
    {
        data.playerCurrency = playerCurrency;
    }

    public void LoadEconomyData(SaveData data)
    {
        playerCurrency = data.playerCurrency;
        OnCurrencyChanged?.Invoke(playerCurrency);
    }
}
