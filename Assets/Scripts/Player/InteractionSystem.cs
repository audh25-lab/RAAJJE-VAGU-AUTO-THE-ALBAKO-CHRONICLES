using UnityEngine;

namespace RVA.TAC.Player
{
    public class InteractionSystem : MonoBehaviour
    {
        public static InteractionSystem Instance { get; private set; }

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

        public void Interact()
        {
            Debug.Log("InteractionSystem: Interacted");
        }
    }
}
