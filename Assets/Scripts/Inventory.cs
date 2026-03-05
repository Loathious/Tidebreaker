using UnityEngine;
using UnityEngine.Events;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    [SerializeField] private int hotbarSize = 1;

    private ItemData[] hotbarItems;
    private int equippedSlot = -1; // -1 means nothing is equipped

    public UnityEvent<ItemData[]> OnInventoryChanged;
    public UnityEvent<int> OnEquippedSlotChanged; // passes equippedSlot (-1 = unequipped)
    public UnityEvent<ItemData> OnItemEquipped;

    // Keep legacy names so SwordPickup and PlayerCombat compile without changes
    public UnityEvent<int> OnSlotChanged => OnEquippedSlotChanged;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        hotbarItems = new ItemData[hotbarSize];
    }

    /// <summary>Adds an item to the first empty hotbar slot. Does NOT auto-equip.</summary>
    public bool AddItem(ItemData item)
    {
        for (int i = 0; i < hotbarItems.Length; i++)
        {
            if (hotbarItems[i] == null)
            {
                hotbarItems[i] = item;
                OnInventoryChanged?.Invoke(hotbarItems);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Toggles equip for the given slot:
    /// - If the slot is already equipped → unequips it (item stays in inventory).
    /// - If the slot has an item and is not equipped → equips it.
    /// </summary>
    public void ToggleEquip(int slot)
    {
        if (slot < 0 || slot >= hotbarItems.Length) return;
        if (hotbarItems[slot] == null) return;

        if (equippedSlot == slot)
        {
            equippedSlot = -1;
            OnItemEquipped?.Invoke(null);
        }
        else
        {
            equippedSlot = slot;
            OnItemEquipped?.Invoke(hotbarItems[slot]);
        }

        OnEquippedSlotChanged?.Invoke(equippedSlot);
    }

    /// <summary>Drops the item from the slot entirely, unequipping it first if needed.</summary>
    public void RemoveItem(int slot)
    {
        if (slot < 0 || slot >= hotbarItems.Length) return;
        if (hotbarItems[slot] == null) return;

        hotbarItems[slot] = null;

        if (equippedSlot == slot)
        {
            equippedSlot = -1;
            OnItemEquipped?.Invoke(null);
            OnEquippedSlotChanged?.Invoke(equippedSlot);
        }

        OnInventoryChanged?.Invoke(hotbarItems);
    }

    public ItemData GetEquippedItem() => equippedSlot >= 0 ? hotbarItems[equippedSlot] : null;
    public ItemData[] GetHotbarItems() => hotbarItems;
    public int GetEquippedSlot() => equippedSlot;

    // Legacy compatibility
    public ItemData GetCurrentItem() => GetEquippedItem();
    public int GetCurrentSlot() => equippedSlot;
    public void SelectSlot(int slot) => ToggleEquip(slot);
}
