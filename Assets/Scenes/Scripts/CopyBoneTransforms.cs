using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Copies bone transforms from a source skeleton (body) to a target skeleton (arms).
/// Both skeletons must share the same bone names for the bones you want to sync.
/// Place this on the arms root GameObject.
/// </summary>
public class CopyBoneTransforms : MonoBehaviour
{
    [Tooltip("Root bone of the full body skeleton (e.g., the body's Armature/Hips)")]
    public Transform sourceRootBone;

    [Tooltip("Root bone of the arms skeleton (e.g., the arms' Armature/Hips)")]
    public Transform targetRootBone;

    private Dictionary<string, Transform> sourceBones = new Dictionary<string, Transform>();
    private Dictionary<string, Transform> targetBones = new Dictionary<string, Transform>();

    void Start()
    {
        if (sourceRootBone == null || targetRootBone == null)
        {
            Debug.LogError("[CopyBoneTransforms] Source or target root bone not assigned!");
            enabled = false;
            return;
        }

        CacheBones(sourceRootBone, sourceBones);
        CacheBones(targetRootBone, targetBones);

        Debug.Log($"[CopyBoneTransforms] Mapped {targetBones.Count} target bones, {sourceBones.Count} source bones");
    }

    void CacheBones(Transform root, Dictionary<string, Transform> dict)
    {
        foreach (Transform bone in root.GetComponentsInChildren<Transform>())
        {
            if (!dict.ContainsKey(bone.name))
                dict[bone.name] = bone;
        }
    }

    void LateUpdate()
    {
        foreach (var kvp in targetBones)
        {
            if (sourceBones.TryGetValue(kvp.Key, out Transform sourceBone))
            {
                kvp.Value.localPosition = sourceBone.localPosition;
                kvp.Value.localRotation = sourceBone.localRotation;
                kvp.Value.localScale = sourceBone.localScale;
            }
        }
    }
}
