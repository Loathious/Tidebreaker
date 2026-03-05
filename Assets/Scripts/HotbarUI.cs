using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotbarUI : MonoBehaviour
{
    [SerializeField] private Transform slotsParent;
    [SerializeField] private Color emptyColor    = new Color(0.10f, 0.10f, 0.10f, 0.40f);
    [SerializeField] private Color hasItemColor  = new Color(0.22f, 0.22f, 0.22f, 0.55f);
    [SerializeField] private Color equippedColor = new Color(0.85f, 0.65f, 0.08f, 0.80f);

    private Image[] slotBackgrounds;
    private Image[] slotIcons;
    private const int SlotCount = 1;

    void Start()
    {
        if (slotsParent == null)
            slotsParent = transform;

        CreateSlots();

        if (Inventory.Instance != null)
        {
            Inventory.Instance.OnInventoryChanged.AddListener(UpdateSlots);
            Inventory.Instance.OnEquippedSlotChanged.AddListener(UpdateEquipped);
        }
    }

    void CreateSlots()
    {
        slotBackgrounds = new Image[SlotCount];
        slotIcons = new Image[SlotCount];

        for (int i = 0; i < SlotCount; i++)
            CreateSlot(i).transform.SetParent(slotsParent, false);

        // Start with nothing equipped
        UpdateEquipped(-1);
    }

    GameObject CreateSlot(int index)
    {
        GameObject slot = new GameObject($"Slot{index}");
        slot.AddComponent<RectTransform>().sizeDelta = new Vector2(44, 44);

        Image slotBg = slot.AddComponent<Image>();
        slotBg.color = emptyColor;
        slotBackgrounds[index] = slotBg;

        // Icon
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(slot.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.sizeDelta = new Vector2(-8, -8);
        iconRect.anchoredPosition = Vector2.zero;
        Image icon = iconObj.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.enabled = false;
        slotIcons[index] = icon;

        // Slot number
        GameObject numObj = new GameObject("Number");
        numObj.transform.SetParent(slot.transform, false);
        RectTransform numRect = numObj.AddComponent<RectTransform>();
        numRect.anchorMin = new Vector2(0, 1);
        numRect.anchorMax = new Vector2(0, 1);
        numRect.pivot = new Vector2(0, 1);
        numRect.sizeDelta = new Vector2(16, 16);
        numRect.anchoredPosition = new Vector2(2, -2);
        TextMeshProUGUI numText = numObj.AddComponent<TextMeshProUGUI>();
        numText.text = (index + 1).ToString();
        numText.fontSize = 10;
        numText.color = new Color(1f, 1f, 1f, 0.55f);

        return slot;
    }

    void UpdateSlots(ItemData[] items)
    {
        int equipped = Inventory.Instance != null ? Inventory.Instance.GetEquippedSlot() : -1;

        for (int i = 0; i < slotIcons.Length && i < items.Length; i++)
        {
            bool hasItem = items[i] != null;
            slotIcons[i].enabled = hasItem && items[i].icon != null;
            if (hasItem && items[i].icon != null)
                slotIcons[i].sprite = items[i].icon;

            slotBackgrounds[i].color = !hasItem ? emptyColor
                : (i == equipped ? equippedColor : hasItemColor);
        }
    }

    void UpdateEquipped(int equippedSlot)
    {
        ItemData[] items = Inventory.Instance?.GetHotbarItems();
        if (items == null) return;

        for (int i = 0; i < slotBackgrounds.Length && i < items.Length; i++)
        {
            slotBackgrounds[i].color = items[i] == null ? emptyColor
                : (i == equippedSlot ? equippedColor : hasItemColor);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            Inventory.Instance?.ToggleEquip(0);
    }
}
