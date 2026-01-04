using UnityEngine;
using System.IO;

/// <summary>
/// CrashHandler is a system for catching unhandled exceptions and crashes.
/// It logs detailed crash reports to a file, which is invaluable for debugging
/// issues that occur in live builds.
/// </summary>
public class CrashHandler : MonoBehaviour
{
    public static CrashHandler Instance;

    private string crashLogPath;

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

        // Set up the path for the crash log file
        crashLogPath = Path.Combine(Application.persistentDataPath, "crash_log.txt");

        // Subscribe to Unity's log message event. This is the core of the crash handler.
        Application.logMessageReceived += HandleLog;
    }

    void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks when the object is destroyed
        Application.logMessageReceived -= HandleLog;
    }

    /// <summary>
    /// This method is called by Unity whenever a log message is generated.
    /// We use it to detect critical errors and exceptions.
    /// </summary>
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // We are interested in exceptions and errors, which often precede a crash.
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
        {
            // Format the crash report
            string report = $"Crash Report - {System.DateTime.Now}\n";
            report += "----------------------------------------------\n";
            report += $"Type: {type}\n";
            report += $"Message: {logString}\n";
            report += "----------------- Stack Trace -----------------\n";
            report += $"{stackTrace}\n";
            report += "----------------- System Info -----------------\n";
            report += $"Device Model: {SystemInfo.deviceModel}\n";
            report += $"OS: {SystemInfo.operatingSystem}\n";
            report += "----------------------------------------------\n\n";

            // Save the report to a file
            SaveReportToFile(report);

            // In a production game, this report would also be sent to a web service.
            // SendReportToBackend(report);
        }
    }

    /// <summary>
    /// Appends the crash report to the local log file.
    /// </summary>
    private void SaveReportToFile(string report)
    {
        try
        {
            File.AppendAllText(crashLogPath, report);
            Debug.Log($"Crash report saved to: {crashLogPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to write crash report: {e.Message}");
        }
    }
}
