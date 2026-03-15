using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

public class Terminal : NetworkBehaviour
{
    [Header("Minigame Settings")]
    public int sequenceLength = 8;
    public GameObject minigameUI;
    public Text sequenceText;
    public Text feedbackText;

    private List<KeyCode> targetSequence = new List<KeyCode>();
    private int currentIndex = 0;
    public bool minigameActive = false;

    // Networked state
    private NetworkVariable<bool> networkIsBroken = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<ulong> repairingPlayerId = new NetworkVariable<ulong>(
        ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> assignedRole = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public PlayerRole AssignedRole => (PlayerRole)assignedRole.Value;

    // Local shortcut
    public bool isBroken => networkIsBroken.Value;

    private KeyCode[] possibleKeys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };

    // Lazy-initialized per local player
    private StarterAssetsInputs playerInput;
    private FirstPersonController playerController;
    private WeaponSwitcher weaponSwitcher;
    private bool localRefsFound = false;

    [Header("Visual Feedback")]
    public GameObject visualSphere;
    public Light terminalLight;
    public Material brokenMaterial;
    public Material workingMaterial;
    private Renderer sphereRenderer;

    [Header("Break Effects")]
    public ParticleSystem breakParticleEffect;
    public AudioClip breakSound;
    public AudioSource audioSource;

