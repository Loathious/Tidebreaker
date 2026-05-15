using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public ItemType itemType;
    public int damage;
    public float attackCooldown = 0.5f;
    [Tooltip("Max hits before item breaks. 0 = unbreakable.")]
    public int maxUses = 0;
}

public enum ItemType
{
    Weapon,
    Consumable,
    QuestItem
}