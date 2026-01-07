using UnityEngine;
using System.Collections.Generic;

namespace RVA.TAC.Player
{
    public class InventorySystem : MonoBehaviour
    {
        public static InventorySystem Instance { get; private set; }

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

        public void AddItem(string itemName)
        {
            Debug.Log($"InventorySystem: Added item {itemName}");
        }
    }
}
