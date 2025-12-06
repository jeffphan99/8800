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

    [Header("Visual Feedback")]
    public GameObject visualSphere; 
    public Light terminalLight; 
    public Material brokenMaterial; 
    public Material workingMaterial; 
    private Renderer sphereRenderer;

    void Start()
    {
        Debug.Log("=== TERMINAL START ===");

        playerInput = FindObjectOfType<StarterAssetsInputs>();
        playerController = FindObjectOfType<FirstPersonController>();

 
        if (visualSphere != null)
        {
            sphereRenderer = visualSphere.GetComponent<Renderer>();
        }
        else
        {
            Debug.LogWarning("Visual Sphere not assigned! Trying to find child named 'Sphere'");
            Transform sphereTransform = transform.Find("Sphere");
            if (sphereTransform != null)
            {
                visualSphere = sphereTransform.gameObject;
                sphereRenderer = visualSphere.GetComponent<Renderer>();
            }
        }

    
        if (sequenceText == null)
        {
            sequenceText = GameObject.Find("SequenceText")?.GetComponent<Text>();
        }

        if (feedbackText == null)
        {
            feedbackText = GameObject.Find("FeedbackText")?.GetComponent<Text>();
        }

        if (minigameUI == null)
        {
            minigameUI = GameObject.Find("MinigamePanel");
        }

        // HIDE UI at start
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
            if (sequenceText != null)
            {
                sequenceText.text = "";
            }

 
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
                Debug.Log($"Key pressed: {key}, Expected: {targetSequence[currentIndex]}");
                CheckKey(key);
                break;
            }
        }

        // ESC to cancel
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("ESC pressed - canceling repair");
            CancelMinigame();
        }
    }

    void CheckKey(KeyCode pressedKey)
    {
        if (pressedKey == targetSequence[currentIndex])
        {

            Debug.Log($"CORRECT! Progress: {currentIndex + 1}/{targetSequence.Count}");
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
        if (sequenceText == null)
        {
            Debug.LogError("sequenceText is NULL - cannot update display!");
            return;
        }

        string display = "";
        for (int i = 0; i < targetSequence.Count; i++)
        {
            if (i < currentIndex)
            {

                display += $"<color=green>✓{KeyToString(targetSequence[i])}</color> ";
            }
            else if (i == currentIndex)
            {

                display += $"<color=yellow><b>[{KeyToString(targetSequence[i])}]</b></color> ";
            }
            else
            {
  
                display += $"{KeyToString(targetSequence[i])} ";
            }
        }

        sequenceText.text = display.Trim();
    }

    string KeyToString(KeyCode key)
    {
        return key.ToString();
    }

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

        UpdateVisuals();

        Invoke(nameof(CloseMinigame), 2f);
    }

    void CancelMinigame()
    {
        CloseMinigame();
    }

    void CloseMinigame()
    {
        Debug.Log("=== CLOSING MINIGAME ===");
        minigameActive = false;


        HideUI();

        // Re-enable player input
        if (playerInput != null)
        {
            playerInput.cursorLocked = true;
            playerInput.cursorInputForLook = true;
        }

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


    public void BreakTerminal()
    {
        isBroken = true;
        Debug.Log($"Terminal {gameObject.name} has broken!");

        UpdateVisuals();
    }

    public void ResetTerminal()
    {
        isBroken = false;
        minigameActive = false;
        currentIndex = 0;
        targetSequence.Clear();

        HideUI();
        UpdateVisuals();
    }

    void UpdateVisuals()
    {

        if (terminalLight != null)
        {
            terminalLight.color = isBroken ? Color.red : Color.green;
        }


        if (sphereRenderer != null)
        {
            if (isBroken && brokenMaterial != null)
            {
                sphereRenderer.material = brokenMaterial;
            }
            else if (!isBroken && workingMaterial != null)
            {
                sphereRenderer.material = workingMaterial;
            }
        }
    }

    void ShowUI()
    {
        if (minigameUI != null)
        {
            minigameUI.SetActive(true);
        }

        if (sequenceText != null)
        {
            sequenceText.enabled = true;
        }

        if (feedbackText != null)
        {
            feedbackText.enabled = true;
        }
    }

    void HideUI()
    {
        if (minigameUI != null)
        {
            minigameUI.SetActive(false);
        }

        if (sequenceText != null)
        {
            sequenceText.enabled = false;
        }

        if (feedbackText != null)
        {
            feedbackText.enabled = false;
        }
    }
}