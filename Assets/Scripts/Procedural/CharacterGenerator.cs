using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace RVA.TAC.Procedural
{
    public class CharacterGenerator : MonoBehaviour
    {
        public static CharacterGenerator Instance { get; private set; }

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

        public void GenerateCharacters()
        {
            List<object> characters = new List<object>();
            for (int i = 0; i < 100; i++)
            {
                characters.Add(new { name = $"Character_{i}", age = Random.Range(18, 60) });
            }

            string json = MiniJSON.Encode(new Dictionary<string, object> { { "characters", characters } });
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "Procedural/characters.json"), json);
            Debug.Log("Generated 100 characters.");
        }
    }
}
