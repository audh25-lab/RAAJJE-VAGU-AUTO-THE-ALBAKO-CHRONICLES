using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "RVA/Inventory Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    [TextArea]
    public string description;
    public Sprite icon;
    public bool isStackable = true;
    public bool isUsable = false;
    // public UnityEvent onUse; // Event to trigger if the item is used
}