    void Start()
    {
        Debug.Log("=== TERMINAL START ===");

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

    public override void OnNetworkSpawn()
    {
        networkIsBroken.OnValueChanged += OnBrokenStateChanged;
        // Sync visuals to current state on spawn
        UpdateVisuals();
    }

    public override void OnNetworkDespawn()
    {
        networkIsBroken.OnValueChanged -= OnBrokenStateChanged;
    }

    private void OnBrokenStateChanged(bool previousValue, bool newValue)
    {
        UpdateVisuals();

        if (newValue)
        {
            // Terminal just broke — play effects on all clients
            PlayBreakEffects();
        }
        else
        {
            // Terminal repaired — stop effects on all clients
            StopBreakEffects();
        }

        if (MinimapManager.Instance != null) MinimapManager.Instance.UpdateTerminalIcon(this);
    }

    private void FindLocalPlayerRefs()
    {
        if (localRefsFound) return;

        GameObject player = PlayerHealth.LocalPlayer;
        if (player == null) return;

        playerInput = player.GetComponent<StarterAssetsInputs>();
        playerController = player.GetComponent<FirstPersonController>();
        weaponSwitcher = player.GetComponentInChildren<WeaponSwitcher>();
        localRefsFound = true;
    }

    public void StartMinigame()
    {
        // Lazy-find local player references
        FindLocalPlayerRefs();

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
            ShowUI();
            if (feedbackText != null)
            {
                feedbackText.text = "Equip Repair Tool to fix! (Press 3)";
                feedbackText.color = Color.red;
            }
            if (sequenceText != null) sequenceText.text = "";
            Invoke(nameof(HideUI), 2f);
            return;
        }

        // Check if another player is already repairing
        if (repairingPlayerId.Value != ulong.MaxValue)
        {
            Debug.Log("Another player is already repairing this terminal!");
            ShowUI();
            if (feedbackText != null)
            {
                feedbackText.text = "Another player is repairing this terminal!";
                feedbackText.color = Color.yellow;
            }
            if (sequenceText != null) sequenceText.text = "";
            Invoke(nameof(HideUI), 2f);
            return;
        }

        // Request repair lock from server
        RequestRepairLockServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRepairLockServerRpc(ulong clientId)
    {
        if (repairingPlayerId.Value != ulong.MaxValue)
        {
            // Already locked by someone else
            RepairLockDeniedClientRpc(clientId);
            return;
        }

        if (!networkIsBroken.Value)
        {
            // Terminal was repaired between request and server processing
            RepairLockDeniedClientRpc(clientId);
            return;
        }

        // Grant the lock
        repairingPlayerId.Value = clientId;
        RepairLockGrantedClientRpc(clientId);
    }

    [ClientRpc]
    private void RepairLockGrantedClientRpc(ulong clientId)
    {
        // Only the requesting client starts the minigame
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        BeginMinigameLocal();
    }

    [ClientRpc]
    private void RepairLockDeniedClientRpc(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        ShowUI();
        if (feedbackText != null)
        {
            feedbackText.text = "Another player is repairing this terminal!";
            feedbackText.color = Color.yellow;
        }
        if (sequenceText != null) sequenceText.text = "";
        Invoke(nameof(HideUI), 2f);
    }

    private void BeginMinigameLocal()
    {
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

        // Show failure risk based on role match
        ShowFailureRisk();

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

    private void ShowFailureRisk()
    {
        Text failureRiskText = null;
        GameObject riskObj = GameObject.Find("FailureRiskText");
        if (riskObj != null)
            failureRiskText = riskObj.GetComponent<Text>();

        if (failureRiskText == null) return;

        // Get local player's role
        GameObject localPlayer = PlayerHealth.LocalPlayer;
        if (localPlayer == null) return;

        var roleCtrl = localPlayer.GetComponent<PlayerRoleController>();
        if (roleCtrl == null) return;

        PlayerRole playerRole = roleCtrl.Role;
        PlayerRole terminalRole = AssignedRole;

        if (playerRole == terminalRole || terminalRole == PlayerRole.None)
        {
            failureRiskText.text = "Role Match \u2014 No failure risk";
            failureRiskText.color = Color.green;
        }
        else
        {
            int currentRound = GameManager.Instance != null ? GameManager.Instance.CurrentRound : 1;
            int failurePercent = currentRound * 15;
            failureRiskText.text = $"Role Mismatch \u2014 {failurePercent}% failure risk";
            failureRiskText.color = new Color(1f, 0.5f, 0f); // orange
        }
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
                display += $"<color=green>\u2713{KeyToString(targetSequence[i])}</color> ";
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

        if (feedbackText != null)
        {
            feedbackText.text = "REPAIR COMPLETE!";
            feedbackText.color = Color.green;
        }

        // Tell server repair is complete
        CompleteRepairServerRpc();

        Invoke(nameof(CloseMinigame), 2f);
    }

    [ServerRpc(RequireOwnership = false)]
    private void CompleteRepairServerRpc(ServerRpcParams rpcParams = default)
    {
        // Capture repairer before clearing
        ulong repairerId = repairingPlayerId.Value;

        networkIsBroken.Value = false;
        repairingPlayerId.Value = ulong.MaxValue;

        // Check role mismatch — may release a monster even though terminal is fixed
        if (repairerId != ulong.MaxValue && AssignedRole != PlayerRole.None)
        {
            if (NetworkManager.ConnectedClients.TryGetValue(repairerId, out var client) && client.PlayerObject != null)
            {
                var roleCtrl = client.PlayerObject.GetComponent<PlayerRoleController>();
                if (roleCtrl != null && roleCtrl.Role != AssignedRole)
                {
                    int currentRound = GameManager.Instance != null ? GameManager.Instance.CurrentRound : 1;
                    float failureChance = 0.15f * currentRound;
                    if (Random.value < failureChance)
                    {
                        Debug.Log($"[Terminal] Role mismatch failure! Player role={roleCtrl.Role}, terminal role={AssignedRole}, chance={failureChance}");
                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.TerminalRepairFailed();
                        }
                    }
                }
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTerminalRepaired(this);
        }
    }

    void CancelMinigame()
    {
        CloseMinigame();
        // Release the lock on the server
        ReleaseRepairLockServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReleaseRepairLockServerRpc()
    {
        repairingPlayerId.Value = ulong.MaxValue;
    }

    void CloseMinigame()
    {
        minigameActive = false;
        HideUI();

        // Clear failure risk text
        GameObject riskObj = GameObject.Find("FailureRiskText");
        if (riskObj != null)
        {
            Text riskText = riskObj.GetComponent<Text>();
            if (riskText != null) riskText.text = "";
        }

        if (playerInput != null)
        {
            playerInput.cursorLocked = true;
            playerInput.cursorInputForLook = true;
        }

        if (playerController != null) playerController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Called by GameManager on server only
    public void BreakTerminal()
    {
        if (!IsServer) return;

        Debug.Log($"[Terminal] Breaking terminal: {gameObject.name}");
        networkIsBroken.Value = true;
    }

    public void SetAssignedRole(PlayerRole role)
    {
        if (!IsServer) return;
        assignedRole.Value = (int)role;
    }

    // Called by GameManager on server only
    public void ResetTerminal()
    {
        if (!IsServer) return;

        networkIsBroken.Value = false;
        repairingPlayerId.Value = ulong.MaxValue;
        assignedRole.Value = 0;
        // Local cleanup for any client running the minigame will happen via OnBrokenStateChanged
    }

    private void PlayBreakEffects()
    {
        if (breakParticleEffect != null)
        {
            ParticleSystem mainPS = breakParticleEffect.GetComponent<ParticleSystem>();
            if (mainPS != null)
            {
                Debug.Log($"[Terminal] Playing break particle: {mainPS.gameObject.name}");
                mainPS.Play();
            }
        }

        if (audioSource != null && breakSound != null)
        {
            audioSource.PlayOneShot(breakSound);
        }
    }

    private void StopBreakEffects()
    {
        if (breakParticleEffect != null)
        {
            ParticleSystem mainPS = breakParticleEffect.GetComponent<ParticleSystem>();
            if (mainPS != null)
            {
                mainPS.Stop();
            }
        }
    }

    void UpdateVisuals()
    {
        bool broken = IsSpawned ? networkIsBroken.Value : false;

        if (terminalLight != null) terminalLight.color = broken ? Color.red : Color.green;

        if (sphereRenderer != null)
        {
            if (broken && brokenMaterial != null) sphereRenderer.material = brokenMaterial;
            else if (!broken && workingMaterial != null) sphereRenderer.material = workingMaterial;
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
        return currentWeapon is RepairToolWeapon;
    }
}
