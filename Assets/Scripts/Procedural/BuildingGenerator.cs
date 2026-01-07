using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace RVA.TAC.Procedural
{
    public class BuildingGenerator : MonoBehaviour
    {
        public static BuildingGenerator Instance { get; private set; }

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

        public void GenerateBuildings()
        {
            List<object> buildings = new List<object>();
            for (int i = 0; i < 20; i++)
            {
                buildings.Add(new { type = $"Building_{i}", size = "Large" });
            }

            string json = MiniJSON.Encode(new Dictionary<string, object> { { "buildings", buildings } });
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "Procedural/buildings.json"), json);
            Debug.Log("Generated 20 buildings.");
        }
    }
}
