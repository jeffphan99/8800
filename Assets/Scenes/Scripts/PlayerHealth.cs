using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

public class PlayerHealth : NetworkBehaviour
{
    /// <summary>The local player's GameObject — set once by the owning client.</summary>
    public static GameObject LocalPlayer;

    [Header("Health Settings")]
    public float maxHealth = 100f;
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>();

    [Header("UI")]
    public Text healthText;
    public Image healthBar;

    [Header("Audio")]
    public AudioClip hurtSound;
    public AudioSource audioSource;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        currentHealth.OnValueChanged += OnHealthChanged;

        if (IsOwner)
        {
            SetupOwner();
        }
        else
        {
            DisableNonOwnerComponents();
        }

        // Register with GameManager on all instances (server needs to track all players)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(gameObject);
        }

        UpdateHealthUI();
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;

        if (LocalPlayer == gameObject)
        {
            LocalPlayer = null;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterPlayer(gameObject);
        }
    }

    private void SetupOwner()
    {
        gameObject.tag = "Player";

        // Find UI elements dynamically if not set (network-spawned clones won't have scene references)
        if (healthText == null)
        {
            GameObject obj = GameObject.Find("Health");
            if (obj != null) healthText = obj.GetComponent<UnityEngine.UI.Text>();
        }
        if (healthBar == null)
        {
            GameObject obj = GameObject.Find("HealthImage");
            if (obj != null) healthBar = obj.GetComponent<UnityEngine.UI.Image>();
        }

        // Point the scene's Cinemachine virtual camera at this player's camera root.
        // The scene camera's Follow target is null after removing the scene-placed player.
        var cameraRoot = transform.Find("PlayerCameraRoot");
        if (cameraRoot != null)
        {
            var allVCams = FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>();
            foreach (var vcam in allVCams)
            {
                // Skip any vcam that's a child of a player (e.g. minimap cameras)
                if (vcam.GetComponentInParent<PlayerHealth>() != null)
                    continue;
                vcam.Follow = cameraRoot;
                Debug.Log($"[PlayerHealth] Set Cinemachine Follow to {cameraRoot.name}");
                break;
            }
        }

        // Tell FPC to delay movement for a few frames so the CharacterController
        // can be warped to the correct network-assigned position
        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null)
        {
            fpc.WarpAfterNetworkSpawn();
        }

        LocalPlayer = gameObject;
        Debug.Log($"[PlayerHealth] Owner setup complete on {gameObject.name}");
    }

    private void DisableNonOwnerComponents()
    {
        // Disable input
        var input = GetComponent<StarterAssetsInputs>();
        if (input != null) input.enabled = false;

        // Disable movement controller
        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = false;

        // Disable PlayerInput (new Input System)
        var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null) playerInput.enabled = false;

        // Disable CharacterController (prevents physics conflicts)
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Disable camera and audio listener on non-owner
        var cam = GetComponentInChildren<Camera>();
        if (cam != null) cam.enabled = false;

        var audioListener = GetComponentInChildren<AudioListener>();
        if (audioListener != null) audioListener.enabled = false;

        // Disable Cinemachine virtual camera if present
        var vcam = GetComponentInChildren<Cinemachine.CinemachineVirtualCamera>();
        if (vcam != null) vcam.gameObject.SetActive(false);

        // Disable weapon switcher
        var weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();
        if (weaponSwitcher != null) weaponSwitcher.enabled = false;

        // Disable all weapon scripts
        foreach (var weapon in GetComponentsInChildren<WeaponBase>(true))
        {
            weapon.enabled = false;
        }

        // Disable StressController (per-local-player post-processing)
        var stress = GetComponent<StressController>();
        if (stress != null) stress.enabled = false;

        // Disable ToolbarUI on non-owner (it references scene UI that won't exist on clones)
        var toolbar = GetComponentInChildren<ToolbarUI_Simple>();
        if (toolbar != null) toolbar.enabled = false;

        Debug.Log($"[PlayerHealth] Disabled non-owner components on {gameObject.name}");
    }

    private void OnHealthChanged(float previousValue, float newValue)
    {
        UpdateHealthUI();
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damage)
    {
        currentHealth.Value -= damage;
        currentHealth.Value = Mathf.Max(0, currentHealth.Value);

        if (currentHealth.Value <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"Player {OwnerClientId} has died!");

        if (IsServer && GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDeath(gameObject);
        }

        // Tell owning client to enter spectator mode
        EnterSpectatorClientRpc();
    }

    [ClientRpc]
    private void EnterSpectatorClientRpc()
    {
        if (!IsOwner) return;
        EnterSpectatorMode();
    }

    private void EnterSpectatorMode()
    {
        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = false;

        var input = GetComponent<StarterAssetsInputs>();
        if (input != null)
        {
            input.enabled = false;
            input.move = Vector2.zero;
            input.look = Vector2.zero;
        }

        var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null) playerInput.enabled = false;

        var weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();
        if (weaponSwitcher != null) weaponSwitcher.enabled = false;

        Debug.Log("[PlayerHealth] Entering spectator mode");
    }

    public void ResetHealth()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    public void ReenableControls()
    {
        if (!IsOwner) return;

        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = true;

        var input = GetComponent<StarterAssetsInputs>();
        if (input != null) input.enabled = true;

        var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null) playerInput.enabled = true;

        var weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();
        if (weaponSwitcher != null) weaponSwitcher.enabled = true;
    }

    public float GetCurrentHealth() => currentHealth.Value;

    void UpdateHealthUI()
    {
        if (!IsOwner) return;

        if (IsClient && currentHealth.Value < maxHealth && hurtSound != null && audioSource != null)
        {
            if (Time.frameCount > 10)
                audioSource.PlayOneShot(hurtSound);
        }

        if (healthText != null)
        {
            healthText.text = $"Health: {currentHealth.Value:F0}/{maxHealth:F0}";
        }

        if (healthBar != null)
        {
            healthBar.fillAmount = currentHealth.Value / maxHealth;

            if (currentHealth.Value / maxHealth > 0.6f)
            {
                healthBar.color = Color.green;
            }
            else if (currentHealth.Value / maxHealth > 0.3f)
            {
                healthBar.color = Color.yellow;
            }
            else
            {
                healthBar.color = Color.red;
            }
        }
    }
}
