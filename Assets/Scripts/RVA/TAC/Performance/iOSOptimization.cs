using UnityEngine;
#if UNITY_IOS
using UnityEngine.iOS;
#endif

public class iOSOptimization : MonoBehaviour
{
    [Header("Performance Settings")]
    public bool useMetalAPI = true;
    public bool allowLowPowerMode = true;

    void Start()
    {
#if UNITY_IOS
        if (useMetalAPI)
        {
            // Unity typically defaults to Metal on modern iOS, but this is how you'd double-check.
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Metal)
            {
                Debug.LogWarning("Metal API is not the default. This may impact performance.");
            }
        }

        // Allow the OS to manage power consumption, which is important for battery life.
        Application.lowRenderFrequency = allowLowPowerMode;

        // Request a specific framerate.
        Application.targetFrameRate = 60;

        Debug.Log("iOS-specific optimizations applied.");
#endif
    }

    public void RequestAppReview()
    {
#if UNITY_IOS
        // This uses the official StoreKit API to request a review from the user.
        Device.RequestStoreReview();
#endif
    }
}
