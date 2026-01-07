using UnityEngine;

namespace RVA.TAC.Core
{
    public class CombatSystem : MonoBehaviour
    {
        public static CombatSystem Instance { get; private set; }

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

        public void ProcessAttack(GameObject attacker, GameObject target)
        {
            Debug.Log($"CombatSystem: {attacker.name} attacked {target.name}");
        }
    }
}
