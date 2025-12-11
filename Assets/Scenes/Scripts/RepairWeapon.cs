using UnityEngine;
using StarterAssets;


public class RepairToolWeapon : WeaponBase
{
    [Header("Animation")]
    [SerializeField] private Animator toolAnimator; 
    [SerializeField] private string repairAnimationTrigger = "Repair"; 
    [SerializeField] private string repairLoopBool = "IsRepairing"; 
    [SerializeField] private bool useLoopingAnimation = true; 

    [Header("Audio")]
    [SerializeField] private AudioClip repairStartSound; 
    [SerializeField] private AudioClip repairLoopSound; 
    [SerializeField] private AudioClip repairCompleteSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem repairParticles; 
    [SerializeField] private Light repairLight; 
    [SerializeField] private GameObject repairEffectPrefab;

  
    private bool isRepairing = false;
    private AudioSource loopAudioSource;
    private Terminal activeTerminal; 
    private GameObject activeRepairEffect;

    void Start()
    {
        // Find animator if not assigned
        if (toolAnimator == null)
        {
            toolAnimator = GetComponentInChildren<Animator>();
        }

        // Find audio source if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Setup looping audio source
        if (repairLoopSound != null)
        {
            loopAudioSource = gameObject.AddComponent<AudioSource>();
            loopAudioSource.clip = repairLoopSound;
            loopAudioSource.loop = true;
            loopAudioSource.playOnAwake = false;
            loopAudioSource.volume = 0.5f;
        }

        // Turn off repair light initially
        if (repairLight != null)
        {
            repairLight.enabled = false;
        }

        // Stop particles initially
        if (repairParticles != null)
        {
            repairParticles.Stop();
        }

        Debug.Log("RepairTool initialized");
    }

    void Update()
    {
        // Check if any terminal minigame is active
        bool minigameActive = CheckIfMinigameActive();

        if (minigameActive && !isRepairing)
        {
            // Minigame just started
            StartRepairAnimation();
        }
        else if (!minigameActive && isRepairing)
        {
            // Minigame just ended
            StopRepairAnimation();
        }
    }

    bool CheckIfMinigameActive()
    {
        // Find active terminal with minigame
        Terminal[] allTerminals = FindObjectsOfType<Terminal>();
        foreach (Terminal terminal in allTerminals)
        {
            if (terminal.minigameActive)
            {
                activeTerminal = terminal;
                return true;
            }
        }

        activeTerminal = null;
        return false;
    }

    void StartRepairAnimation()
    {
        isRepairing = true;
        Debug.Log("RepairTool: Starting repair animation");

        // Play animation
        if (toolAnimator != null)
        {
            if (useLoopingAnimation && !string.IsNullOrEmpty(repairLoopBool))
            {
    
                toolAnimator.SetBool(repairLoopBool, true);
            }

            if (!string.IsNullOrEmpty(repairAnimationTrigger))
            {

                toolAnimator.SetTrigger(repairAnimationTrigger);
            }
        }

        // Play start sound
        if (repairStartSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(repairStartSound);
        }

        // Start looping sound
        if (loopAudioSource != null)
        {
            loopAudioSource.Play();
        }

        // Start particle effects
        if (repairParticles != null)
        {
            repairParticles.Play();
        }

        if (repairLight != null)
        {
            repairLight.enabled = true;
        }

        // Spawn repair effect
        if (repairEffectPrefab != null)
        {

            Transform spawnPoint = transform.Find("EffectPoint");
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;

            activeRepairEffect = Instantiate(repairEffectPrefab, spawnPos, transform.rotation);
            activeRepairEffect.transform.SetParent(transform);
        }
    }

    void StopRepairAnimation()
    {
        if (!isRepairing) return;

        isRepairing = false;
        Debug.Log("RepairTool: Stopping repair animation");

        // Stop animation
        if (toolAnimator != null && !string.IsNullOrEmpty(repairLoopBool))
        {
            toolAnimator.SetBool(repairLoopBool, false);
        }

        // Play complete sound
        if (repairCompleteSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(repairCompleteSound);
        }

        // Stop looping sound
        if (loopAudioSource != null && loopAudioSource.isPlaying)
        {
            loopAudioSource.Stop();
        }

        // Stop particle effects
        if (repairParticles != null)
        {
            repairParticles.Stop();
        }

        // Turn off repair light
        if (repairLight != null)
        {
            repairLight.enabled = false;
        }

        // Destroy repair effect
        if (activeRepairEffect != null)
        {
            Destroy(activeRepairEffect);
            activeRepairEffect = null;
        }
    }

    public override void OnEquip()
    {
        base.OnEquip();
        gameObject.SetActive(true);
        Debug.Log("Repair Tool equipped");
    }

    public override void OnUnequip()
    {
        base.OnUnequip();
        StopRepairAnimation(); // Stop animation if switching weapons
        gameObject.SetActive(false);
        Debug.Log("Repair Tool unequipped");
    }

    void OnDisable()
    {
        StopRepairAnimation();
    }

    void OnDestroy()
    {
        if (activeRepairEffect != null)
        {
            Destroy(activeRepairEffect);
        }
    }

    public void OnRepairSuccess()
    {
        Debug.Log("RepairTool: Repair successful!");

        // Play success animation/sound if you want
        if (repairCompleteSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(repairCompleteSound);
        }
    }

    public void OnRepairFail()
    {
        Debug.Log("RepairTool: Repair failed!");

    }

    public override void PrimaryAction()
    {
     
    }

    public override void SecondaryAction()
    {
    }

    public override void UpdateStatusUI()
    {
      
    }
}