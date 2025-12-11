using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Round Settings")]
    public float roundTime = 120f;
    public float terminalBreakInterval = 30f;
    public float terminalRepairTime = 15f;

    [Header("References")]
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

    private float currentRoundTime;
    private float nextTerminalBreakTime;
    private Terminal currentBrokenTerminal;
    private float terminalBreakDeadline;
    private bool terminalNeedsRepair = false;
    private bool roundActive = false;

    private GameObject player;

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

        // Find player
        player = GameObject.FindGameObjectWithTag("Player");
        Debug.Log($"Player found: {player != null}");

        // CLEAR LISTS FIRST (removes old/invalid references from Inspector)
        allTerminals.Clear();
        allMonsters.Clear();
        allDoors.Clear();

        // Find terminals - INCLUDE INACTIVE
        allTerminals.AddRange(FindObjectsOfType<Terminal>(true));
        Debug.Log($"[GameManager] Found {allTerminals.Count} terminals");

        // Find monsters - INCLUDE INACTIVE (CRITICAL!)
        allMonsters.AddRange(FindObjectsOfType<MonsterAI>(true));
        Debug.Log($"[GameManager] Found {allMonsters.Count} monsters:");

        foreach (MonsterAI monster in allMonsters)
        {
            if (monster != null)
            {
                Debug.Log($"[GameManager]   - {monster.gameObject.name} ({monster.GetType().Name})");
            }
        }

        if (allMonsters.Count == 0)
        {
            Debug.LogError("[GameManager] NO MONSTERS FOUND!");
        }

        // Find doors - INCLUDE INACTIVE
        allDoors.AddRange(FindObjectsOfType<Door>(true));
        Debug.Log($"[GameManager] Found {allDoors.Count} doors");

        // Ensure panels are hidden at start
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        Debug.Log("=== GAME MANAGER START COMPLETE ===");
        StartNewRound();
    }

    void Update()
    {
        if (!roundActive) return;

        currentRoundTime -= Time.deltaTime;
        UpdateTimerUI();

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
        Debug.Log("=== STARTING NEW ROUND ===");
        roundActive = true;
        currentRoundTime = roundTime;
        terminalNeedsRepair = false;

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        ResetPlayer();

        // Reset and deactivate all monsters - WITH NULL CHECK
        foreach (MonsterAI monster in allMonsters)
        {
            if (monster != null)
            {
                monster.ResetMonster();
                monster.DeactivateMonster();
            }
        }

        // Close all doors - WITH NULL CHECK
        foreach (Door door in allDoors)
        {
            if (door != null && door.isOpen)
            {
                door.ToggleDoor();
            }
        }

        // Reset all terminals - WITH NULL CHECK
        foreach (Terminal terminal in allTerminals)
        {
            if (terminal != null)
            {
                terminal.ResetTerminal();
            }
        }

        nextTerminalBreakTime = Time.time + terminalBreakInterval;

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

        if (terminalWarningText != null)
        {
            terminalWarningText.text = $"⚠ TERMINAL MALFUNCTION! Fix it quickly!";
            terminalWarningText.color = Color.red;
        }
    }

    public void OnTerminalRepaired(Terminal terminal)
    {
        if (terminal == currentBrokenTerminal && terminalNeedsRepair)
        {
            Debug.Log("Terminal repaired in time!");
            terminalNeedsRepair = false;
            currentBrokenTerminal = null;

            nextTerminalBreakTime = Time.time + terminalBreakInterval;

            if (terminalWarningText != null)
            {
                terminalWarningText.text = "Terminal repaired! All systems operational";
                terminalWarningText.color = Color.green;
            }
        }
    }

    void TerminalRepairFailed()
    {
        Debug.Log("=== TERMINAL REPAIR FAILED! ===");

        terminalNeedsRepair = false;
        MonsterAI activeMonster = null;

        // 1. Activate a random monster
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

            // Loop through the first 2 doors in the sorted list (closest ones)
            int doorsToOpen = Mathf.Min(2, allDoors.Count);
            Debug.Log($"[GameManager] Breaching closest {doorsToOpen} doors near monster...");

            for (int i = 0; i < doorsToOpen; i++)
            {
                Door door = allDoors[i];
                if (door != null)
                {
                    if (!door.isOpen)
                    {
                        door.ToggleDoor(); // Or door.OpenDoor() if you prefer
                        Debug.Log($"[GameManager]   > BREACHED DOOR: {door.name}");
                    }
                    else
                    {
                        Debug.Log($"[GameManager]   > Door {door.name} was already open.");
                    }
                }
            }
        }
        else
        {
            // Fallback if no monster was found (open closest to player instead?)
            Debug.LogWarning("[GameManager] No active monster to calculate door distance from!");
        }

        // UI Updates
        if (terminalWarningText != null)
        {
            terminalWarningText.text = "⚠ CONTAINMENT BREACH! SECTOR UNLOCKED!";
            terminalWarningText.color = Color.red;
        }

        if (gameStatusText != null)
        {
            gameStatusText.text = "DANGER! Monster is hunting you!";
            gameStatusText.color = Color.red;
        }

        nextTerminalBreakTime = Time.time + terminalBreakInterval;
        Debug.Log("=== TERMINAL REPAIR FAILED - COMPLETE ===");
    }

    void ResetPlayer()
    {
        if (player == null) return;

        if (playerSpawnPoint != null)
        {
            player.transform.position = playerSpawnPoint.position;
            player.transform.rotation = playerSpawnPoint.rotation;
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.ResetHealth();
        }
    }

    void EndRound(bool playerWon)
    {
        roundActive = false;

        foreach (MonsterAI monster in allMonsters)
        {
            if (monster != null)
            {
                monster.DeactivateMonster();
            }
        }

        if (playerWon)
        {
            Debug.Log("=== PLAYER SURVIVED THE ROUND! ===");
            if (winPanel != null)
            {
                winPanel.SetActive(true);
            }
        }
        else
        {
            Debug.Log("=== PLAYER DIED! ===");
            if (losePanel != null)
            {
                losePanel.SetActive(true);
            }
        }

        Invoke(nameof(StartNewRound), 3f);
    }

    public void OnPlayerDeath()
    {
        if (roundActive)
        {
            EndRound(false);
        }
    }

    void UpdateTimerUI()
    {
        if (roundTimerText != null)
        {
            int minutes = Mathf.FloorToInt(currentRoundTime / 60f);
            int seconds = Mathf.FloorToInt(currentRoundTime % 60f);
            roundTimerText.text = $"Time: {minutes:00}:{seconds:00}";

            if (currentRoundTime < 30f)
            {
                roundTimerText.color = Color.red;
            }
            else if (currentRoundTime < 60f)
            {
                roundTimerText.color = Color.yellow;
            }
            else
            {
                roundTimerText.color = Color.white;
            }
        }

        if (terminalNeedsRepair && terminalWarningText != null)
        {
            float timeLeft = terminalBreakDeadline - Time.time;
            terminalWarningText.text = $"⚠ TERMINAL MALFUNCTION! Repair in {Mathf.CeilToInt(timeLeft)}s or monster releases!";
        }
    }

    public bool IsRoundActive()
    {
        return roundActive;
    }
}