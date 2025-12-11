using UnityEngine;
using System.Collections.Generic;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager Instance { get; private set; }

    [Header("Camera Settings")]
    public Camera minimapCamera;
    public float cameraHeight = 50f;
    public float orthographicSize = 30f;

    [Header("Player Tracking")]
    public Transform player;
    public bool followPlayer = true;
    public Vector3 cameraOffset = new Vector3(0, 50, 0);

    [Header("Icon Prefabs (Optional)")]
    public GameObject playerIconPrefab;
    public GameObject terminalIconPrefab;
    public GameObject monsterIconPrefab;

    [Header("Colors")]
    public Color playerColor = Color.cyan;
    public Color terminalWorkingColor = Color.green;
    public Color terminalBrokenColor = Color.red;
    public Color monsterColor = Color.red;

    private Dictionary<GameObject, GameObject> trackedIcons = new Dictionary<GameObject, GameObject>();
    private GameObject playerIcon;

    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Find minimap camera if not assigned
        if (minimapCamera == null)
        {
            minimapCamera = GetComponent<Camera>();
            if (minimapCamera == null)
            {
                Debug.LogError("MinimapManager: No camera found!");
                return;
            }
        }

        // Find player if not assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        // Create player icon
        if (player != null)
        {
            CreatePlayerIcon();
        }

        // Auto-find and track objects
        Invoke(nameof(AutoTrackObjects), 0.1f);
    }

    void LateUpdate()
    {
        if (followPlayer && player != null && minimapCamera != null)
        {
            // Make camera follow player's X and Z position
            Vector3 newPos = player.position + cameraOffset;
            minimapCamera.transform.position = newPos;
        }
    }

    void CreatePlayerIcon()
    {
        if (playerIconPrefab != null)
        {
            playerIcon = Instantiate(playerIconPrefab);
        }
        else
        {
            // Create a simple arrow/triangle to show direction
            playerIcon = new GameObject("PlayerMinimapIcon");

            // Create a quad for the icon
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(playerIcon.transform);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.Euler(90, 0, 0);
            quad.transform.localScale = Vector3.one * 3f; // Slightly larger than other icons

            // Remove collider
            Collider col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Set color
            Renderer renderer = quad.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = playerColor;
                renderer.material = mat;
            }
        }

        playerIcon.transform.SetParent(player);
        playerIcon.transform.localPosition = new Vector3(0, 50, 0);
        playerIcon.layer = LayerMask.NameToLayer("Minimap");

        // Make all children also on minimap layer
        foreach (Transform child in playerIcon.transform)
        {
            child.gameObject.layer = LayerMask.NameToLayer("Minimap");
        }
    }

    void AutoTrackObjects()
    {
        // Track all terminals
        Terminal[] terminals = FindObjectsOfType<Terminal>();
        foreach (Terminal terminal in terminals)
        {
            TrackTerminal(terminal);
        }

        // Track all monsters
        MonsterAI[] monsters = FindObjectsOfType<MonsterAI>();
        foreach (MonsterAI monster in monsters)
        {
            TrackMonster(monster);
        }

        Debug.Log($"MinimapManager: Tracking {terminals.Length} terminals and {monsters.Length} monsters");
    }

    public void TrackTerminal(Terminal terminal)
    {
        if (terminal == null || trackedIcons.ContainsKey(terminal.gameObject)) return;

        GameObject icon = CreateMinimapIcon(
            terminal.transform,
            terminalIconPrefab,
            terminal.isBroken ? terminalBrokenColor : terminalWorkingColor,
            1.5f // Size
        );

        trackedIcons[terminal.gameObject] = icon;
    }

    public void TrackMonster(MonsterAI monster)
    {
        if (monster == null || trackedIcons.ContainsKey(monster.gameObject)) return;

        GameObject icon = CreateMinimapIcon(
            monster.transform,
            monsterIconPrefab,
            monsterColor,
            2f // Slightly larger
        );

        // Initially hide if monster not active
        if (!monster.isActive)
        {
            icon.SetActive(false);
        }

        trackedIcons[monster.gameObject] = icon;
    }

    GameObject CreateMinimapIcon(Transform target, GameObject prefab, Color color, float size = 2f)
    {
        GameObject icon;

        if (prefab != null)
        {
            icon = Instantiate(prefab);
            icon.transform.localScale = Vector3.one * size;
        }
        else
        {
            // Create default quad
            icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            icon.transform.localScale = Vector3.one * size;

            // Remove collider
            Collider col = icon.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Set color
            Renderer renderer = icon.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = color;
                renderer.material = mat;
            }
        }

        icon.name = target.name + "_MinimapIcon";
        icon.transform.SetParent(target);
        icon.transform.localPosition = new Vector3(0, 50, 0); // Height above object
        icon.transform.rotation = Quaternion.Euler(90, 0, 0); // Face up
        icon.layer = LayerMask.NameToLayer("Minimap");

        return icon;
    }

    public void UpdateTerminalIcon(Terminal terminal)
    {
        if (terminal == null) return;

        if (trackedIcons.ContainsKey(terminal.gameObject))
        {
            GameObject icon = trackedIcons[terminal.gameObject];
            if (icon != null)
            {
                Renderer renderer = icon.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = terminal.isBroken ? terminalBrokenColor : terminalWorkingColor;
                }
            }
        }
        else
        {
            // Icon doesn't exist, create it
            TrackTerminal(terminal);
        }
    }

    public void UpdateMonsterIcon(MonsterAI monster, bool isActive)
    {
        if (monster == null) return;

        if (trackedIcons.ContainsKey(monster.gameObject))
        {
            GameObject icon = trackedIcons[monster.gameObject];
            if (icon != null)
            {
                icon.SetActive(isActive);
            }
        }
    }

}