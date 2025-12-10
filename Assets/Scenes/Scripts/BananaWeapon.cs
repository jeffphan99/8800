using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

public class BananaWeapon : WeaponBase
{
    [Header("Banana Settings")]
    public int maxBananas = 2;
    public float consumeTime = 1f;
    
    [Header("Peel Settings")]
    public GameObject bananaPeelPrefab;
    public Vector3 peelDropOffset = new Vector3(0, -0.5f, 0);
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip eatSound;
    public AudioClip dropSound;
    public AudioClip emptySound;
    
    [Header("Visual")]
    public GameObject bananaModel;
    
    private int currentBananas;
    private bool isEating = false;
    private float eatTimer = 0f;
    private PlayerHealth playerHealth;

    void Start()
    {
        currentBananas = maxBananas;
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
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
    }

    void Update()
    {
        // Handle eating timer
        if (isEating)
        {
            eatTimer += Time.deltaTime;
            
            if (eatTimer >= consumeTime)
            {
                FinishEating();
            }
            
            return; // Don't accept input while eating
        }

        // Don't use if minigame active
        Terminal activeTerminal = FindObjectOfType<Terminal>();
        if (activeTerminal != null && activeTerminal.minigameActive)
            return;

        // Eat banana with left click
        if (Input.GetMouseButtonDown(0))
        {
            PrimaryAction();
        }

        UpdateStatusUI();
    }

    public override void PrimaryAction()
    {
        if (isEating) return;
        
        if (currentBananas <= 0)
        {
            if (emptySound != null && audioSource != null)
            {
                audioSource.PlayOneShot(emptySound, 0.5f);
            }
            return;
        }

        // Start eating
        StartEating();
    }

    void StartEating()
    {
        isEating = true;
        eatTimer = 0f;
        currentBananas--;
        
        // Show eating message
        if (actionText != null)
        {
            actionText.enabled = true;
            actionText.text = "Eating banana...";
            actionText.color = Color.yellow;
        }
        
        // Play eating sound
        if (eatSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(eatSound, 0.6f);
        }
        
        // Hide banana model
/*        if (bananaModel != null)
        {
            bananaModel.SetActive(false);
        }*/
        
        Debug.Log($"Started eating banana. {currentBananas} remaining");
    }

    void FinishEating()
    {
        // Heal player
        if (playerHealth != null)
        {
            playerHealth.ResetHealth();
            Debug.Log("Player healed to full health!");
        }
        
        // Drop peel
        DropPeel();
        
        // Show banana model again
        if (bananaModel != null)
        {
            bananaModel.SetActive(true);
        }
        
        // Hide action text
        if (actionText != null)
        {
            actionText.enabled = false;
        }
        
        isEating = false;
        eatTimer = 0f;
        
        Debug.Log("Finished eating banana");
        UpdateStatusUI();
    }

    void DropPeel()
    {
        Debug.Log("=== DropPeel() CALLED ===");

        if (bananaPeelPrefab == null)
        {
            Debug.LogError("Banana Peel Prefab is NULL! Assign it in Inspector!");
            return;
        }

        Debug.Log($"Prefab assigned: {bananaPeelPrefab.name}");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("Player not found!");
            return;
        }

        Debug.Log($"Player found at: {player.transform.position}");

        Vector3 dropPosition = player.transform.position + peelDropOffset;
        Debug.Log($"Drop position: {dropPosition}");
        Debug.Log($"Peel offset: {peelDropOffset}");

        GameObject peel = Instantiate(bananaPeelPrefab, dropPosition, bananaPeelPrefab.transform.rotation);

        if (peel != null)
        {
            Debug.Log($"SUCCESS: Peel created - {peel.name} at {peel.transform.position}");
        }
        else
        {
            Debug.LogError("FAILED: Instantiate returned null!");
        }

        if (dropSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(dropSound, 0.4f);
        }
    }

    public override void SecondaryAction()
    {
    }

    public override void UpdateStatusUI()
    {
        if (statusText != null)
        {
            statusText.text = $"Bananas: {currentBananas}/{maxBananas}";
            
            if (currentBananas == 0)
                statusText.color = Color.red;
            else if (currentBananas == 1)
                statusText.color = Color.yellow;
            else
                statusText.color = Color.white;
        }
    }

    public override void OnEquip()
    {
        base.OnEquip();
        
        if (bananaModel != null)
        {
            bananaModel.SetActive(true);
        }
        
        Debug.Log("Banana equipped");
    }

    public override void OnUnequip()
    {
        base.OnUnequip();
        
        // Cancel eating if switching weapons
        if (isEating)
        {
            isEating = false;
            eatTimer = 0f;
            
            if (actionText != null)
            {
                actionText.enabled = false;
            }
            
            if (bananaModel != null)
            {
                bananaModel.SetActive(true);
            }
        }
        
        if (bananaModel != null)
        {
            bananaModel.SetActive(false);
        }
    }

    public override bool IsBusy()
    {
        return isEating;
    }

    public void Replenish()
    {
        currentBananas = maxBananas;
        Debug.Log("Bananas replenished!");
    }

    public int GetCurrentBananas()
    {
        return currentBananas;
    }
}