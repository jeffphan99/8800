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
    public LayerMask lightLayer; 

    [Header("Switch Visual")]
    public GameObject switchLever;
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

 
        if (autoFindLightsOnStart && controlledLights.Count == 0)
        {
            FindNearbyLights();
        }

 
        if (controlledLights.Count > 0 && controlledLights[0] != null)
        {
            lightsAreOn = controlledLights[0].isOn;
        }
        else
        {
            lightsAreOn = true;
        }

        UpdateIndicator(true);
        UpdateSwitchVisual(true);
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

        Collider[] colliders = Physics.OverlapSphere(transform.position, lightDetectionRadius);

        foreach (Collider col in colliders)
        {
            RoomLight light = col.GetComponent<RoomLight>();
            // Only add if not already in the list
            if (light != null && !controlledLights.Contains(light))
            {
                controlledLights.Add(light);
            }
        }

        if (controlledLights.Count == 0)
        {
            RoomLight[] allLights = FindObjectsOfType<RoomLight>();
            foreach (RoomLight light in allLights)
            {
                float distance = Vector3.Distance(transform.position, light.transform.position);
                if (distance <= lightDetectionRadius && !controlledLights.Contains(light))
                {
                    controlledLights.Add(light);
                }
            }
        }

        Debug.Log($"[{zoneName}] LightSwitch tracking {controlledLights.Count} lights");
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

    public void AddLight(RoomLight light)
    {
        if (light != null && !controlledLights.Contains(light))
        {
            controlledLights.Add(light);
        }
    }

    public void RemoveLight(RoomLight light)
    {
        if (controlledLights.Contains(light))
        {
            controlledLights.Remove(light);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, lightDetectionRadius);

        Gizmos.color = lightsAreOn ? Color.green : Color.red;
        foreach (RoomLight light in controlledLights)
        {
            if (light != null)
            {
                Gizmos.DrawLine(transform.position, light.transform.position);
                Gizmos.DrawWireSphere(light.transform.position, 1f);
            }
        }
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2, zoneName);
#endif
    }
}