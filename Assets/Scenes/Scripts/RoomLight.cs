using UnityEngine;

public class RoomLight : MonoBehaviour
{
    [Header("Light Components")]
    public Light lightSource;
    public Renderer lightBulbVisual; // Optional: visible bulb mesh

    [Header("Light Settings")]
    public bool startsOn = true;
    public bool canBeToggled = true;
    public bool canBeBroken = true;

    [Header("State")]
    public bool isOn = true;
    public bool isBroken = false;

    [Header("Audio")]
    public AudioClip toggleSound;
    public AudioClip breakSound;
    private AudioSource audioSource;

    [Header("Effects")]
    public ParticleSystem sparksEffect;
    public float sparksDuration = 1f;

    private float originalIntensity;
    private Color originalColor;
    private Material bulbMaterial;

    void Start()
    {
        SetupAudioSource();
        StoreOriginalSettings();
        InitializeState();

        if (sparksEffect != null)
        {
            sparksEffect.Stop();
        }
    }

    void SetupAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.maxDistance = 20f;
        }
    }

    void StoreOriginalSettings()
    {
        if (lightSource != null)
        {
            originalIntensity = lightSource.intensity;
            originalColor = lightSource.color;
        }

        if (lightBulbVisual != null)
        {
            bulbMaterial = lightBulbVisual.material;
        }
    }

    void InitializeState()
    {
        isOn = startsOn;
        isBroken = false;
        UpdateLightState();
    }

    public void TurnOn()
    {
        if (isBroken || !canBeToggled) return;

        isOn = true;
        UpdateLightState();

        if (toggleSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(toggleSound, 0.2f);
        }
    }

    public void TurnOff()
    {
        if (isBroken) return;

        isOn = false;
        UpdateLightState();

        if (toggleSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(toggleSound, 0.2f);
        }
    }

    public void Toggle()
    {
        if (isOn)
            TurnOff();
        else
            TurnOn();
    }

    public void BreakLight()
    {
        if (isBroken || !canBeBroken) return;

        isBroken = true;
        isOn = false;
        UpdateLightState();

        // Play break sound
        if (breakSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(breakSound, 0.6f);
        }

        // Trigger sparks
        if (sparksEffect != null)
        {
            sparksEffect.Play();
            Invoke(nameof(StopSparks), sparksDuration);
        }

        Debug.Log($"{gameObject.name} light broken by monster!");
    }

    void StopSparks()
    {
        if (sparksEffect != null)
        {
            sparksEffect.Stop();
        }
    }

    public void RepairLight()
    {
        if (!isBroken) return;

        isBroken = false;
        isOn = true;
        UpdateLightState();

        Debug.Log($"{gameObject.name} light repaired");
    }

    void UpdateLightState()
    {
        if (lightSource != null)
        {
            if (isBroken)
            {
                lightSource.enabled = false;
            }
            else
            {
                lightSource.enabled = isOn;

                if (isOn)
                {
                    lightSource.intensity = originalIntensity;
                    lightSource.color = originalColor;
                }
            }
        }

        UpdateBulbVisual();
    }

    void UpdateBulbVisual()
    {
        if (bulbMaterial == null) return;

        if (isBroken)
        {
            // Broken - no emission
            if (bulbMaterial.HasProperty("_EmissionColor"))
            {
                bulbMaterial.SetColor("_EmissionColor", Color.black);
            }
            bulbMaterial.color = new Color(0.3f, 0.3f, 0.3f); // Dark gray
        }
        else if (isOn)
        {
            // On - bright emission
            if (bulbMaterial.HasProperty("_EmissionColor"))
            {
                bulbMaterial.SetColor("_EmissionColor", originalColor * 2f);
                bulbMaterial.EnableKeyword("_EMISSION");
            }
            bulbMaterial.color = originalColor;
        }
        else
        {
            // Off - no emission
            if (bulbMaterial.HasProperty("_EmissionColor"))
            {
                bulbMaterial.SetColor("_EmissionColor", Color.black);
            }
            bulbMaterial.color = new Color(0.5f, 0.5f, 0.5f); // Gray
        }
    }

    void OnDrawGizmosSelected()
    {
        Light light = lightSource != null ? lightSource : GetComponent<Light>();
        if (light != null)
        {
            Gizmos.color = isOn ? Color.yellow : Color.gray;
            Gizmos.DrawWireSphere(transform.position, light.range);
        }
    }
}