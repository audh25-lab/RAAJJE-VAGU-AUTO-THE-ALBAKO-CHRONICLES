using UnityEngine;
using TMPro;

namespace RVA.TAC.Systems
{
    /// <summary>
    /// CompatibilitySystem checks the user's device specifications at startup to ensure
    /// it meets the minimum requirements. It can automatically adjust graphics settings
    /// to provide a better experience on lower-end devices.
    /// </summary>
    public class CompatibilitySystem : MonoBehaviour
    {
        [Header("Minimum Requirements")]
        [Tooltip("Minimum required system memory in megabytes.")]
        [SerializeField] private int minRamMB = 2048; // 2GB
        [Tooltip("The graphics API level required (e.g., 3.0 for OpenGL ES 3.0).")]
        [SerializeField] private float minGraphicsShaderLevel = 3.5f;

        [Header("UI Feedback")]
        [Tooltip("A panel to display a warning if the device is below spec.")]
        [SerializeField] private GameObject warningPanel;
        [Tooltip("The text component for the warning message.")]
        [SerializeField] private TextMeshProUGUI warningText;

        void Start()
        {
            // Run the compatibility check as soon as the game starts.
            PerformCompatibilityCheck();
        }

        private void PerformCompatibilityCheck()
        {
            bool isCompatible = true;
            string warningMessage = "Warning: Your device is below the recommended specifications, which may result in poor performance.\n";

            // --- Check System Memory (RAM) ---
            int systemMemory = SystemInfo.systemMemorySize;
            if (systemMemory < minRamMB)
            {
                isCompatible = false;
                warningMessage += $"\n- Requires {minRamMB}MB RAM (You have {systemMemory}MB)";
                Debug.LogWarning($"Device is below minimum RAM requirement: {systemMemory}MB found, {minRamMB}MB required.");
            }

            // --- Check Graphics API Level ---
            float shaderLevel = SystemInfo.graphicsShaderLevel / 10f; // Convert from int (e.g., 35) to float (3.5)
            if (shaderLevel < minGraphicsShaderLevel)
            {
                isCompatible = false;
                warningMessage += $"\n- Requires Graphics API Level {minGraphicsShaderLevel} (You have {shaderLevel})";
                Debug.LogWarning($"Device is below minimum graphics API level: {shaderLevel} found, {minGraphicsShaderLevel} required.");
            }

            // --- Take Action Based on Compatibility ---
            if (!isCompatible)
            {
                // If the device is low-end, automatically lower the graphics settings.
                Debug.Log("Low-end device detected. Automatically setting graphics to 'Low'.");
                QualitySettings.SetQualityLevel(0, true); // 0 is typically the index for the lowest quality level

                // Display the warning message to the player.
                if (warningPanel != null && warningText != null)
                {
                    warningText.text = warningMessage;
                    warningPanel.SetActive(true);
                }
            }
            else
            {
                Debug.Log("Device meets minimum requirements.");
            }

            // This object has done its job, so it can be destroyed.
            Destroy(gameObject);
        }
    }
}
