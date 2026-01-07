using UnityEngine;

namespace RVA.TAC.World
{
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

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

        public void SetWeather(string weatherType)
        {
            Debug.Log($"WeatherSystem: Set weather to {weatherType}");
        }
    }
}
