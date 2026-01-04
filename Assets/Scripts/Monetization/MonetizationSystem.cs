using UnityEngine;
using System;

/// <summary>
/// MonetizationSystem is the central manager for all in-app purchases (IAPs) and rewarded ads.
/// It provides a simplified, unified interface for handling transactions and ad playback,
/// while adhering to the game's "ethical monetization" principles.
/// </summary>
public class MonetizationSystem : MonoBehaviour
{
    public static MonetizationSystem Instance;

    // --- Events ---
    public static event Action<string> OnPurchaseSuccessful;
    public static event Action<string> OnRewardedAdCompleted;

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
            return;
        }

        Initialize();
    }

    /// <summary>
    /// Initializes the platform's IAP and Ads services.
    /// </summary>
    private void Initialize()
    {
        // In a real implementation, this is where you would initialize Unity IAP and Unity Ads.
        // Example: UnityAds.Initialize(...)
        Debug.Log("MonetizationSystem initialized (simulated).");
    }

    /// <summary>
    /// Initiates the purchase process for a specific item.
    /// </summary>
    /// <param name="productID">The unique ID of the product to purchase (e.g., "com.rvatac.cosmetic.sunglasses").</param>
    public void InitiatePurchase(string productID)
    {
        Debug.Log($"Initiating purchase for product: {productID}");

        // --- Simulated Purchase Logic ---
        // In a real implementation, you would call the Unity IAP API here.
        // For now, we'll simulate a successful purchase after a delay.
        Invoke(nameof(SimulatePurchaseSuccess), 2.0f);
    }

    private void SimulatePurchaseSuccess()
    {
        string fakeProductID = "com.rvatac.cosmetic.sunglasses";
        Debug.Log($"Purchase successful for: {fakeProductID}");
        OnPurchaseSuccessful?.Invoke(fakeProductID);
    }

    /// <summary>
    /// Shows a rewarded video ad to the player.
    /// </summary>
    /// <param name="placementID">The ID for the ad placement (e.g., "reward_double_mission_payout").</param>
    public void ShowRewardedAd(string placementID)
    {
        Debug.Log($"Showing rewarded ad for placement: {placementID}");

        // --- Simulated Ad Logic ---
        // In a real implementation, you would check if an ad is ready and then show it.
        // For now, we'll simulate the player watching the ad and receiving the reward.
        Invoke(nameof(SimulateAdCompletion), 3.0f);
    }

    private void SimulateAdCompletion()
    {
        string fakePlacementID = "reward_double_mission_payout";
        Debug.Log($"Rewarded ad completed for: {fakePlacementID}");
        OnRewardedAdCompleted?.Invoke(fakePlacementID);
    }

    /// <summary>
    /// Checks if rewarded ads are available to be shown.
    /// </summary>
    public bool IsRewardedAdReady()
    {
        // In a real implementation, this would check the status of the Ads service.
        return true; // Always ready in simulation
    }
}
