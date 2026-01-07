using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace RVA.TAC.Procedural
{
    public class VehicleGenerator : MonoBehaviour
    {
        public static VehicleGenerator Instance { get; private set; }

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

        public void GenerateVehicles()
        {
            List<object> vehicles = new List<object>();
            for (int i = 0; i < 50; i++)
            {
                vehicles.Add(new { type = $"Vehicle_{i}", color = "Red" });
            }

            string json = MiniJSON.Encode(new Dictionary<string, object> { { "vehicles", vehicles } });
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "Procedural/vehicles.json"), json);
            Debug.Log("Generated 50 vehicles.");
        }
    }
}
