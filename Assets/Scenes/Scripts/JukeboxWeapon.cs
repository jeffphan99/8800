using UnityEngine;
using StarterAssets;

public class JukeboxWeapon : WeaponBase
{
    [Header("Jukebox Settings")]
    public float maxBattery = 10f;
    public float aggroRadius = 40f;
    public float minActivationTime = 1f;

    [Header("Audio")]
    public AudioSource musicSource;
    public AudioClip musicClip;

    [Header("Empty/Toggle Audio")]
    public AudioSource sfxSource;
    public AudioClip toggleOnSound;
    public AudioClip toggleOffSound;
    public AudioClip emptySound;

    private float currentBattery;
    private bool isPlaying = false;
    private float lastToggleTime;

    // Static: all active jukeboxes — MonsterAI reads this to find taunt targets
    private static readonly System.Collections.Generic.List<JukeboxWeapon> activeJukeboxes
        = new System.Collections.Generic.List<JukeboxWeapon>();

    public static System.Collections.Generic.IReadOnlyList<JukeboxWeapon> ActiveJukeboxes => activeJukeboxes;

    void Start()
    {
        currentBattery = maxBattery;
        lastToggleTime = -minActivationTime; // allow immediate first use

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }
        musicSource.clip = musicClip;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 1f; // 3D sound
        musicSource.maxDistance = aggroRadius;
        musicSource.rolloffMode = AudioRolloffMode.Linear;

        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null || sfxSource == musicSource)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }
        }
    }

    void Update()
    {
        if (_input == null) return;
        if (!gameObject.activeInHierarchy) return;

        // Don't toggle during minigame
        Terminal activeTerminal = FindObjectOfType<Terminal>();
        if (activeTerminal != null && activeTerminal.minigameActive)
            return;

        // Toggle on click (with minimum activation time)
        if (_input.fire)
        {
            _input.fire = false;
            TryToggle();
        }

        // Drain battery while playing
        if (isPlaying)
        {
            currentBattery -= Time.deltaTime;

            if (currentBattery <= 0f)
            {
                currentBattery = 0f;
                TurnOff();
            }
        }

        UpdateStatusUI();
    }

    void TryToggle()
    {
        // Enforce minimum activation time to prevent spam
        if (Time.time - lastToggleTime < minActivationTime) return;

        if (isPlaying)
        {
            TurnOff();
        }
        else
        {
            if (currentBattery <= 0f)
            {
                if (sfxSource != null && emptySound != null)
                    sfxSource.PlayOneShot(emptySound);
                return;
            }
            TurnOn();
        }
    }

    void TurnOn()
    {
        isPlaying = true;
        lastToggleTime = Time.time;

        if (musicSource != null && musicClip != null)
            musicSource.Play();

        if (sfxSource != null && toggleOnSound != null)
            sfxSource.PlayOneShot(toggleOnSound);

        activeJukeboxes.Add(this);
        Debug.Log("[Jukebox] ON");
    }

    void TurnOff()
    {
        if (!isPlaying) return;

        isPlaying = false;
        lastToggleTime = Time.time;

        if (musicSource != null)
            musicSource.Stop();

        if (sfxSource != null && toggleOffSound != null)
            sfxSource.PlayOneShot(toggleOffSound);

        activeJukeboxes.Remove(this);
        Debug.Log("[Jukebox] OFF");
    }

    public override void UpdateStatusUI()
    {
        if (statusText != null)
        {
            float pct = currentBattery / maxBattery;
            string state = isPlaying ? "ON" : "OFF";
            statusText.text = $"Jukebox [{state}]: {currentBattery:F1}s";

            if (pct > 0.6f)
                statusText.color = Color.green;
            else if (pct > 0.3f)
                statusText.color = new Color(1f, 0.65f, 0f); // orange
            else
                statusText.color = Color.red;
        }
    }

    public override void PrimaryAction() { }
    public override void SecondaryAction() { }

    public override void OnEquip()
    {
        base.OnEquip();
        Debug.Log("Jukebox equipped");
    }

    public override void OnUnequip()
    {
        base.OnUnequip();
        TurnOff();
    }

    void OnDisable()
    {
        if (isPlaying) TurnOff();
    }

    void OnDestroy()
    {
        activeJukeboxes.Remove(this);
    }

    public override bool IsBusy()
    {
        return false;
    }

    public void Replenish()
    {
        currentBattery = maxBattery;
        TurnOff();
        Debug.Log("[Jukebox] Battery replenished");
    }

    /// <summary>Returns the world position of the player holding this jukebox.</summary>
    public Vector3 GetPlayerPosition()
    {
        return transform.root.position;
    }

    public float GetAggroRadius() => aggroRadius;
    public bool IsActive() => isPlaying;
    public float GetBattery() => currentBattery;
    public float GetMaxBattery() => maxBattery;
}
