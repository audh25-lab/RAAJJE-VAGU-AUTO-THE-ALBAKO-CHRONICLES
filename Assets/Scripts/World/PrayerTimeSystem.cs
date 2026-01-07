using UnityEngine;
using System;

namespace RVA.TAC.World
{
    public class PrayerTimeSystem : MonoBehaviour
    {
        public static PrayerTimeSystem Instance { get; private set; }

        public event Action<string> OnPrayerTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }
        }

        public void CheckPrayerTimes()
        {
            // In a real implementation, this would check the current time against a schedule of prayer times.
            // For now, we'll just simulate a prayer time event.
            OnPrayerTime?.Invoke("Dhuhr");
        }
    }
}
