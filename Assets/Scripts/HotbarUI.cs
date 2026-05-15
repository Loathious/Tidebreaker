using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotbarUI : MonoBehaviour
{
    [SerializeField] private Transform slotsParent;
    [SerializeField] private Color emptyColor    = new Color(0.10f, 0.10f, 0.10f, 0.40f);
    [SerializeField] private Color hasItemColor  = new Color(0.22f, 0.22f, 0.22f, 0.55f);
    [SerializeField] private Color equippedColor = new Color(0.85f, 0.65f, 0.08f, 0.80f);

    private Image[]    slotBackgrounds;
    private Image[]    slotIcons;
    private bool[]     _slotPrevHadItem;     // for detecting "just picked up" transitions
    private const int SlotCount = 1;

    void Awake()
    {
        // Hotbar is valid in all gameplay scenes (Village, Cave, etc.)
    }

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
        slotBackgrounds  = new Image[SlotCount];
        slotIcons        = new Image[SlotCount];
        _slotPrevHadItem = new bool [SlotCount];

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

            // ── Pickup animation ─────────────────────────────────────────────
            // Detect a slot transitioning from empty → has-item (i.e. "just picked up")
            if (hasItem && !_slotPrevHadItem[i])
            {
                StartCoroutine(PickupAnimation(slotBackgrounds[i].rectTransform, slotIcons[i]));
            }
            _slotPrevHadItem[i] = hasItem;
        }
    }

    /// <summary>
    /// Plays a pickup animation on a hotbar slot:
    ///   - golden flash on the slot background
    ///   - punch scale on the icon (0 → 1.4 → 1)
    ///   - 4 small "spark" sprites burst outward from the slot
    /// </summary>
    IEnumerator PickupAnimation(RectTransform slotRect, Image icon)
    {
        if (icon == null) yield break;

        // Burst of sparks
        SpawnPickupSparks(slotRect);

        // Icon punch-scale from 0
        Transform iconT = icon.transform;
        float duration = 0.5f;
        float t = 0f;
        Color baseColor = icon.color;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = t / duration;
            // Ease-out back: peaks above 1 then settles to 1
            float scale;
            if (k < 0.55f) scale = Mathf.Lerp(0f, 1.4f, Mathf.SmoothStep(0f, 1f, k / 0.55f));
            else           scale = Mathf.Lerp(1.4f, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.55f) / 0.45f));
            iconT.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        iconT.localScale = Vector3.one;
    }

    void SpawnPickupSparks(RectTransform slotRect)
    {
        if (slotRect == null) return;

        // Build a 1×1 white sprite once
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        Sprite sparkSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);

        Canvas parentCanvas = slotRect.GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        for (int i = 0; i < 6; i++)
        {
            GameObject spark = new GameObject("PickupSpark");
            spark.transform.SetParent(parentCanvas.transform, false);

            RectTransform sRt = spark.AddComponent<RectTransform>();
            sRt.position = slotRect.position;
            sRt.sizeDelta = new Vector2(6f, 6f);

            Image img = spark.AddComponent<Image>();
            img.sprite = sparkSprite;
            img.color  = new Color(1f, 0.85f, 0.3f, 1f); // gold
            img.raycastTarget = false;

            // Random direction outward
            float angle = (360f / 6f) * i + Random.Range(-15f, 15f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                                      Mathf.Sin(angle * Mathf.Deg2Rad));
            StartCoroutine(AnimateSpark(sRt, img, dir));
        }
    }

    IEnumerator AnimateSpark(RectTransform rt, Image img, Vector2 dir)
    {
        float duration = 0.45f;
        float distance = 50f; // pixels
        Vector3 startPos = rt.position;
        Vector3 endPos   = startPos + (Vector3)(dir * distance);
        float t = 0f;
        while (t < duration && rt != null)
        {
            t += Time.unscaledDeltaTime;
            float k = t / duration;
            rt.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, k));
            img.color = new Color(img.color.r, img.color.g, img.color.b, 1f - k);
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
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

    /// <summary>Shows or hides the hotbar by toggling the GameObject active state.</summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
