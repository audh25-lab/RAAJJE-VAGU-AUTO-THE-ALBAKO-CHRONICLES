using UnityEngine;
using System;
using System.Collections.Generic;

// ScriptableObject to define a type of fish.
[CreateAssetMenu(fileName = "New Fish", menuName = "RVA/Fish")]
public class FishData : ScriptableObject
{
    public string fishName;
    public int fishID;
    public float minWeight = 1.0f;
    public float maxWeight = 5.0f;
    [Range(0, 1)]
    public float rarity = 0.5f; // 0 = very common, 1 = very rare
}

// Component to be placed on GameObjects in the world to mark fishing spots.
public class FishingSpot : MonoBehaviour
{
    [Header("Available Fish")]
    public List<FishData> availableFish;

    // You could add more properties, like time of day restrictions, required bait, etc.
}

public class FishingSystem : MonoBehaviour
{
    public static FishingSystem Instance { get; private set; }

    public event Action<FishData, float> OnFishCaught;
    public event Action OnFishingStarted;
    public event Action OnFishingEnded;
    public event Action OnFishHooked;

    public enum FishingState
    {
        Idle,
        Casting,
        WaitingForBite,
        Reeling,
        FishCaught
    }

    [Header("State")]
    public FishingState currentState = FishingState.Idle;

    [Header("Gameplay Settings")]
    public float minWaitTime = 3.0f;
    public float maxWaitTime = 10.0f;
    public float reelingTime = 5.0f; // How long the player has to reel in the fish.

    private FishingSpot currentFishingSpot;
    private float biteTimer;
    private float reelTimer;
    private FishData hookedFish;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public void StartFishing(FishingSpot spot)
    {
        if (currentState != FishingState.Idle || spot == null || spot.availableFish.Count == 0)
        {
            Debug.LogWarning("Cannot start fishing. Either already fishing or the spot is invalid.");
            return;
        }

        currentFishingSpot = spot;
        currentState = FishingState.Casting;
        Debug.Log("Casting the line...");
        OnFishingStarted?.Invoke();

        // Simple state transition for now. In a real game, this would wait for an animation.
        currentState = FishingState.WaitingForBite;
        biteTimer = Time.time + UnityEngine.Random.Range(minWaitTime, maxWaitTime);
    }

    private void Update()
    {
        if (currentState == FishingState.WaitingForBite)
        {
            if (Time.time >= biteTimer)
            {
                HookFish();
            }
        }
        else if (currentState == FishingState.Reeling)
        {
            // The reeling mini-game logic would go here.
            // For now, we'll use a simple timer.
            reelTimer -= Time.deltaTime;
            if (reelTimer <= 0)
            {
                // For simplicity, we assume success if the player doesn't cancel.
                CatchFish();
            }
        }
    }

    private void HookFish()
    {
        currentState = FishingState.Reeling;
        hookedFish = SelectRandomFish();
        reelTimer = reelingTime;

        Debug.Log($"A {hookedFish.fishName} is on the line! Reel it in!");
        OnFishHooked?.Invoke();
    }

    // This would be called by player input during the Reeling state.
    public void ReelInput()
    {
        if (currentState != FishingState.Reeling) return;
        // Logic for a successful reel action. This could add to a success meter.
        // For this simplified version, we'll just let the timer run out for a catch.
    }

    private void CatchFish()
    {
        float weight = UnityEngine.Random.Range(hookedFish.minWeight, hookedFish.maxWeight);
        Debug.Log($"You caught a {hookedFish.fishName} weighing {weight:F2} kg!");

        OnFishCaught?.Invoke(hookedFish, weight);

        // TODO: Add fish to player inventory.

        EndFishing();
    }

    public void EndFishing()
    {
        if (currentState == FishingState.Idle) return;

        currentState = FishingState.Idle;
        currentFishingSpot = null;
        hookedFish = null;
        Debug.Log("Fishing has ended.");
        OnFishingEnded?.Invoke();
    }

    private FishData SelectRandomFish()
    {
        // A simple selection based on rarity. A more complex system could be used.
        float totalRarity = 0;
        foreach (var fish in currentFishingSpot.availableFish)
        {
            totalRarity += (1 - fish.rarity);
        }

        float randomValue = UnityEngine.Random.Range(0, totalRarity);

        float cumulativeRarity = 0;
        foreach (var fish in currentFishingSpot.availableFish)
        {
            cumulativeRarity += (1 - fish.rarity);
            if (randomValue <= cumulativeRarity)
            {
                return fish;
            }
        }

        // Fallback in case something goes wrong
        return currentFishingSpot.availableFish[0];
    }
}
