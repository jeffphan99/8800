using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

public class FlashlightWeapon : WeaponBase
{
    [Header("Flashlight Settings")]
    public Light flashlight;
    public float maxBattery = 60f;
    public float batteryDrainRate = 1f; // Battery per second
    public float rechargeRate = 0.5f; // Recharge per second when off
    public float lightRange = 20f;
    public float spotAngle = 45f;

    [Header("Detection Settings")]
    public float detectionRange = 20f;
    public LayerMask monsterLayer;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip toggleOnSound;
    public AudioClip toggleOffSound;
    public AudioClip batteryLowSound;

    [Header("Visual Effects")]
    public GameObject flashlightModel;
    public Light batteryIndicator; // Small light showing battery status
    public Color fullBatteryColor = Color.green;
    public Color lowBatteryColor = Color.red;

    private float currentBattery;
    private bool isOn = false;
    private bool batteryEmpty = false;
    private bool hasWarnedLowBattery = false;

    void Start()
    {
        currentBattery = maxBattery;

        if (flashlight != null)
        {
            flashlight.enabled = false;
            flashlight.type = LightType.Spot;
            flashlight.range = lightRange;
            flashlight.spotAngle = spotAngle;
            flashlight.intensity = 2f;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        UpdateBatteryIndicator();
    }

    void Update()
    {
        if (_input == null) return;

        // Don't use if minigame active
        Terminal activeTerminal = FindObjectOfType<Terminal>();
        if (activeTerminal != null && activeTerminal.minigameActive)
            return;

        // Toggle with primary action (left click)
        if (_input.fire)
        {
            PrimaryAction();
            _input.fire = false;
        }

        // Handle battery drain/recharge
        if (isOn && !batteryEmpty)
        {
            DrainBattery();
            CheckForMonsters();
        }
        else if (!isOn && currentBattery < maxBattery)
        {
            RechargeBattery();
        }

        UpdateStatusUI();
        UpdateBatteryIndicator();
    }

    public override void PrimaryAction()
    {
        // Toggle flashlight on/off
        if (batteryEmpty)
        {
            Debug.Log("Battery empty! Wait for recharge...");
            if (audioSource != null && batteryLowSound != null)
            {
                audioSource.PlayOneShot(batteryLowSound, 0.5f);
            }
            return;
        }

        isOn = !isOn;

        if (flashlight != null)
        {
            flashlight.enabled = isOn;
        }

        // Play sound
        if (audioSource != null)
        {
            if (isOn && toggleOnSound != null)
            {
                audioSource.PlayOneShot(toggleOnSound, 0.4f);
            }
            else if (!isOn && toggleOffSound != null)
            {
                audioSource.PlayOneShot(toggleOffSound, 0.4f);
            }
        }

        Debug.Log($"Flashlight {(isOn ? "ON" : "OFF")} - Battery: {currentBattery:F1}/{maxBattery}");
    }

    public override void SecondaryAction()
    {
 
    }

    void DrainBattery()
    {
        currentBattery -= batteryDrainRate * Time.deltaTime;
        currentBattery = Mathf.Max(0, currentBattery);

        // Check for low battery warning
        if (currentBattery < maxBattery * 0.2f && !hasWarnedLowBattery)
        {
            hasWarnedLowBattery = true;
            if (audioSource != null && batteryLowSound != null)
            {
                audioSource.PlayOneShot(batteryLowSound, 0.3f);
            }
            Debug.Log("Flashlight battery low!");
        }

        // Battery empty
        if (currentBattery <= 0)
        {
            batteryEmpty = true;
            isOn = false;
            if (flashlight != null)
            {
                flashlight.enabled = false;
            }
            Debug.Log("Flashlight battery empty!");
        }
    }

    void RechargeBattery()
    {
        currentBattery += rechargeRate * Time.deltaTime;
        currentBattery = Mathf.Min(maxBattery, currentBattery);

        // Battery recharged enough to use
        if (batteryEmpty && currentBattery > maxBattery * 0.1f)
        {
            batteryEmpty = false;
            hasWarnedLowBattery = false;
            Debug.Log("Flashlight recharged!");
        }
    }

    void CheckForMonsters()
    {
        if (playerCamera == null) return;

        // Raycast from camera in flashlight direction
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, detectionRange, monsterLayer))
        {
            // Check if we hit a monster
            MonsterAI monster = hit.collider.GetComponentInParent<MonsterAI>();
            if (monster != null)
            {
                // Notify monster it's being shone on
                monster.OnFlashlightShone();
                Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.yellow);
            }
        }
    }

    void UpdateBatteryIndicator()
    {
        if (batteryIndicator == null) return;

        float batteryPercent = currentBattery / maxBattery;

        // Color gradient from green to red
        batteryIndicator.color = Color.Lerp(lowBatteryColor, fullBatteryColor, batteryPercent);
        batteryIndicator.intensity = Mathf.Lerp(0.2f, 1f, batteryPercent);
    }

    public override void UpdateStatusUI()
    {
        if (statusText != null)
        {
            float batteryPercent = (currentBattery / maxBattery) * 100f;
            statusText.text = $"Battery: {batteryPercent:F0}%";

            // Color code the status
            if (batteryPercent < 20f)
            {
                statusText.color = Color.red;
            }
            else if (batteryPercent < 50f)
            {
                statusText.color = Color.yellow;
            }
            else
            {
                statusText.color = Color.white;
            }

            // Show ON/OFF state
            if (isOn)
            {
                statusText.text += " [ON]";
            }
        }

        if (actionText != null)
        {
            if (batteryEmpty)
            {
                actionText.enabled = true;
                actionText.text = "BATTERY EMPTY - Recharging...";
                actionText.color = Color.red;
            }
            else if (isOn && currentBattery < maxBattery * 0.2f)
            {
                actionText.enabled = true;
                actionText.text = "LOW BATTERY!";
                actionText.color = Color.yellow;
            }
            else
            {
                actionText.enabled = false;
            }
        }
    }

    public override void OnEquip()
    {
        base.OnEquip();

        if (flashlightModel != null)
        {
            flashlightModel.SetActive(true);
        }

        Debug.Log("Flashlight equipped");
    }

    public override void OnUnequip()
    {
        base.OnUnequip();

        // Turn off when unequipped
        if (isOn)
        {
            isOn = false;
            if (flashlight != null)
            {
                flashlight.enabled = false;
            }
        }

        if (flashlightModel != null)
        {
            flashlightModel.SetActive(false);
        }
    }

    public override bool IsBusy()
    {
        return false; // Flashlight is never "busy"
    }

    // Public getters for other systems
    public bool IsOn() => isOn;
    public float GetBatteryPercent() => (currentBattery / maxBattery) * 100f;
}