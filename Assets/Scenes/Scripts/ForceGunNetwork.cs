using Unity.Netcode;
using UnityEngine;
using StarterAssets;

public class ForceGunNetwork : NetworkBehaviour
{
    [ServerRpc(RequireOwnership = false)]
    public void RequestPushPlayerServerRpc(ulong targetClientId, Vector3 force)
    {
        // Broadcast to all clients; only the target applies the force
        ApplyPushClientRpc(targetClientId, force);
    }

    [ClientRpc]
    private void ApplyPushClientRpc(ulong targetClientId, Vector3 force)
    {
        if (PlayerHealth.LocalPlayer == null) return;

        var health = PlayerHealth.LocalPlayer.GetComponent<PlayerHealth>();
        if (health == null || health.OwnerClientId != targetClientId) return;

        FirstPersonController fpc = PlayerHealth.LocalPlayer.GetComponent<FirstPersonController>();
        if (fpc != null)
        {
            fpc.AddExternalForce(force);
            Debug.Log($"[ForceGunNetwork] Pushed by force gun: {force}");
        }
    }
}
