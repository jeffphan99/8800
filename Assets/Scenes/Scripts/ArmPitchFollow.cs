using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Rotates the arms group to follow the camera's vertical pitch.
/// Only runs on the local owner — remote players' arms follow CopyBoneTransforms instead.
/// Place on the arm-only-chracters root GameObject.
/// In LateUpdate so it runs after Animator and CopyBoneTransforms.
/// </summary>
public class ArmPitchFollow : MonoBehaviour
{
    [Tooltip("The PlayerCameraRoot transform (drives camera vertical look)")]
    public Transform cameraRoot;

    [Tooltip("Offset to position arms relative to camera (tweak in editor)")]
    public Vector3 positionOffset = new Vector3(0f, -0.3f, 0.2f);

    private NetworkBehaviour _owner;

    void Start()
    {
        _owner = GetComponentInParent<NetworkBehaviour>();
    }

    void LateUpdate()
    {
        // Only run for the local owning player
        if (_owner == null || !_owner.IsOwner) return;
        if (cameraRoot == null) return;

        transform.rotation = cameraRoot.rotation;
        transform.position = cameraRoot.position + cameraRoot.TransformDirection(positionOffset);
    }
}
