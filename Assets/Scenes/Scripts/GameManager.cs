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
    public float minTerminalBreakInterval = 7f;
    public float terminalBreakIntervalReduction = 2f;

    [Header("Difficulty Scaling")]
    public float monsterChaseSpeedScale = 0.1f;
    public int extraMonsterEveryNRounds = 3;
    public float gameOverDelay = 5f;
    public int maxRounds = 3;

    [Header("References")]
    public GameObject playerPrefab;
    public Transform playerSpawnPoint;
    public List<Transform> monsterSpawnPoints = new List<Transform>();
    public List<Terminal> allTerminals = new List<Terminal>();
    public List<MonsterAI> allMonsters = new List<MonsterAI>();
    public List<Door> allDoors = new List<Door>();
    public List<RoomLight> allLights = new List<RoomLight>();

    [Header("UI")]
    public Text roundTimerText;
    public Text terminalWarningText;
    public Text gameStatusText;
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Audio")]
    public AudioClip roundStartSound;
    public AudioClip roundWinSound;
    public AudioClip roundLoseSound;
    public AudioClip terminalBreakSound;
    public AudioClip containmentBreachSound;
    private AudioSource audioSource;

    // Synced state
    private NetworkVariable<float> networkRoundTime = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> networkRoundActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> networkCurrentRound = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int CurrentRound => networkCurrentRound.Value;

    private float currentRoundTime;
    private float nextTerminalBreakTime;
    private List<Terminal> currentBrokenTerminals = new List<Terminal>();
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
            return;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
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

        allLights.AddRange(FindObjectsOfType<RoomLight>(true));
        Debug.Log($"[GameManager] Found {allLights.Count} lights");

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
        currentBrokenTerminals.Clear();
        networkCurrentRound.Value++;

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

        // Repair all broken lights
        foreach (RoomLight light in allLights)
        {
            if (light != null && light.isBroken)
            {
                light.RepairLight();
            }
        }

        // Apply difficulty scaling — monsters get faster each round
        int round = networkCurrentRound.Value;
        foreach (MonsterAI monster in allMonsters)
        {
            if (monster != null)
            {
                monster.chaseSpeed = 3.2f * (1f + monsterChaseSpeedScale * (round - 1));
            }
        }

        // Assign roles to players (no duplicates)
        List<PlayerRole> rolePool = new List<PlayerRole> { PlayerRole.Silverback, PlayerRole.Neurochimp, PlayerRole.WrenchMonkey };
        ShuffleList(rolePool);

        List<PlayerRole> assignedRoles = new List<PlayerRole>();
        for (int i = 0; i < activePlayers.Count && i < rolePool.Count; i++)
        {
            var roleCtrl = activePlayers[i].GetComponent<PlayerRoleController>();
            if (roleCtrl != null)
            {
                roleCtrl.SetRole(rolePool[i]);
                assignedRoles.Add(rolePool[i]);
                Debug.Log($"[GameManager] Assigned role {rolePool[i]} to player {i}");
            }
        }

        // Assign a random role to each terminal (from roles actually in play)
        if (assignedRoles.Count > 0)
        {
            foreach (Terminal terminal in allTerminals)
            {
                if (terminal != null)
                {
                    PlayerRole terminalRole = assignedRoles[Random.Range(0, assignedRoles.Count)];
                    terminal.SetAssignedRole(terminalRole);
                }
            }
        }

        // Terminal break interval decreases with difficulty
        float scaledBreakInterval = Mathf.Max(minTerminalBreakInterval,
            terminalBreakInterval - terminalBreakIntervalReduction * (round - 1));
        nextTerminalBreakTime = Time.time + scaledBreakInterval;

        OnRoundStartClientRpc();
    }

    [ClientRpc]
    private void OnRoundStartClientRpc()
    {
        if (audioSource != null && roundStartSound != null)
            audioSource.PlayOneShot(roundStartSound);

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

        // Re-enable controls and replenish weapons for all local players
        foreach (var playerObj in activePlayers)
        {
            if (playerObj == null) continue;
            var health = playerObj.GetComponent<PlayerHealth>();
            if (health != null && health.IsOwner)
            {
                health.ReenableControls();

                // Replenish force gun uses
                var forceGun = playerObj.GetComponentInChildren<ForceGunWeapon>(true);
                if (forceGun != null) forceGun.Replenish();

                // Replenish jukebox battery
                var jukebox = playerObj.GetComponentInChildren<JukeboxWeapon>(true);
                if (jukebox != null) jukebox.Replenish();
            }
        }
    }

    void ResetAllPlayers()
    {
        if (!IsServer) return;

        Vector3 spawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
        Quaternion spawnRot = playerSpawnPoint != null ? playerSpawnPoint.rotation : Quaternion.identity;

        for (int i = 0; i < activePlayers.Count; i++)
        {
            var playerObj = activePlayers[i];
            if (playerObj == null) continue;

            PlayerHealth health = playerObj.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.ResetHealth();
                health.WarpToSpawnClientRpc(spawnPos, spawnRot);
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
                workingTerminals.Add(terminal);
        }

        if (workingTerminals.Count == 0)
        {
            float scaledInterval = Mathf.Max(minTerminalBreakInterval,
                terminalBreakInterval - terminalBreakIntervalReduction * (networkCurrentRound.Value - 1));
            nextTerminalBreakTime = Time.time + scaledInterval;
            return;
        }

        // Break one terminal per player, capped at available working terminals
        int toBreak = Mathf.Min(activePlayers.Count, workingTerminals.Count);
        ShuffleList(workingTerminals);

        currentBrokenTerminals.Clear();
        for (int i = 0; i < toBreak; i++)
        {
            workingTerminals[i].BreakTerminal();
            currentBrokenTerminals.Add(workingTerminals[i]);
            Debug.Log($"[GameManager] Terminal {workingTerminals[i].gameObject.name} has broken!");
        }

        terminalNeedsRepair = true;
        terminalBreakDeadline = Time.time + terminalRepairTime;

        string msg = toBreak > 1
            ? $"{toBreak} TERMINAL MALFUNCTIONS! Fix them quickly!"
            : "TERMINAL MALFUNCTION! Fix it quickly!";
        UpdateWarningTextClientRpc(msg, true);
        PlayTerminalBreakSoundClientRpc();
    }

    [ClientRpc]
    private void PlayTerminalBreakSoundClientRpc()
    {
        if (audioSource != null && terminalBreakSound != null)
            audioSource.PlayOneShot(terminalBreakSound);
    }

    public void OnTerminalRepaired(Terminal terminal)
    {
        if (!IsServer) return;
        if (!terminalNeedsRepair) return;

        currentBrokenTerminals.Remove(terminal);
        Debug.Log($"[GameManager] Terminal repaired. Remaining broken: {currentBrokenTerminals.Count}");

        if (currentBrokenTerminals.Count > 0)
        {
            // Still terminals left to repair — update warning text
            string msg = currentBrokenTerminals.Count > 1
                ? $"{currentBrokenTerminals.Count} TERMINALS still need repair!"
                : "1 TERMINAL still needs repair!";
            UpdateWarningTextClientRpc(msg, true);
        }
        else
        {
            // All repaired
            terminalNeedsRepair = false;
            lastWarningSeconds = -1;

            float scaledInterval = Mathf.Max(minTerminalBreakInterval,
                terminalBreakInterval - terminalBreakIntervalReduction * (networkCurrentRound.Value - 1));
            nextTerminalBreakTime = Time.time + scaledInterval;

            UpdateWarningTextClientRpc("All terminals repaired! Systems operational", false);
        }
    }

    public void TerminalRepairFailed()
    {
        Debug.Log("=== TERMINAL REPAIR FAILED! ===");

        terminalNeedsRepair = false;
        lastWarningSeconds = -1;

        // Determine how many monsters to release: 1 base + 1 per N rounds
        int round = networkCurrentRound.Value;
        int monstersToRelease = 1 + (round - 1) / extraMonsterEveryNRounds;

        List<MonsterAI> inactiveMonsters = new List<MonsterAI>();
        foreach (MonsterAI m in allMonsters)
        {
            if (m != null && !m.isActive) inactiveMonsters.Add(m);
        }

        // Shuffle and release up to monstersToRelease
        ShuffleList(inactiveMonsters);
        int released = Mathf.Min(monstersToRelease, inactiveMonsters.Count);
        MonsterAI firstMonster = null;

        for (int i = 0; i < released; i++)
        {
            inactiveMonsters[i].ActivateMonster();
            Debug.Log($"[GameManager] RELEASED MONSTER: {inactiveMonsters[i].gameObject.name}");
            if (i == 0) firstMonster = inactiveMonsters[i];
        }

        // Open cell doors nearest to the first released monster
        if (firstMonster != null)
        {
            List<Door> cellDoors = new List<Door>();
            foreach (Door d in allDoors)
            {
                if (d != null && d.isCellDoor) cellDoors.Add(d);
            }

            cellDoors.Sort((a, b) => {
                float distA = Vector3.Distance(firstMonster.transform.position, a.transform.position);
                float distB = Vector3.Distance(firstMonster.transform.position, b.transform.position);
                return distA.CompareTo(distB);
            });

            int doorsToOpen = Mathf.Min(2, cellDoors.Count);
            for (int i = 0; i < doorsToOpen; i++)
            {
                if (!cellDoors[i].isOpen)
                    cellDoors[i].OpenDoor();
            }
        }

        float scaledInterval = Mathf.Max(minTerminalBreakInterval,
            terminalBreakInterval - terminalBreakIntervalReduction * (round - 1));
        nextTerminalBreakTime = Time.time + scaledInterval;

        string warningMsg = released > 1
            ? $"CONTAINMENT BREACH! {released} MONSTERS RELEASED!"
            : "CONTAINMENT BREACH! SECTOR UNLOCKED!";
        UpdateWarningTextClientRpc(warningMsg, true);
        UpdateGameStatusClientRpc("DANGER! Monster is hunting you!", true);
        PlayContainmentBreachSoundClientRpc();
    }

    [ClientRpc]
    private void PlayContainmentBreachSoundClientRpc()
    {
        if (audioSource != null && containmentBreachSound != null)
            audioSource.PlayOneShot(containmentBreachSound);
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
            int currentRound = networkCurrentRound.Value;
            Debug.Log($"=== PLAYERS SURVIVED ROUND {currentRound}! ===");

            if (currentRound >= maxRounds)
            {
                // Players beat all rounds — final victory
                Debug.Log("=== PLAYERS WIN THE GAME! ===");
                OnRoundEndClientRpc(true, currentRound);
                // Reset and restart after longer delay
                networkCurrentRound.Value = 0;
                Invoke(nameof(StartNewRound), gameOverDelay);
            }
            else
            {
                OnRoundEndClientRpc(true, currentRound);
                Invoke(nameof(StartNewRound), 3f);
            }
        }
        else
        {
            int roundReached = networkCurrentRound.Value;
            Debug.Log($"=== GAME OVER on round {roundReached}! ===");
            OnRoundEndClientRpc(false, roundReached);
            // Reset round counter so next run starts at round 1
            networkCurrentRound.Value = 0;
            Invoke(nameof(StartNewRound), gameOverDelay);
        }
    }

    [ClientRpc]
    private void OnRoundEndClientRpc(bool playerWon, int roundReached)
    {
        if (audioSource != null)
        {
            AudioClip clip = playerWon ? roundWinSound : roundLoseSound;
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        if (playerWon)
        {
            if (winPanel != null) winPanel.SetActive(true);
            if (gameStatusText != null)
            {
                if (roundReached >= maxRounds)
                {
                    gameStatusText.text = "VICTORY! All rounds survived! Restarting...";
                    gameStatusText.color = Color.green;
                }
                else
                {
                    gameStatusText.text = $"Round {roundReached} survived! Next round starting...";
                    gameStatusText.color = Color.green;
                }
            }
        }
        else
        {
            if (losePanel != null) losePanel.SetActive(true);
            if (gameStatusText != null)
            {
                gameStatusText.text = $"GAME OVER — Survived {roundReached} round{(roundReached != 1 ? "s" : "")}. Restarting...";
                gameStatusText.color = Color.red;
            }
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

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
