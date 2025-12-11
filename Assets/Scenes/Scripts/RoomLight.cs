using UnityEngine;

public class RoomLight : MonoBehaviour
{
    [Header("Light Components")]
    public Light lightSource;
    public Renderer lightBulbVisual;

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

        if (lightSource == null)
        {
 
            Transform namedChild = transform.Find("PointLight");
            if (namedChild == null) namedChild = transform.Find("pointlight");

            if (namedChild != null)
            {
                lightSource = namedChild.GetComponent<Light>();
            }

   
            if (lightSource == null)
            {
                lightSource = GetComponentInChildren<Light>();
            }
        }

        if (lightSource == null)
        {
            Debug.LogError($"[RoomLight] CRITICAL: No Light component found on {gameObject.name} or its children!");
        }
        else
        {
 
            if (lightSource.lightmapBakeType == LightmapBakeType.Baked)
            {
                Debug.LogWarning($"[RoomLight] WARNING: Light '{lightSource.gameObject.name}' is set to BAKED! It will not turn off in game. Change it to Realtime or Mixed.");
            }
        }

        SetupAudioSource();
        StoreOriginalSettings();
        InitializeState();

        if (sparksEffect != null) sparksEffect.Stop();
    }

    void SetupAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }
    }

    void StoreOriginalSettings()
    {
        if (lightSource != null)
        {
            originalIntensity = lightSource.intensity;
            originalColor = lightSource.color;
        }
        if (lightBulbVisual != null) bulbMaterial = lightBulbVisual.material;
    }

    void InitializeState()
    {
        isOn = startsOn;
        isBroken = false;
        UpdateLightState();
    }

    public void TurnOn()
    {
        // Debugging why it might fail
        if (isBroken)
        {
            Debug.Log($"[RoomLight] Cannot turn on {gameObject.name}: It is BROKEN.");
            return;
        }
        if (!canBeToggled)
        {
            Debug.Log($"[RoomLight] Cannot turn on {gameObject.name}: 'Can Be Toggled' is FALSE.");
            return;
        }

        isOn = true;
        UpdateLightState();
        PlaySound(toggleSound, 0.2f);
    }

    public void TurnOff()
    {
        if (isBroken) return;

        isOn = false;
        UpdateLightState();
        PlaySound(toggleSound, 0.2f);
    }

    public void Toggle()
    {
        if (isOn) TurnOff();
        else TurnOn();
    }

    public void BreakLight()
    {
        if (isBroken || !canBeBroken) return;

        isBroken = true;
        isOn = false;
        UpdateLightState();
        PlaySound(breakSound, 0.6f);

        if (sparksEffect != null)
        {
            sparksEffect.Play();
            Invoke(nameof(StopSparks), sparksDuration);
        }
        Debug.Log($"{gameObject.name} light broken!");
    }

    void StopSparks()
    {
        if (sparksEffect != null) sparksEffect.Stop();
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
      
            lightSource.enabled = !isBroken && isOn;

            // Restore intensity if on
            if (lightSource.enabled)
            {
                lightSource.intensity = originalIntensity;
                lightSource.color = originalColor;
            }
        }
        UpdateBulbVisual();
    }

    void UpdateBulbVisual()
    {
        if (bulbMaterial == null) return;

        if (isBroken)
        {
            SetEmission(Color.black, new Color(0.3f, 0.3f, 0.3f));
        }
        else if (isOn)
        {
            SetEmission(originalColor * 2f, originalColor);
        }
        else
        {
            SetEmission(Color.black, new Color(0.5f, 0.5f, 0.5f));
        }
    }

    void SetEmission(Color emitColor, Color albedoColor)
    {
        if (bulbMaterial.HasProperty("_EmissionColor"))
        {
            bulbMaterial.SetColor("_EmissionColor", emitColor);
            if (emitColor == Color.black) bulbMaterial.DisableKeyword("_EMISSION");
            else bulbMaterial.EnableKeyword("_EMISSION");
        }
        bulbMaterial.color = albedoColor;
    }

    void PlaySound(AudioClip clip, float vol)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, vol);
    }

    void OnDrawGizmosSelected()
    {
        Light light = lightSource != null ? lightSource : GetComponentInChildren<Light>();
        if (light != null)
        {
            Gizmos.color = isOn ? Color.yellow : Color.gray;
            Gizmos.DrawWireSphere(light.transform.position, light.range);
        }
    }
}