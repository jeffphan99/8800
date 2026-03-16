using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Toggles body/arms mesh visibility based on ownership.
/// Local player: hides body renderers, shows arms on Arms layer (overlay camera, no z-clipping).
/// Remote players: shows body renderers, shows arms on Default layer (visible to all cameras).
/// Place on the player prefab root.
/// </summary>
public class PlayerMeshVisibility : NetworkBehaviour
{
    [Tooltip("Parent of armless body meshes (visible to other players)")]
    public GameObject bodyGroup;

    [Tooltip("Parent of arm-only meshes (local = Arms layer, remote = Default layer)")]
    public GameObject armsGroup;

    [Tooltip("Must match the Arms layer name in Project Settings > Tags and Layers")]
    public string armsLayerName = "Arms";

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Local player: hide body, show arms on Arms layer (overlay camera renders them)
            SetRenderersEnabled(bodyGroup, false);
            if (armsGroup != null) armsGroup.SetActive(true);
        }
        else
        {
            // Remote player: show body + arms on Default layer (visible to all cameras)
            SetRenderersEnabled(bodyGroup, true);
            if (armsGroup != null)
            {
                armsGroup.SetActive(true);
                SetLayerRecursive(armsGroup, LayerMask.NameToLayer("Default"));

                // Disable ArmPitchFollow — remote arms follow CopyBoneTransforms instead
                var pitchFollow = armsGroup.GetComponent<ArmPitchFollow>();
                if (pitchFollow != null) pitchFollow.enabled = false;
            }
        }
    }

    private void SetRenderersEnabled(GameObject group, bool enabled)
    {
        if (group == null) return;
        foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = enabled;
        }
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
