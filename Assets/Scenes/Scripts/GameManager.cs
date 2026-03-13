using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Round Settings")]
    public float roundTime = 120f;
    public float terminalBreakInterval = 30f;
    public float terminalRepairTime = 15f;

    [Header("References")]
    public GameObject playerPrefab;
    public Transform playerSpawnPoint;
    public List<Transform> monsterSpawnPoints = new List<Transform>();
    public List<Terminal> allTerminals = new List<Terminal>();
    public List<MonsterAI> allMonsters = new List<MonsterAI>();
    public List<Door> allDoors = new List<Door>();

    [Header("UI")]
    public Text roundTimerText;
    public Text terminalWarningText;
    public Text gameStatusText;
    public GameObject winPanel;
    public GameObject losePanel;

    // Synced state
    private NetworkVariable<float> networkRoundTime = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> networkRoundActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float currentRoundTime;
    private float nextTerminalBreakTime;
    private Terminal currentBrokenTerminal;
    private float terminalBreakDeadline;
    private bool terminalNeedsRepair = false;
    private bool roundActive = false;
    private int lastWarningSeconds = -1; // Prevent ClientRpc spam

    // Multiplayer player tracking
    private List<GameObject> activePlayers = new List<GameObject>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.Log("=== GAME MANAGER START ===");

        allTerminals.Clear();
        allMonsters.Clear();
        allDoors.Clear();

        allTerminals.AddRange(FindObjectsOfType<Terminal>(true));
        Debug.Log($"[GameManager] Found {allTerminals.Count} terminals");

        allMonsters.AddRange(FindObjectsOfType<MonsterAI>(true));
        Debug.Log($"[GameManager] Found {allMonsters.Count} monsters");

        allDoors.AddRange(FindObjectsOfType<Door>(true));
        Debug.Log($"[GameManager] Found {allDoors.Count} doors");

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // Tell ConnectionApprovalHandler where to spawn new players
        if (playerSpawnPoint != null)
        {
            Unity.Template.Multiplayer.NGO.Runtime.ConnectionApprovalHandler.s_SpawnPosition = playerSpawnPoint.position;
            Unity.Template.Multiplayer.NGO.Runtime.ConnectionApprovalHandler.s_SpawnRotation = playerSpawnPoint.rotation;
            Debug.Log($"[GameManager] Set player spawn position to {playerSpawnPoint.position}");
        }

        Debug.Log("=== GAME MANAGER START COMPLETE ===");
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Delay spawn slightly so clients have finished loading the scene
            StartCoroutine(DelayedSpawnAndStart());
        }
    }

    private IEnumerator DelayedSpawnAndStart()
    {
        // Wait a frame for all scene objects to initialize on clients
        yield return null;
        SpawnPlayers();
        StartCoroutine(WaitForPlayersAndStart());
    }

    void SpawnPlayers()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[GameManager] playerPrefab not assigned!");
            return;
        }

        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            Vector3 pos = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
            Quaternion rot = playerSpawnPoint != null ? playerSpawnPoint.rotation : Quaternion.identity;

            var player = Instantiate(playerPrefab, pos, rot);
            var netObj = player.GetComponent<NetworkObject>();
            netObj.SpawnAsPlayerObject(client.Key);
            Debug.Log($"[GameManager] Spawned player for client {client.Key}");
        }
    }

    private IEnumerator WaitForPlayersAndStart()
    {
        float timeout = 15f;
        float elapsed = 0f;
        while (activePlayers.Count < 1 && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(1f);
        StartNewRound();
    }

    // Player registration
    public void RegisterPlayer(GameObject playerObj)
    {
        if (!activePlayers.Contains(playerObj))
        {
            activePlayers.Add(playerObj);
            Debug.Log($"[GameManager] Player registered. Total: {activePlayers.Count}");
        }
    }

    public void UnregisterPlayer(GameObject playerObj)
    {
        activePlayers.Remove(playerObj);
        Debug.Log($"[GameManager] Player unregistered. Total: {activePlayers.Count}");
    }

    public List<GameObject> GetActivePlayers() => activePlayers;

    void Update()
    {
        // All clients: update UI
        UpdateTimerUI();

        // Server only: run game logic
        if (!IsSpawned || !IsServer) return;
        if (!roundActive) return;

        currentRoundTime -= Time.deltaTime;
        networkRoundTime.Value = currentRoundTime;

        if (currentRoundTime <= 0)
        {
            EndRound(true);
            return;
        }

        if (terminalNeedsRepair && Time.time >= terminalBreakDeadline)
        {
            Debug.Log($"[GameManager] Terminal repair deadline reached! Time: {Time.time}, Deadline: {terminalBreakDeadline}");
            TerminalRepairFailed();
            return;
        }

        if (!terminalNeedsRepair && Time.time >= nextTerminalBreakTime)
        {
            BreakRandomTerminal();
        }
    }

    public void StartNewRound()
    {
        if (!IsServer) return;

        Debug.Log("=== STARTING NEW ROUND ===");
        roundActive = true;
        networkRoundActive.Value = true;
        currentRoundTime = roundTime;
        networkRoundTime.Value = roundTime;
        terminalNeedsRepair = false;

        // Reset all players
        ResetAllPlayers();

        // Reset and deactivate all monsters
        foreach (MonsterAI monster in allMonsters)
        {
            if (monster != null)
            {
                monster.ResetMonster();
                monster.DeactivateMonster();
            }
        }

        // Close all doors
        foreach (Door door in allDoors)
        {
            if (door != null && door.isOpen)
            {
                door.ToggleDoor();
            }
        }

        // Reset all terminals
        foreach (Terminal terminal in allTerminals)
        {
            if (terminal != null)
            {
                terminal.ResetTerminal();
            }
        }

        nextTerminalBreakTime = Time.time + terminalBreakInterval;

        OnRoundStartClientRpc();
    }

    [ClientRpc]
    private void OnRoundStartClientRpc()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        if (gameStatusText != null)
        {
            gameStatusText.text = "";
            gameStatusText.color = Color.green;
        }

        if (terminalWarningText != null)
        {
            terminalWarningText.text = "All systems operational";
            terminalWarningText.color = Color.green;
        }

        // Re-enable controls for all local players (in case they were spectating)
        foreach (var playerObj in activePlayers)
        {
            if (playerObj == null) continue;
            var health = playerObj.GetComponent<PlayerHealth>();
            if (health != null && health.IsOwner)
            {
                health.ReenableControls();
            }
        }
    }

    void ResetAllPlayers()
    {
        if (!IsServer) return;

        for (int i = 0; i < activePlayers.Count; i++)
        {
            var playerObj = activePlayers[i];
            if (playerObj == null) continue;

            PlayerHealth health = playerObj.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.ResetHealth();
            }
        }
    }

    void BreakRandomTerminal()
    {
        if (allTerminals.Count == 0) return;

        List<Terminal> workingTerminals = new List<Terminal>();
        foreach (Terminal terminal in allTerminals)
        {
            if (terminal != null && !terminal.isBroken)
            {
                workingTerminals.Add(terminal);
            }
        }

        if (workingTerminals.Count == 0) return;

        currentBrokenTerminal = workingTerminals[Random.Range(0, workingTerminals.Count)];
        currentBrokenTerminal.BreakTerminal();

        terminalNeedsRepair = true;
        terminalBreakDeadline = Time.time + terminalRepairTime;

        Debug.Log($"Terminal {currentBrokenTerminal.gameObject.name} has broken! Fix it in {terminalRepairTime} seconds!");

        UpdateWarningTextClientRpc($"TERMINAL MALFUNCTION! Fix it quickly!", true);
    }

    public void OnTerminalRepaired(Terminal terminal)
    {
        if (!IsServer) return;

        if (terminal == currentBrokenTerminal && terminalNeedsRepair)
        {
            Debug.Log("Terminal repaired in time!");
            terminalNeedsRepair = false;
            lastWarningSeconds = -1;
            currentBrokenTerminal = null;

            nextTerminalBreakTime = Time.time + terminalBreakInterval;

            UpdateWarningTextClientRpc("Terminal repaired! All systems operational", false);
        }
    }

    void TerminalRepairFailed()
    {
        Debug.Log("=== TERMINAL REPAIR FAILED! ===");

        terminalNeedsRepair = false;
        lastWarningSeconds = -1;
        MonsterAI activeMonster = null;

        if (allMonsters.Count > 0)
        {
            List<MonsterAI> validMonsters = new List<MonsterAI>();
            foreach (MonsterAI m in allMonsters)
            {
                if (m != null) validMonsters.Add(m);
            }

            if (validMonsters.Count > 0)
            {
                int randomIndex = Random.Range(0, validMonsters.Count);
                activeMonster = validMonsters[randomIndex];
                activeMonster.ActivateMonster();
                Debug.Log($"[GameManager] RELEASED MONSTER: {activeMonster.gameObject.name}");
            }
        }

        if (activeMonster != null && allDoors.Count > 0)
        {
            allDoors.Sort((a, b) => {
                if (a == null || b == null) return 0;
                float distA = Vector3.Distance(activeMonster.transform.position, a.transform.position);
                float distB = Vector3.Distance(activeMonster.transform.position, b.transform.position);
                return distA.CompareTo(distB);
            });

            int doorsToOpen = Mathf.Min(2, allDoors.Count);
            for (int i = 0; i < doorsToOpen; i++)
            {
                Door door = allDoors[i];
                if (door != null && !door.isOpen)
                {
                    door.ToggleDoor();
                }
            }
        }

        UpdateWarningTextClientRpc("CONTAINMENT BREACH! SECTOR UNLOCKED!", true);
        UpdateGameStatusClientRpc("DANGER! Monster is hunting you!", true);

        nextTerminalBreakTime = Time.time + terminalBreakInterval;
    }

    [ClientRpc]
    private void UpdateWarningTextClientRpc(string text, bool isRed)
    {
        if (terminalWarningText != null)
        {
            terminalWarningText.text = text;
            terminalWarningText.color = isRed ? Color.red : Color.green;
        }
    }

    [ClientRpc]
    private void UpdateGameStatusClientRpc(string text, bool isRed)
    {
        if (gameStatusText != null)
        {
            gameStatusText.text = text;
            gameStatusText.color = isRed ? Color.red : Color.green;
        }
    }

    public void OnPlayerDeath(GameObject deadPlayer)
    {
        if (!IsServer) return;

        int aliveCount = 0;
        foreach (var p in activePlayers)
        {
            if (p == null) continue;
            var health = p.GetComponent<PlayerHealth>();
            if (health != null && health.GetCurrentHealth() > 0)
                aliveCount++;
        }

        Debug.Log($"[GameManager] Player died. Alive players: {aliveCount}");

        if (aliveCount <= 0 && roundActive)
        {
            EndRound(false);
        }
    }

    void EndRound(bool playerWon)
    {
        if (!IsServer) return;

        roundActive = false;
        networkRoundActive.Value = false;

        foreach (MonsterAI monster in allMonsters)
        {
            if (monster != null)
            {
                monster.DeactivateMonster();
            }
        }

        if (playerWon)
        {
            Debug.Log("=== PLAYERS SURVIVED THE ROUND! ===");
        }
        else
        {
            Debug.Log("=== ALL PLAYERS DIED! ===");
        }

        OnRoundEndClientRpc(playerWon);
        Invoke(nameof(StartNewRound), 3f);
    }

    [ClientRpc]
    private void OnRoundEndClientRpc(bool playerWon)
    {
        if (playerWon)
        {
            if (winPanel != null) winPanel.SetActive(true);
        }
        else
        {
            if (losePanel != null) losePanel.SetActive(true);
        }
    }

    void UpdateTimerUI()
    {
        if (!IsSpawned) return;

        float displayTime = IsServer ? currentRoundTime : networkRoundTime.Value;
        bool isActive = IsServer ? roundActive : networkRoundActive.Value;

        if (!isActive) return;

        if (roundTimerText != null)
        {
            int minutes = Mathf.FloorToInt(displayTime / 60f);
            int seconds = Mathf.FloorToInt(displayTime % 60f);
            roundTimerText.text = $"Time: {minutes:00}:{seconds:00}";

            if (displayTime < 30f)
            {
                roundTimerText.color = Color.red;
            }
            else if (displayTime < 60f)
            {
                roundTimerText.color = Color.yellow;
            }
            else
            {
                roundTimerText.color = Color.white;
            }
        }

        // Only send ClientRpc when the countdown seconds actually change (not every frame)
        if (IsServer && terminalNeedsRepair)
        {
            float timeLeft = terminalBreakDeadline - Time.time;
            int secondsLeft = Mathf.CeilToInt(timeLeft);
            if (secondsLeft != lastWarningSeconds)
            {
                lastWarningSeconds = secondsLeft;
                UpdateWarningTextClientRpc($"TERMINAL MALFUNCTION! Repair in {secondsLeft}s or monster releases!", true);
            }
        }
    }

    public bool IsRoundActive()
    {
        return IsServer ? roundActive : networkRoundActive.Value;
    }
}
