using UnityEngine;
using System;

namespace RVA.TAC.Core
{
    /// <summary>
    /// A reusable component for managing the health of any game entity (Player, NPC, Vehicle).
    /// Handles taking damage, healing, and triggering a death event.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        public float currentHealth { get; private set; }

        public event Action OnDeath;
        public event Action<float> OnHealthChanged;

        public float MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        /// <summary>
        /// Applies a specified amount of damage to the entity.
        /// </summary>
        /// <param name="damageAmount">The amount of damage to apply.</param>
        public void TakeDamage(float damageAmount)
        {
            if (IsDead) return;

            currentHealth -= damageAmount;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            OnHealthChanged?.Invoke(currentHealth);

            if (IsDead)
            {
                OnDeath?.Invoke();
            }
        }

        /// <summary>
        /// Heals the entity by a specified amount.
        /// </summary>
        /// <param name="healAmount">The amount of health to restore.</param>
        public void Heal(float healAmount)
        {
            if (IsDead) return;

            currentHealth += healAmount;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            OnHealthChanged?.Invoke(currentHealth);
        }

        /// <summary>
        /// Instantly kills the entity.
        /// </summary>
        public void Kill()
        {
            TakeDamage(maxHealth);
        }
    }
}
