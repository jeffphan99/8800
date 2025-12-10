using UnityEngine;
using System.Collections.Generic;

public class LightSwitch : MonoBehaviour
{
    [Header("Zone Settings")]
    public string zoneName = "Room 1";

    [Header("Lights to Control")]
    public List<RoomLight> controlledLights = new List<RoomLight>();

    [Header("Auto-Find Lights")]
    public bool autoFindLightsOnStart = true;
    public float lightDetectionRadius = 20f;
    public LayerMask lightLayer; // Optional: only find lights on specific layer

    [Header("Switch Visual")]
    public GameObject switchLever; // The physical switch model
    public Vector3 onRotation = new Vector3(-30, 0, 0);
    public Vector3 offRotation = new Vector3(30, 0, 0);
    public float switchSpeed = 8f;

    [Header("Indicator Light")]
    public Light indicatorLight; // Small light on the switch itself
    public Color onColor = Color.green;
    public Color offColor = Color.red;

    [Header("Audio")]
    public AudioClip switchSound;
    private AudioSource audioSource;

    private bool lightsAreOn = true;
    private bool isAnimating = false;

    void Start()
    {
        SetupAudioSource();

        if (autoFindLightsOnStart)
        {
            FindNearbyLights();
        }

        // Set initial state
        lightsAreOn = true;
        UpdateIndicator(true);
        UpdateSwitchVisual(true); // Instant update on start
    }

    void SetupAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.maxDistance = 15f;
        }
    }

    void FindNearbyLights()
    {
        controlledLights.Clear();

        // Find all RoomLight components in range
        Collider[] colliders = Physics.OverlapSphere(transform.position, lightDetectionRadius);

        foreach (Collider col in colliders)
        {
            RoomLight light = col.GetComponent<RoomLight>();
            if (light != null)
            {
                controlledLights.Add(light);
            }
        }

        // If no colliders found, try finding by distance
        if (controlledLights.Count == 0)
        {
            RoomLight[] allLights = FindObjectsOfType<RoomLight>();
            foreach (RoomLight light in allLights)
            {
                float distance = Vector3.Distance(transform.position, light.transform.position);
                if (distance <= lightDetectionRadius)
                {
                    controlledLights.Add(light);
                }
            }
        }

        Debug.Log($"[{zoneName}] LightSwitch found {controlledLights.Count} lights in radius {lightDetectionRadius}");
    }

    public void Toggle()
    {
        if (isAnimating) return; // Prevent rapid toggling

        lightsAreOn = !lightsAreOn;

        // Toggle all controlled lights (unless broken)
        int toggledCount = 0;
        foreach (RoomLight light in controlledLights)
        {
            if (light != null && !light.isBroken)
            {
                if (lightsAreOn)
                    light.TurnOn();
                else
                    light.TurnOff();

                toggledCount++;
            }
        }

        // Update visuals
        UpdateSwitchVisual(false);
        UpdateIndicator(false);

        // Play sound
        if (switchSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(switchSound, 0.5f);
        }

        Debug.Log($"[{zoneName}] Lights toggled: {(lightsAreOn ? "ON" : "OFF")} ({toggledCount}/{controlledLights.Count} lights affected)");
    }

    void UpdateSwitchVisual(bool instant)
    {
        if (switchLever == null) return;

        Quaternion targetRot = Quaternion.Euler(lightsAreOn ? onRotation : offRotation);

        if (instant)
        {
            switchLever.transform.localRotation = targetRot;
        }
        else
        {
            StartCoroutine(AnimateSwitch(targetRot));
        }
    }

    System.Collections.IEnumerator AnimateSwitch(Quaternion targetRotation)
    {
        isAnimating = true;

        while (Quaternion.Angle(switchLever.transform.localRotation, targetRotation) > 0.5f)
        {
            switchLever.transform.localRotation = Quaternion.Slerp(
                switchLever.transform.localRotation,
                targetRotation,
                Time.deltaTime * switchSpeed
            );
            yield return null;
        }

        switchLever.transform.localRotation = targetRotation;
        isAnimating = false;
    }

    void UpdateIndicator(bool instant)
    {
        if (indicatorLight != null)
        {
            indicatorLight.color = lightsAreOn ? onColor : offColor;
            indicatorLight.enabled = true;
        }
    }

    // Public method to manually add lights
    public void AddLight(RoomLight light)
    {
        if (light != null && !controlledLights.Contains(light))
        {
            controlledLights.Add(light);
        }
    }

    // Public method to remove lights
    public void RemoveLight(RoomLight light)
    {
        if (controlledLights.Contains(light))
        {
            controlledLights.Remove(light);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw detection radius
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, lightDetectionRadius);

        // Draw lines to controlled lights
        Gizmos.color = lightsAreOn ? Color.green : Color.red;
        foreach (RoomLight light in controlledLights)
        {
            if (light != null)
            {
                Gizmos.DrawLine(transform.position, light.transform.position);
                Gizmos.DrawWireSphere(light.transform.position, 1f);
            }
        }

        // Draw zone label
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2, zoneName);
#endif
    }
}