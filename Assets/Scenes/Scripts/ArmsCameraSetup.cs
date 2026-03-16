using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Two-camera FPS setup: attaches an arms-only camera as a child of the main camera.
/// The main camera excludes the Arms layer; the arms camera renders only it.
/// Place this on the player prefab root — it runs once on the local owner's camera.
/// </summary>
public class ArmsCameraSetup : MonoBehaviour
{
    [Header("Arms Camera Settings")]
    [Tooltip("Layer name for first-person arms/weapons")]
    public string armsLayerName = "Arms";
    [Tooltip("FOV for arms camera (wider prevents clipping near body)")]
    public float armsFOV = 70f;
    [Tooltip("Near clip for arms camera (tight to avoid z-fighting)")]
    public float armsNearClip = 0.01f;

    private Camera armsCamera;

    /// <summary>
    /// Call this after the main camera is bound (e.g., from PlayerHealth.SetupOwner).
    /// Pass the main camera that Cinemachine is rendering through.
    /// </summary>
    public void SetupArmsCamera(Camera mainCamera)
    {
        if (mainCamera == null)
        {
            Debug.LogError("[ArmsCameraSetup] Main camera is null!");
            return;
        }

        int armsLayer = LayerMask.NameToLayer(armsLayerName);
        if (armsLayer == -1)
        {
            Debug.LogError($"[ArmsCameraSetup] Layer '{armsLayerName}' not found! Add it in Project Settings > Tags and Layers.");
            return;
        }

        // Main camera: render everything EXCEPT the arms layer
        mainCamera.cullingMask &= ~(1 << armsLayer);

        // Check if arms camera already exists
        if (armsCamera != null) return;

        // Create arms camera as child of main camera
        GameObject armsCamObj = new GameObject("ArmsCamera");
        armsCamObj.transform.SetParent(mainCamera.transform, false);
        armsCamObj.transform.localPosition = Vector3.zero;
        armsCamObj.transform.localRotation = Quaternion.identity;

        armsCamera = armsCamObj.AddComponent<Camera>();
        armsCamera.fieldOfView = armsFOV;
        armsCamera.nearClipPlane = armsNearClip;
        armsCamera.farClipPlane = mainCamera.farClipPlane;
        armsCamera.clearFlags = CameraClearFlags.Depth;
        armsCamera.cullingMask = 1 << armsLayer;
        armsCamera.depth = mainCamera.depth + 1;

        // URP: register as overlay camera on the main camera's stack
        var mainCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        var armsCameraData = armsCamObj.AddComponent<UniversalAdditionalCameraData>();

        if (mainCameraData != null && armsCameraData != null)
        {
            armsCameraData.renderType = CameraRenderType.Overlay;
            mainCameraData.cameraStack.Add(armsCamera);
        }

        Debug.Log($"[ArmsCameraSetup] Arms camera created — layer: {armsLayerName} ({armsLayer}), FOV: {armsFOV}");
    }
}
