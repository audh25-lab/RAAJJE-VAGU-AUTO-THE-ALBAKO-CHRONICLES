using UnityEngine;

namespace RVA.TAC.UI
{
    public class NotificationSystem : MonoBehaviour
    {
        public static NotificationSystem Instance { get; private set; }

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

        public void ShowNotification(string message)
        {
            Debug.Log($"NotificationSystem: {message}");
        }
    }
}
