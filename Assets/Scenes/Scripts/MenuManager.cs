using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [Header("Network Settings")]
    public string serverIP = "127.0.0.1";
    public ushort port = 9797;

    [Header("Main Menu UI")]
    public GameObject mainMenuPanel;
    public Button hostButton;
    public Button joinButton;
    public Button quitButton;

    [Header("Lobby UI")]
    public GameObject lobbyPanel;
    public Text statusText;
    public Text playerCountText;
    public Button startGameButton;
    public Button backButton;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
        startGameButton.onClick.AddListener(OnStartGameClicked);
        backButton.onClick.AddListener(OnBackClicked);

        ShowMainMenu();
    }

    void Update()
    {
        if (lobbyPanel.activeSelf)
            UpdateLobbyUI();
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // ── State ────────────────────────────────────────────────────

    void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
    }

    void ShowLobby(bool isHost)
    {
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        statusText.text = isHost ? "Waiting for players..." : "Connecting...";
        startGameButton.gameObject.SetActive(isHost);
    }

    // ── Button Callbacks ─────────────────────────────────────────

    public void OnHostClicked()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        SetTransport(nm, "0.0.0.0");
        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;
        nm.StartHost();
        ShowLobby(true);
    }

    public void OnJoinClicked()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        SetTransport(nm, serverIP);
        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;
        nm.StartClient();
        ShowLobby(false);
    }

    public void OnStartGameClicked()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsHost) return;

        nm.SceneManager.LoadScene("Main", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void OnBackClicked()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && (nm.IsClient || nm.IsServer))
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.Shutdown();
        }
        ShowMainMenu();
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Helpers ──────────────────────────────────────────────────

    void SetTransport(NetworkManager nm, string address)
    {
        var transport = nm.GetComponent<UnityTransport>();
        if (transport != null)
            transport.SetConnectionData(address, port, "0.0.0.0");
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[MenuManager] Client {clientId} connected");
    }

    void OnClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (!nm.IsServer && !nm.IsClient)
        {
            statusText.text = "Disconnected from server.";
            statusText.color = Color.red;
            Invoke(nameof(ShowMainMenu), 2f);
        }
    }

    void UpdateLobbyUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (nm.IsServer)
        {
            int count = nm.ConnectedClients.Count;
            playerCountText.text = $"Players: {count}/2";
            statusText.text = count >= 2 ? "All players connected!" : "Waiting for players...";
            statusText.color = count >= 2 ? Color.green : Color.gray;
        }
        else if (nm.IsClient)
        {
            statusText.text = "Connected — waiting for host to start...";
            statusText.color = Color.green;
        }
    }
}
