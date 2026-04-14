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

    [Header("Death Animation")]
    [Tooltip("Name of the animator layer that holds the death animation (weight 0 by default)")]
    public string deathLayerName = "Death";

    private bool isDead = false;



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
        var cameraRoot = transform.Find("PlayerCameraRoot");
        if (cameraRoot != null)
        {
            if (!TryBindCinemachine(cameraRoot))
            {
                // Scene camera may not be ready yet (loading from lobby), retry shortly
                StartCoroutine(RetryBindCinemachine(cameraRoot));
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

    private bool TryBindCinemachine(Transform cameraRoot)
    {
        var allVCams = FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>();
        foreach (var vcam in allVCams)
        {
            if (vcam.GetComponentInParent<PlayerHealth>() != null)
                continue;
            vcam.Follow = cameraRoot;
            Debug.Log($"[PlayerHealth] Set Cinemachine Follow to {cameraRoot.name}");

            // Set up arms camera on the main camera
            var armsSetup = GetComponent<ArmsCameraSetup>();
            if (armsSetup != null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null) armsSetup.SetupArmsCamera(mainCam);
            }

            return true;
        }
        return false;
    }

    private System.Collections.IEnumerator RetryBindCinemachine(Transform cameraRoot)
    {
        for (int i = 0; i < 10; i++)
        {
            yield return null;
            if (TryBindCinemachine(cameraRoot))
                yield break;
        }
        Debug.LogWarning("[PlayerHealth] Could not find scene Cinemachine camera to bind to.");
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

        // Play death animation and remove collision on all clients
        PlayDeathClientRpc();

        // Tell owning client to disable controls
        EnterSpectatorClientRpc();
    }

    [ClientRpc]
    private void PlayDeathClientRpc()
    {
        // Enable death layer and restart the animation from the beginning
        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            int layerIndex = animator.GetLayerIndex(deathLayerName);
            if (layerIndex >= 0)
            {
                // Grab the state hash before setting weight so we can restart it from 0.
                // The animator runs the death layer internally at weight 0, so by the time
                // the player dies the animation is already at the end — we must replay it.
                int stateHash = animator.GetCurrentAnimatorStateInfo(layerIndex).fullPathHash;
                animator.SetLayerWeight(layerIndex, 1f);
                animator.Play(stateHash, layerIndex, 0f);
            }
        }

        // Remove CharacterController so dead body doesn't block movement
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
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

        isDead = true;

        // Follow an alive player's camera if one exists
        StartCoroutine(FindSpectatorTarget());

        Debug.Log("[PlayerHealth] Entering spectator mode");
    }

    private System.Collections.IEnumerator FindSpectatorTarget()
    {
        // Wait a frame so death state is fully applied before searching
        yield return null;

        if (GameManager.Instance == null) yield break;

        foreach (var playerObj in GameManager.Instance.GetActivePlayers())
        {
            if (playerObj == null || playerObj == gameObject) continue;

            var health = playerObj.GetComponent<PlayerHealth>();
            if (health == null || health.GetCurrentHealth() <= 0) continue;

            // Found an alive player — rebind Cinemachine to follow them
            var targetCameraRoot = playerObj.transform.Find("PlayerCameraRoot");
            if (targetCameraRoot == null) continue;

            var allVCams = FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>();
            foreach (var vcam in allVCams)
            {
                if (vcam.GetComponentInParent<PlayerHealth>() != null) continue;
                vcam.Follow = targetCameraRoot;
                Debug.Log($"[PlayerHealth] Spectating {playerObj.name}");
            }
            yield break;
        }
    }

    public void ResetHealth()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    [ClientRpc]
    public void WarpToSpawnClientRpc(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner) return;

        // Disable CC before moving — CharacterController fights manual position sets
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.position = position;
        transform.rotation = rotation;

        if (cc != null) cc.enabled = true;
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

        if (isDead)
        {
            isDead = false;

            // Restore collision
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            // Hide death layer so normal animations take over again
            var animator = GetComponent<Animator>();
            if (animator != null)
            {
                int layerIndex = animator.GetLayerIndex(deathLayerName);
                if (layerIndex >= 0)
                    animator.SetLayerWeight(layerIndex, 0f);
            }

            // Rebind Cinemachine back to own camera root
            var cameraRoot = transform.Find("PlayerCameraRoot");
            if (cameraRoot != null) TryBindCinemachine(cameraRoot);
        }
    }

    public float GetCurrentHealth() => currentHealth.Value;

    [ServerRpc(RequireOwnership = false)]
    public void ApplyBananaSlowServerRpc(float duration)
    {
        ApplyBananaSlowClientRpc(duration);
    }

    [ClientRpc]
    private void ApplyBananaSlowClientRpc(float duration)
    {
        if (!IsOwner) return;
        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) StartCoroutine(BananaSlowCoroutine(fpc, duration));
    }

    private System.Collections.IEnumerator BananaSlowCoroutine(FirstPersonController fpc, float duration)
    {
        float originalSpeed = fpc.MoveSpeed;
        float originalSprintSpeed = fpc.SprintSpeed;

        fpc.MoveSpeed = originalSpeed * 0.6f;
        fpc.SprintSpeed = originalSprintSpeed * 0.6f;

        yield return new WaitForSeconds(duration);

        fpc.MoveSpeed = originalSpeed;
        fpc.SprintSpeed = originalSprintSpeed;
    }

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
