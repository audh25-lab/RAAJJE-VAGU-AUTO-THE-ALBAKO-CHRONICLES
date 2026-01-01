using UnityEngine;

// ScriptableObject to define a weapon's properties.
[CreateAssetMenu(fileName = "New Weapon", menuName = "RVA/Weapon")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public enum WeaponType { Melee, Ranged }
    public WeaponType type;

    [Header("Combat Properties")]
    public float damage = 25f;
    public float range = 50f; // Used for ranged weapons or melee area of effect.
    public float attackRate = 0.5f; // Time between attacks.
}

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

    // A universal method for performing an attack.
    public void PerformAttack(Transform attacker, WeaponData weapon)
    {
        if (weapon.type == WeaponData.WeaponType.Ranged)
        {
            PerformRangedAttack(attacker, weapon);
        }
        else if (weapon.type == WeaponData.WeaponType.Melee)
        {
            PerformMeleeAttack(attacker, weapon);
        }
    }

    private void PerformRangedAttack(Transform attacker, WeaponData weapon)
    {
        RaycastHit hit;
        // The ray is cast from the attacker's position, forward in their direction.
        if (Physics.Raycast(attacker.position, attacker.forward, out hit, weapon.range))
        {
            Debug.DrawLine(attacker.position, hit.point, Color.red, 1.0f);
            
            // Check if the hit object has a Health component.
            Health targetHealth = hit.collider.GetComponent<Health>();
            if (targetHealth != null)
            {
                Debug.Log($"{attacker.name} hit {hit.collider.name} with {weapon.weaponName} for {weapon.damage} damage.");
                targetHealth.TakeDamage(weapon.damage);
            }
        }
        else
        {
            Debug.DrawRay(attacker.position, attacker.forward * weapon.range, Color.yellow, 1.0f);
            Debug.Log($"{attacker.name}'s ranged attack missed.");
        }
    }

    private void PerformMeleeAttack(Transform attacker, WeaponData weapon)
    {
        // Find all colliders within a sphere in front of the attacker.
        Vector3 attackCenter = attacker.position + attacker.forward * (weapon.range / 2);
        Collider[] hits = Physics.OverlapSphere(attackCenter, weapon.range / 2);

        bool hitSomething = false;
        foreach (var hit in hits)
        {
            // Don't hit the attacker themselves.
            if (hit.transform == attacker) continue;

            Health targetHealth = hit.GetComponent<Health>();
            if (targetHealth != null)
            {
                Debug.Log($"{attacker.name} hit {hit.name} with a melee attack using {weapon.weaponName}.");
                targetHealth.TakeDamage(weapon.damage);
                hitSomething = true;
            }
        }

        if (!hitSomething)
        {
            Debug.Log($"{attacker.name}'s melee attack missed.");
        }
    }
}
