using UnityEngine;

/// <summary>
/// Moves this transform to match a target bone every frame.
/// Use this to keep weapons (on default layer) following the body skeleton's Hand_R,
/// so weapons are visible to all players regardless of body/arms visibility.
/// Place on the weapon holder object.
/// </summary>
public class WeaponBoneFollow : MonoBehaviour
{
    [Tooltip("The Hand_R bone from the body (armless) skeleton")]
    public Transform targetBone;

    void LateUpdate()
    {
        if (targetBone == null) return;
        transform.position = targetBone.position;
        transform.rotation = targetBone.rotation;
    }
}
