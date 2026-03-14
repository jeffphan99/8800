using System.Collections;
using UnityEngine;
using Unity.Netcode;
using StarterAssets;

public class ForceGunWeapon : WeaponBase
{
    [Header("Force Gun Settings")]
    public int maxUses = 2;
    public float maxChargeTime = 1.5f;

    [Header("Blast Shape (Cylinder)")]
    public float blastRadius = 3f;
    public float minBlastLength = 5f;
    public float maxBlastLength = 15f;

    [Header("Force")]
    public float minForce = 8f;
    public float maxForce = 22f;
    public float verticalLift = 0.2f;
    public float selfKnockbackMultiplier = 0.5f;

    [Header("Layers")]
    public LayerMask affectedLayers;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip chargeSound;
    public AudioClip fireSound;
    public AudioClip emptySound;

    [Header("Visual")]
    public ParticleSystem blastEffect;

    private int currentUses;
    private bool isCharging = false;
    private float chargeStartTime;
    private float currentCharge; // 0-1

    void Start()
    {
        currentUses = maxUses;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }
    }

    void Update()
    {
        // Lazy-init: _input may not be set if SetSharedReferences ran before this was in the array
        if (_input == null)
        {
            _input = GetComponentInParent<StarterAssetsInputs>();
            if (_input == null) return;
        }
        if (!gameObject.activeInHierarchy) return;

        // Don't fire during minigame
        Terminal activeTerminal = FindObjectOfType<Terminal>();
        if (activeTerminal != null && activeTerminal.minigameActive)
            return;

        // Hold left click to charge
        if (Input.GetMouseButton(0))
        {
            if (!isCharging && currentUses > 0)
            {
                StartCharge();
            }

            if (isCharging)
            {
                currentCharge = Mathf.Clamp01((Time.time - chargeStartTime) / maxChargeTime);
                UpdateChargeUI();
            }
        }
        // Release to fire
        else if (isCharging)
        {
            Fire();
        }

        // Empty click
        if (Input.GetMouseButtonDown(0) && currentUses <= 0 && !isCharging)
        {
            if (audioSource != null && emptySound != null)
                audioSource.PlayOneShot(emptySound);
        }
    }

    void StartCharge()
    {
        isCharging = true;
        chargeStartTime = Time.time;
        currentCharge = 0f;

        if (audioSource != null && chargeSound != null)
        {
            audioSource.clip = chargeSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (actionText != null)
        {
            actionText.enabled = true;
            actionText.color = Color.cyan;
        }
    }

    void Fire()
    {
        isCharging = false;
        currentUses--;

        // Stop charge sound
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }

        // Play fire sound
        if (audioSource != null && fireSound != null)
            audioSource.PlayOneShot(fireSound);

        // Hide charge UI
        if (actionText != null)
            actionText.enabled = false;

        float force = Mathf.Lerp(minForce, maxForce, currentCharge);
        float blastLength = Mathf.Lerp(minBlastLength, maxBlastLength, currentCharge);

        // Play blast effect
        if (blastEffect != null)
            blastEffect.Play();

        // Cylinder overlap: capsule along the player's forward direction
        Vector3 origin = transform.root.position + Vector3.up * 1f; // chest height
        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.root.forward;
        Vector3 capsuleStart = origin + forward * 0.5f;
        Vector3 capsuleEnd = origin + forward * blastLength;

        Collider[] hits = Physics.OverlapCapsule(capsuleStart, capsuleEnd, blastRadius, affectedLayers);

        bool hitAnything = false;

        foreach (Collider col in hits)
        {
            // Skip self
            if (col.transform.root == transform.root) continue;

            Vector3 toTarget = col.transform.position - origin;
            float distance = toTarget.magnitude;
            if (distance < 0.1f) continue;

            // Distance falloff: full force at close range, weaker at edge
            float distanceFactor = 1f - Mathf.Clamp01(distance / blastLength);
            float appliedForce = force * distanceFactor;

            // Push direction: along player's forward + vertical lift
            Vector3 pushDir = (forward + Vector3.up * verticalLift).normalized;
            Vector3 forceVector = pushDir * appliedForce;

            // Try monster
            MonsterAI monster = col.GetComponentInParent<MonsterAI>();
            if (monster != null)
            {
                ApplyForceToMonster(monster, forceVector);
                hitAnything = true;
                continue;
            }

            // Try other player
            FirstPersonController fpc = col.GetComponentInParent<FirstPersonController>();
            if (fpc != null)
            {
                // For networked players, we need to go through the network
                PlayerHealth targetHealth = fpc.GetComponent<PlayerHealth>();
                if (targetHealth != null && targetHealth.IsSpawned)
                {
                    // Request server to push the other player
                    ForceGunNetwork netHelper = GetComponentInParent<ForceGunNetwork>();
                    if (netHelper != null)
                    {
                        netHelper.RequestPushPlayerServerRpc(
                            targetHealth.OwnerClientId,
                            forceVector
                        );
                    }
                }
                hitAnything = true;
                continue;
            }
        }

        // Self knockback (opposite direction)
        FirstPersonController selfFpc = GetComponentInParent<FirstPersonController>();
        if (selfFpc != null)
        {
            Vector3 selfKnockback = (-forward + Vector3.up * verticalLift).normalized
                * force * selfKnockbackMultiplier;
            selfFpc.AddExternalForce(selfKnockback);
        }

        UpdateStatusUI();
        Debug.Log($"[ForceGun] Fired! Charge: {currentCharge:F2}, Force: {force:F1}, Hits: {hits.Length}");
    }

    void ApplyForceToMonster(MonsterAI monster, Vector3 forceVector)
    {
        StartCoroutine(PushMonsterCoroutine(monster, forceVector));
    }

    IEnumerator PushMonsterCoroutine(MonsterAI monster, Vector3 forceVector)
    {
        UnityEngine.AI.NavMeshAgent agent = monster.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent == null || !agent.isOnNavMesh) yield break;

        // Briefly disable agent to apply force manually
        float pushDuration = 0.4f;
        float elapsed = 0f;

        agent.isStopped = true;
        agent.updatePosition = false;

        Vector3 startPos = monster.transform.position;
        Vector3 endPos = startPos + new Vector3(forceVector.x, 0f, forceVector.z) * 0.5f;

        while (elapsed < pushDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pushDuration;
            // Ease out curve
            float easedT = 1f - (1f - t) * (1f - t);

            Vector3 pos = Vector3.Lerp(startPos, endPos, easedT);
            // Add vertical arc
            pos.y += Mathf.Sin(t * Mathf.PI) * forceVector.y * 0.3f;

            monster.transform.position = pos;
            yield return null;
        }

        // Re-sync agent to new position
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.Warp(monster.transform.position);
            agent.updatePosition = true;
            agent.isStopped = false;
        }
    }

    void UpdateChargeUI()
    {
        if (actionText != null)
        {
            int bars = Mathf.CeilToInt(currentCharge * 10);
            actionText.text = $"CHARGING [{new string('|', bars)}{new string('.', 10 - bars)}]";

            actionText.color = Color.Lerp(Color.cyan, Color.yellow, currentCharge);
        }
    }

    public override void PrimaryAction() { }
    public override void SecondaryAction() { }

    public override void UpdateStatusUI()
    {
        if (statusText != null)
        {
            statusText.text = $"Force: {currentUses}/{maxUses}";

            if (currentUses == 0)
                statusText.color = Color.red;
            else if (currentUses == 1)
                statusText.color = Color.yellow;
            else
                statusText.color = Color.cyan;
        }
    }

    public override void OnEquip()
    {
        base.OnEquip();
        Debug.Log("Force Gun equipped");
    }

    public override void OnUnequip()
    {
        base.OnUnequip();

        if (isCharging)
        {
            isCharging = false;
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }
            if (actionText != null)
                actionText.enabled = false;
        }
    }

    public override bool IsBusy()
    {
        return false; // allow weapon switching even while charging; OnUnequip cancels the charge
    }

    public void Replenish()
    {
        currentUses = maxUses;
        Debug.Log("[ForceGun] Uses replenished");
    }

    public int GetCurrentUses() => currentUses;
    public int GetMaxUses() => maxUses;
}
