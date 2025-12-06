using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Keep this for Text components

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Round Settings")]
    public float roundTime = 120f; // 2 minutes per round
    public float terminalBreakInterval = 30f; // Terminal breaks every 30 seconds
    public float terminalRepairTime = 15f; // Player has 15 seconds to fix it

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

    // NEW UI REFERENCES
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
        // Singleton pattern
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
        // Find player
        player = GameObject.FindGameObjectWithTag("Player");

        // Auto-find components
        if (allTerminals.Count == 0)
        {
            allTerminals.AddRange(FindObjectsOfType<Terminal>());
        }
        if (allMonsters.Count == 0)
        {
            allMonsters.AddRange(FindObjectsOfType<MonsterAI>());
        }
        if (allDoors.Count == 0)
        {
            allDoors.AddRange(FindObjectsOfType<Door>());
        }

        // Ensure panels are hidden at start
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        StartNewRound();
    }

    void Update()
    {
        if (!roundActive) return;

        // Update round timer
        currentRoundTime -= Time.deltaTime;
        UpdateTimerUI();

        // Check if round time is up
        if (currentRoundTime <= 0)
        {
            EndRound(true); // Player survived!
            return;
        }

        // IMPORTANT: Check terminal deadline FIRST (higher priority)
        if (terminalNeedsRepair && Time.time >= terminalBreakDeadline)
        {
            Debug.Log($"[GameManager] Terminal repair deadline reached! Time: {Time.time}, Deadline: {terminalBreakDeadline}");
            TerminalRepairFailed();
            return; // Stop here to process the failure
        }

        // Check if it's time to break a terminal
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

        // Hide end-game UI panels
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // Reset player position
        ResetPlayer();

        // Reset and deactivate all monsters
        foreach (MonsterAI monster in allMonsters)
        {
            monster.ResetMonster();
            monster.DeactivateMonster();
        }

        // Close all doors
        foreach (Door door in allDoors)
        {
            if (door.isOpen)
            {
                door.ToggleDoor();
            }
        }

        // Reset all terminals
        foreach (Terminal terminal in allTerminals)
        {
            terminal.ResetTerminal();
        }

        // Schedule first terminal break
        nextTerminalBreakTime = Time.time + terminalBreakInterval;

        // Update UI
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

        // Pick a random working terminal
        List<Terminal> workingTerminals = new List<Terminal>();
        foreach (Terminal terminal in allTerminals)
        {
            if (!terminal.isBroken)
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

        // Update UI
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

            // Schedule next terminal break
            nextTerminalBreakTime = Time.time + terminalBreakInterval;

            // Update UI
            if (terminalWarningText != null)
            {
                terminalWarningText.text = "Terminal repaired! All systems operational";
                terminalWarningText.color = Color.green;
            }
        }
    }

    void TerminalRepairFailed()
    {
        Debug.Log("=== TERMINAL REPAIR FAILED! RELEASING MONSTER! ===");
        terminalNeedsRepair = false;

        // Activate a random monster
        if (allMonsters.Count > 0)
        {
            MonsterAI randomMonster = allMonsters[Random.Range(0, allMonsters.Count)];
            randomMonster.ActivateMonster();
        }

        // Open all doors
        foreach (Door door in allDoors)
        {
            if (!door.isOpen)
            {
                door.ToggleDoor();
            }
        }

        // Update UI
        if (terminalWarningText != null)
        {
            terminalWarningText.text = "⚠ CONTAINMENT BREACH! MONSTER RELEASED!";
            terminalWarningText.color = Color.red;
        }

        if (gameStatusText != null)
        {
            gameStatusText.text = "DANGER! Monster is hunting you!";
            gameStatusText.color = Color.red;
        }

        // Schedule next terminal break
        nextTerminalBreakTime = Time.time + terminalBreakInterval;
    }

    void ResetPlayer()
    {
        if (player == null) return;

        if (playerSpawnPoint != null)
        {
            player.transform.position = playerSpawnPoint.position;
            player.transform.rotation = playerSpawnPoint.rotation;
        }

        // Reset player velocity if has rigidbody
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Reset player health if exists
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.ResetHealth();
        }
    }

    void EndRound(bool playerWon)
    {
        roundActive = false;

        // DEACTIVATE ALL MONSTERS (Stop chasing immediately)
        foreach (MonsterAI monster in allMonsters)
        {
            monster.DeactivateMonster();
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

        // Start new round after delay
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

            // Change color as time runs out
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

        // Update terminal repair countdown
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