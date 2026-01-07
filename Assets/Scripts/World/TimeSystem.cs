using UnityEngine;
using System;

namespace RVA.TAC.World
{
    public class TimeSystem : MonoBehaviour
    {
        public static TimeSystem Instance { get; private set; }

        public float CurrentHour { get; private set; }

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

        private void Update()
        {
            CurrentHour = (DateTime.Now.Hour + DateTime.Now.Minute / 60f) % 24;
        }
    }
}
