using UnityEngine;

namespace RVA.TAC.UI
{
    public class MapSystem : MonoBehaviour
    {
        public static MapSystem Instance { get; private set; }

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

        public void SetObjectiveIcon(Vector3 position)
        {
            Debug.Log($"MapSystem: Set objective icon at {position}");
        }

        public void HideObjectiveIcon()
        {
            Debug.Log("MapSystem: Hid objective icon");
        }
    }
}
