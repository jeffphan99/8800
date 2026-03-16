using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Sets the arms group layer at start based on ownership.
/// Owner: Arms layer (rendered by overlay camera only).
/// Non-owner: Default layer (rendered by main camera, visible to all).
/// Place on arm-only-chracters root.
/// </summary>
public class ArmsLayerSetup : NetworkBehaviour
{
    public string armsLayerName = "Arms";

    public override void OnNetworkSpawn()
    {
        int layer = IsOwner
            ? LayerMask.NameToLayer(armsLayerName)
            : LayerMask.NameToLayer("Default");

        SetLayerRecursive(gameObject, layer);
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
