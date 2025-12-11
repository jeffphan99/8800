using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

public class Terminal : MonoBehaviour
{
    [Header("Minigame Settings")]
    public int sequenceLength = 8;
    public GameObject minigameUI;
    public Text sequenceText;
    public Text feedbackText;

    private List<KeyCode> targetSequence = new List<KeyCode>();
    private int currentIndex = 0;
    public bool minigameActive = false;
    public bool isBroken = false;

    private KeyCode[] possibleKeys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };

    private StarterAssetsInputs playerInput;
    private FirstPersonController playerController;
    private WeaponSwitcher weaponSwitcher; // Reference to the weapon switcher

    [Header("Visual Feedback")]
    public GameObject visualSphere;
    public Light terminalLight;
    public Material brokenMaterial;
    public Material workingMaterial;
    private Renderer sphereRenderer;

    [Header("Break Effects")]
    public ParticleSystem breakParticleEffect; // Sparks/smoke when terminal breaks
    public AudioClip breakSound; // Sound when breaking
    public AudioSource audioSource;

    void Start()
    {
        Debug.Log("=== TERMINAL START ===");

        playerInput = FindObjectOfType<StarterAssetsInputs>();
        playerController = FindObjectOfType<FirstPersonController>();

        // Find the weapon switcher on the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            weaponSwitcher = player.GetComponentInChildren<WeaponSwitcher>();
        }

        if (visualSphere != null)
        {
            sphereRenderer = visualSphere.GetComponent<Renderer>();
        }
        else
        {
            Transform sphereTransform = transform.Find("Sphere");
            if (sphereTransform != null)
            {
                visualSphere = sphereTransform.gameObject;
                sphereRenderer = visualSphere.GetComponent<Renderer>();
            }
        }

        if (sequenceText == null) sequenceText = GameObject.Find("SequenceText")?.GetComponent<Text>();
        if (feedbackText == null) feedbackText = GameObject.Find("FeedbackText")?.GetComponent<Text>();
        if (minigameUI == null) minigameUI = GameObject.Find("MinigamePanel");

        // Set up audio source if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        HideUI();
        UpdateVisuals();
    }

    public void StartMinigame()
    {
        if (!isBroken)
        {
            Debug.Log("Terminal is working fine - no repair needed");
            ShowUI();
            if (feedbackText != null)
            {
                feedbackText.text = "Terminal operational - no repair needed";
                feedbackText.color = Color.green;
            }
            if (sequenceText != null) sequenceText.text = "";

            Invoke(nameof(HideUI), 2f);
            return;
        }

        if (!IsRepairToolEquipped())
        {
            Debug.Log("You need the repair tool equipped!");

            // Show a warning message using your existing UI
            ShowUI();
            if (feedbackText != null)
            {
                feedbackText.text = "Equip Repair Tool to fix! (Press 3)";
                feedbackText.color = Color.red;
            }
            if (sequenceText != null) sequenceText.text = "";

            // Hide the warning after 2 seconds
            Invoke(nameof(HideUI), 2f);
            return;
        }

        Debug.Log("=== START REPAIR MINIGAME ===");

        ShowUI();
        minigameActive = true;
        currentIndex = 0;

        // Generate random sequence
        targetSequence.Clear();
        for (int i = 0; i < sequenceLength; i++)
        {
            targetSequence.Add(possibleKeys[Random.Range(0, possibleKeys.Length)]);
        }

        Debug.Log($"Generated Sequence: {string.Join(", ", targetSequence)}");

        UpdateSequenceDisplay();

        if (feedbackText != null)
        {
            feedbackText.text = "REPAIRING: Type the sequence!";
            feedbackText.color = Color.yellow;
        }

        // Lock Player Movement
        if (playerInput != null)
        {
            playerInput.cursorLocked = false;
            playerInput.cursorInputForLook = false;
            playerInput.move = Vector2.zero;
            playerInput.look = Vector2.zero;
        }

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (!minigameActive) return;

        foreach (KeyCode key in possibleKeys)
        {
            if (Input.GetKeyDown(key))
            {
                CheckKey(key);
                break;
            }
        }

        // ESC to cancel
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelMinigame();
        }
    }

    void CheckKey(KeyCode pressedKey)
    {
        if (pressedKey == targetSequence[currentIndex])
        {
            currentIndex++;

            if (currentIndex >= targetSequence.Count)
            {
                CompleteMinigame();
            }
            else
            {
                UpdateSequenceDisplay();
                if (feedbackText != null)
                {
                    feedbackText.text = $"Correct! ({currentIndex}/{targetSequence.Count})";
                    feedbackText.color = Color.green;
                }
            }
        }
        else
        {
            Debug.Log("WRONG KEY! Resetting sequence...");
            currentIndex = 0;
            if (feedbackText != null)
            {
                feedbackText.text = "Wrong! Sequence reset.";
                feedbackText.color = Color.red;
            }
            UpdateSequenceDisplay();
        }
    }

    void UpdateSequenceDisplay()
    {
        if (sequenceText == null) return;

        string display = "";
        for (int i = 0; i < targetSequence.Count; i++)
        {
            if (i < currentIndex)
                display += $"<color=green>✓{KeyToString(targetSequence[i])}</color> ";
            else if (i == currentIndex)
                display += $"<color=yellow><b>[{KeyToString(targetSequence[i])}]</b></color> ";
            else
                display += $"{KeyToString(targetSequence[i])} ";
        }

        sequenceText.text = display.Trim();
    }

    string KeyToString(KeyCode key) { return key.ToString(); }

    void CompleteMinigame()
    {
        Debug.Log("=== TERMINAL REPAIRED! ===");
        isBroken = false;

        if (feedbackText != null)
        {
            feedbackText.text = "REPAIR COMPLETE!";
            feedbackText.color = Color.green;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTerminalRepaired(this);
        }

        if (MinimapManager.Instance != null) MinimapManager.Instance.UpdateTerminalIcon(this);

        UpdateVisuals();
        Invoke(nameof(CloseMinigame), 2f);

        // Get the particle system on the assigned GameObject
        ParticleSystem mainPS = breakParticleEffect.GetComponent<ParticleSystem>();
        if (mainPS != null)
        {
            Debug.Log($"[Terminal] Stopping main particle: {mainPS.gameObject.name}");
            mainPS.Stop();
        }

    }

    void CancelMinigame() { CloseMinigame(); }

    void CloseMinigame()
    {
        minigameActive = false;
        HideUI();

        if (playerInput != null)
        {
            playerInput.cursorLocked = true;
            playerInput.cursorInputForLook = true;
        }

        if (playerController != null) playerController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void BreakTerminal()
    {
        Debug.Log($"[Terminal] Breaking terminal: {gameObject.name}");

        isBroken = true;

        // Play break particle effect - SYNTY COMPATIBLE
        if (breakParticleEffect != null)
        {
            Debug.Log("[Terminal] Playing break particles");

            // Get the particle system on the assigned GameObject
            ParticleSystem mainPS = breakParticleEffect.GetComponent<ParticleSystem>();
            if (mainPS != null)
            {
                Debug.Log($"[Terminal] Playing main particle: {mainPS.gameObject.name}");
                mainPS.Play();
            }

        }
        else
        {
            Debug.LogWarning("[Terminal] No break particle effect assigned!");
        }

        // Play break sound
        if (audioSource != null && breakSound != null)
        {
            Debug.Log("[Terminal] Playing break sound");
            audioSource.PlayOneShot(breakSound);
        }
        else if (breakSound == null)
        {
            Debug.LogWarning("[Terminal] No break sound assigned!");
        }

        UpdateVisuals();
        if (MinimapManager.Instance != null) MinimapManager.Instance.UpdateTerminalIcon(this);
    }

    public void ResetTerminal()
    {
        isBroken = false;
        minigameActive = false;
        currentIndex = 0;
        targetSequence.Clear();
        HideUI();
        UpdateVisuals();
        if (MinimapManager.Instance != null) MinimapManager.Instance.UpdateTerminalIcon(this);
    }

    void UpdateVisuals()
    {
        if (terminalLight != null) terminalLight.color = isBroken ? Color.red : Color.green;

        if (sphereRenderer != null)
        {
            if (isBroken && brokenMaterial != null) sphereRenderer.material = brokenMaterial;
            else if (!isBroken && workingMaterial != null) sphereRenderer.material = workingMaterial;
        }
    }

    void ShowUI()
    {
        if (minigameUI != null) minigameUI.SetActive(true);
        if (sequenceText != null) sequenceText.enabled = true;
        if (feedbackText != null) feedbackText.enabled = true;
    }

    void HideUI()
    {
        if (minigameUI != null) minigameUI.SetActive(false);
        if (sequenceText != null) sequenceText.enabled = false;
        if (feedbackText != null) feedbackText.enabled = false;
    }


    bool IsRepairToolEquipped()
    {
        if (weaponSwitcher == null) return false;

        WeaponBase currentWeapon = weaponSwitcher.GetCurrentWeapon();

        if (currentWeapon == null) return false;

        if (currentWeapon is RepairToolWeapon)
        {
            return true;
        }

        return false;
    }
}