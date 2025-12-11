using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class WeaponSwitcher : MonoBehaviour
    {
        [Header("Weapons")]
        public WeaponBase[] weapons; // Array of all weapons (gun, melee, flashlight)
        private int currentWeaponIndex = 0;

        [Header("Shared UI")]
        public Text statusText; // Shows ammo/battery/etc
        public Text actionText; // Shows reload/recharge/etc
        public Camera playerCamera;

        [Header("Input")]
        public StarterAssetsInputs input;

        void Start()
        {
            // Get input component if not assigned
            if (input == null)
            {
                input = GetComponent<StarterAssetsInputs>();
                if (input == null)
                {
                    Debug.LogError("StarterAssetsInputs component not found!");
                }
            }

            // Setup all weapons with shared references
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null)
                {
                    Debug.Log($"Setting up weapon {i}: {weapons[i].gameObject.name}");
                    weapons[i].SetSharedReferences(playerCamera, statusText, actionText);
                    weapons[i].gameObject.SetActive(false);
                }
                else
                {
                    Debug.LogWarning($"Weapon slot {i} is NULL!");
                }
            }

            // Activate first weapon
            SelectWeapon(0);
        }

        void Update()
        {
            // Don't switch if current weapon is busy
            if (weapons[currentWeaponIndex] != null && weapons[currentWeaponIndex].IsBusy())
                return;

            // Don't switch during minigame
            Terminal activeTerminal = FindObjectOfType<Terminal>();
            if (activeTerminal != null && activeTerminal.minigameActive)
                return;

            // Check for weapon slot input
            int slotInput = input.GetWeaponSlotInput();
            if (slotInput >= 0)
            {
                SelectWeapon(slotInput);
            }
        }

        void SelectWeapon(int index)
        {
            if (index < 0 || index >= weapons.Length) return;
            if (weapons[index] == null) return;
            if (index == currentWeaponIndex && weapons[index].gameObject.activeSelf) return; // Already equipped

            // Unequip current weapon
            if (weapons[currentWeaponIndex] != null)
            {
                weapons[currentWeaponIndex].OnUnequip();
                weapons[currentWeaponIndex].gameObject.SetActive(false);
            }

            // Equip new weapon
            currentWeaponIndex = index;
            weapons[currentWeaponIndex].gameObject.SetActive(true);
            weapons[currentWeaponIndex].OnEquip();

            Debug.Log("Switched to: " + weapons[index].gameObject.name);
        }

        void CycleWeapon(int direction)
        {
            int newIndex = currentWeaponIndex + direction;

            // Wrap around
            if (newIndex >= weapons.Length)
            {
                newIndex = 0;
            }
            else if (newIndex < 0)
            {
                newIndex = weapons.Length - 1;
            }

            SelectWeapon(newIndex);
        }

        public WeaponBase GetCurrentWeapon()
        {
            return weapons[currentWeaponIndex];
        }

    }
}