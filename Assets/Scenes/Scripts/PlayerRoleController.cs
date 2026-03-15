using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerRoleController : NetworkBehaviour
{
    [Header("Skin Models (assign in Inspector)")]
    public GameObject[] skinModels;

    [Header("Helmet Slot")]
    public GameObject helmetSlot;

    private NetworkVariable<int> networkRole = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public PlayerRole Role => (PlayerRole)networkRole.Value;

    private Text roleText;

    public override void OnNetworkSpawn()
    {
        networkRole.OnValueChanged += OnRoleChanged;

        // Apply current skin immediately (handles late joiners)
        ApplySkin();

        if (IsOwner)
        {
            UpdateRoleUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        networkRole.OnValueChanged -= OnRoleChanged;
    }

    private void OnRoleChanged(int previousValue, int newValue)
    {
        ApplySkin();

        if (IsOwner)
        {
            UpdateRoleUI();
        }
    }

    /// <summary>
    /// Called by GameManager on the server to assign a role.
    /// </summary>
    public void SetRole(PlayerRole role)
    {
        if (!IsServer) return;
        networkRole.Value = (int)role;
    }

    private void ApplySkin()
    {
        int skinIndex = PlayerRoleHelper.SkinIndex(Role);

        for (int i = 0; i < skinModels.Length; i++)
        {
            if (skinModels[i] != null)
            {
                skinModels[i].SetActive(i == skinIndex);
            }
        }
    }

    private void UpdateRoleUI()
    {
        if (roleText == null)
        {
            GameObject roleObj = GameObject.Find("RoleText");
            if (roleObj != null)
                roleText = roleObj.GetComponent<Text>();
        }

        if (roleText != null)
        {
            roleText.text = PlayerRoleHelper.DisplayName(Role);
        }
    }
}
