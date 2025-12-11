using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

public class ToolbarUI_Simple : MonoBehaviour
{
    [System.Serializable]
    public class WeaponSlot
    {
        [Header("UI References")]
        public GameObject slotObject;      
        public Text keyText;               
        public Text nameText;              
        public GameObject highlight;       

        [Header("Weapon Info")]
        public string weaponName = "Weapon";

        [HideInInspector] public bool isSelected = false;
    }

    [Header("Weapon Slots")]
    public WeaponSlot[] weaponSlots = new WeaponSlot[4];

    [Header("Weapon Names")]
    public string gunName = "Gun";
    public string flashlightName = "Light";
    public string repairName = "Repair";
    public string bananaName = "Banana";


    [Header("References")]
    public WeaponSwitcher weaponSwitcher;

    [Header("Colors (Optional)")]
    public Color normalTextColor = Color.white;
    public Color selectedTextColor = Color.yellow;
    public bool changeTextColorOnSelect = false;

    void Start()
    {
        FindReferences();
        SetupWeaponNames();
        InitializeSlots();
    }

    void FindReferences()
    {
        if (weaponSwitcher == null)
        {
            weaponSwitcher = FindObjectOfType<WeaponSwitcher>();
            if (weaponSwitcher == null)
            {
                Debug.LogError("[ToolbarUI] WeaponSwitcher not found!");
            }
            else
            {
                Debug.Log("[ToolbarUI] WeaponSwitcher found!");
            }
        }
    }

    void SetupWeaponNames()
    {

        if (weaponSlots.Length > 0) weaponSlots[0].weaponName = gunName;
        if (weaponSlots.Length > 1) weaponSlots[1].weaponName = flashlightName;
        if (weaponSlots.Length > 2) weaponSlots[2].weaponName = repairName;
        if (weaponSlots.Length > 3) weaponSlots[3].weaponName = bananaName;
    }

    void InitializeSlots()
    {
        Debug.Log($"[ToolbarUI] Initializing {weaponSlots.Length} slots...");

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            Debug.Log($"[ToolbarUI] --- Slot {i} ({weaponSlots[i].weaponName}) ---");

            // Set key text (1, 2, 3, 4)
            if (weaponSlots[i].keyText != null)
            {
                weaponSlots[i].keyText.text = (i + 1).ToString();
                Debug.Log($"[ToolbarUI] Slot {i}: KeyText set to '{i + 1}'");
            }
            else
            {
                Debug.LogWarning($"[ToolbarUI] Slot {i}: KeyText is NULL!");
            }

            // Set weapon name
            if (weaponSlots[i].nameText != null)
            {
                weaponSlots[i].nameText.text = weaponSlots[i].weaponName;
                weaponSlots[i].nameText.color = normalTextColor;
                Debug.Log($"[ToolbarUI] Slot {i}: NameText set to '{weaponSlots[i].weaponName}'");
            }
            else
            {
                Debug.LogWarning($"[ToolbarUI] Slot {i}: NameText is NULL!");
            }

            // Hide highlight initially
            if (weaponSlots[i].highlight != null)
            {
                weaponSlots[i].highlight.SetActive(false);
                Debug.Log($"[ToolbarUI] Slot {i}: Highlight '{weaponSlots[i].highlight.name}' found and disabled");
            }
            else
            {
                Debug.LogWarning($"[ToolbarUI] Slot {i}: Highlight is NULL!");
            }
        }

        Debug.Log("[ToolbarUI] Initialization complete!");
    }

    void Update()
    {
        UpdateWeaponSelection();
    }

    void UpdateWeaponSelection()
    {
        if (weaponSwitcher == null)
        {
            Debug.LogWarning("[ToolbarUI] WeaponSwitcher is null in Update!");
            return;
        }

        if (weaponSwitcher.weapons == null)
        {
            Debug.LogWarning("[ToolbarUI] weaponSwitcher.weapons array is null!");
            return;
        }

        // Check which weapon is currently active
        for (int i = 0; i < weaponSlots.Length && i < weaponSwitcher.weapons.Length; i++)
        {
            if (weaponSwitcher.weapons[i] == null)
            {
                Debug.LogWarning($"[ToolbarUI] Weapon at index {i} is NULL!");
                continue;
            }

            bool isSelected = weaponSwitcher.weapons[i].gameObject.activeSelf;

            // Only update if selection changed
            if (weaponSlots[i].isSelected != isSelected)
            {
                Debug.Log($"[ToolbarUI] Slot {i} selection changed: {weaponSlots[i].isSelected} -> {isSelected}");
                weaponSlots[i].isSelected = isSelected;
                UpdateSlotVisuals(i, isSelected);
            }
        }
    }

    void UpdateSlotVisuals(int slotIndex, bool isSelected)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return;

        WeaponSlot slot = weaponSlots[slotIndex];

        Debug.Log($"[ToolbarUI] UpdateSlotVisuals: Slot {slotIndex} ({slot.weaponName}), isSelected: {isSelected}");

        // Show/hide highlight
        if (slot.highlight != null)
        {
            slot.highlight.SetActive(isSelected);
            Debug.Log($"[ToolbarUI] Slot {slotIndex}: Highlight set to {isSelected}, actually is: {slot.highlight.activeSelf}");
        }
        else
        {
            Debug.LogError($"[ToolbarUI] Slot {slotIndex}: Highlight is NULL, cannot update!");
        }

        // Change text color if enabled
        if (changeTextColorOnSelect)
        {
            Color targetColor = isSelected ? selectedTextColor : normalTextColor;

            if (slot.keyText != null)
            {
                slot.keyText.color = targetColor;
            }

            if (slot.nameText != null)
            {
                slot.nameText.color = targetColor;
            }
        }

        Debug.Log($"[ToolbarUI] Weapon slot {slotIndex + 1} ({slot.weaponName}) " + (isSelected ? "SELECTED" : "deselected"));
    }

    public void SetWeaponName(int slotIndex, string newName)
    {
        if (slotIndex >= 0 && slotIndex < weaponSlots.Length)
        {
            weaponSlots[slotIndex].weaponName = newName;
            if (weaponSlots[slotIndex].nameText != null)
            {
                weaponSlots[slotIndex].nameText.text = newName;
            }
        }
    }
}