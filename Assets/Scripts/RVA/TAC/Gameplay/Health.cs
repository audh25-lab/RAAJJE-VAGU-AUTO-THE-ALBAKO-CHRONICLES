using UnityEngine;
using System;

public class Health : MonoBehaviour
{
    public event Action<float> OnHealthChanged;
    public event Action OnDeath;

    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetMaxHealth()
    {
        return maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth);
        Debug.Log($"{gameObject.name} took {amount} damage. Health is now {currentHealth}.");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (currentHealth <= 0) return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth);
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} has been defeated.");
        OnDeath?.Invoke();

        // In a real game, you might disable the object, play a death animation, etc.
        // For now, we'll just destroy it.
        Destroy(gameObject, 2f); // Destroy after 2 seconds to allow other systems to react.
    }
}
