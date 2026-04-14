using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class WeaponSwitcher : NetworkBehaviour
    {
        [Header("Weapons")]
        public WeaponBase[] weapons;
        private int currentWeaponIndex = 0;

        [Header("Shared UI")]
        public Text statusText;
        public Text actionText;
        public Camera playerCamera;

        [Header("Input")]
        public StarterAssetsInputs input;

        [Header("Audio")]
        public AudioClip switchSound;
        private AudioSource audioSource;

        [Header("Swap Animation")]
        public Animator playerAnimator;

        private bool isSwitching = false;

        private NetworkVariable<int> networkWeaponIndex = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        void Start()
        {
            if (input == null)
            {
                input = GetComponent<StarterAssetsInputs>();
                if (input == null)
                    Debug.LogError("StarterAssetsInputs component not found!");
            }

            if (statusText == null)
            {
                GameObject obj = GameObject.Find("Ammo");
                if (obj != null) statusText = obj.GetComponent<Text>();
            }
            if (actionText == null)
            {
                GameObject obj = GameObject.Find("Reload");
                if (obj != null) actionText = obj.GetComponent<Text>();
            }
            if (playerCamera == null)
                playerCamera = Camera.main;

            if (playerAnimator == null)
                playerAnimator = GetComponentInParent<Animator>();

            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null)
                {
                    weapons[i].SetSharedReferences(playerCamera, statusText, actionText);
                    weapons[i].gameObject.SetActive(false);
                }
                else
                {
                    Debug.LogWarning($"Weapon slot {i} is NULL!");
                }
            }

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            // Initial equip is instant — no swap animation on startup
            EquipDirect(0);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // Non-owners apply the synced weapon index on spawn (handles late joiners)
            if (!IsOwner)
                EquipDirect(networkWeaponIndex.Value);
        }

        void Update()
        {
            if (!IsOwner) return;
            if (isSwitching) return;
            if (weapons[currentWeaponIndex] != null && weapons[currentWeaponIndex].IsBusy()) return;
            if (Terminal.AnyMinigameActive) return;

            int slotInput = input.GetWeaponSlotInput();
            if (slotInput >= 0)
                SelectWeapon(slotInput);
        }

        void SelectWeapon(int index)
        {
            if (index < 0 || index >= weapons.Length) return;
            if (weapons[index] == null) return;
            if (index == currentWeaponIndex && weapons[index].gameObject.activeSelf) return;

            // Owner runs locally for immediate feel; broadcasts to all other clients
            StartCoroutine(SwapRoutine(index));
            BroadcastSwapServerRpc(index, OwnerClientId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void BroadcastSwapServerRpc(int newIndex, ulong ownerClientId)
        {
            networkWeaponIndex.Value = newIndex;
            PlaySwapClientRpc(newIndex, ownerClientId);
        }

        [ClientRpc]
        private void PlaySwapClientRpc(int newIndex, ulong ownerClientId)
        {
            // Owner already ran SwapRoutine locally
            if (NetworkManager.Singleton.LocalClientId == ownerClientId) return;
            StartCoroutine(SwapRoutine(newIndex));
        }

        IEnumerator SwapRoutine(int newIndex)
        {
            isSwitching = true;

            if (playerAnimator != null)
                playerAnimator.SetBool("Swap", true);

            // Wait for the halfway point — arms fully down
            yield return new WaitForSeconds(0.25f);

            // Swap the weapon at the bottom of the arc
            if (weapons[currentWeaponIndex] != null)
            {
                weapons[currentWeaponIndex].OnUnequip();
                weapons[currentWeaponIndex].gameObject.SetActive(false);
            }

            currentWeaponIndex = newIndex;
            weapons[currentWeaponIndex].gameObject.SetActive(true);
            weapons[currentWeaponIndex].OnEquip();

            if (audioSource != null && switchSound != null)
                audioSource.PlayOneShot(switchSound);

            // Wait for the raise-back second half
            yield return new WaitForSeconds(0.25f);

            if (playerAnimator != null)
                playerAnimator.SetBool("Swap", false);

            isSwitching = false;
        }

        // Called by WeaponBase when shooting — owner already played it locally, this syncs to others
        public void NotifyShoot(ulong ownerClientId)
        {
            if (!IsSpawned) return;
            BroadcastShootServerRpc(ownerClientId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void BroadcastShootServerRpc(ulong ownerClientId)
        {
            PlayShootAnimClientRpc(ownerClientId);
        }

        [ClientRpc]
        private void PlayShootAnimClientRpc(ulong ownerClientId)
        {
            // Owner already triggered it locally
            if (NetworkManager.Singleton.LocalClientId == ownerClientId) return;
            if (playerAnimator != null)
                playerAnimator.SetTrigger("Shoot");
        }

        // Used on startup or for non-owner sync — no animation, just swap immediately
        void EquipDirect(int index)
        {
            if (index < 0 || index >= weapons.Length) return;
            if (weapons[index] == null) return;

            if (currentWeaponIndex != index && weapons[currentWeaponIndex] != null)
                weapons[currentWeaponIndex].gameObject.SetActive(false);

            currentWeaponIndex = index;
            weapons[currentWeaponIndex].gameObject.SetActive(true);
            weapons[currentWeaponIndex].OnEquip();
        }

        void CycleWeapon(int direction)
        {
            int newIndex = currentWeaponIndex + direction;
            if (newIndex >= weapons.Length) newIndex = 0;
            else if (newIndex < 0) newIndex = weapons.Length - 1;
            SelectWeapon(newIndex);
        }

        public WeaponBase GetCurrentWeapon()
        {
            return weapons[currentWeaponIndex];
        }
    }
}
