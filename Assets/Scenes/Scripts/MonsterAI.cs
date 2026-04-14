using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : NetworkBehaviour
{
    [Header("Monster Settings")]
    public float detectionRange = 10f;
    public float attackRange = 2f;
    public float attackDamage = 50f;
    public float attackCooldown = 1f;

    [Header("Visual Indicators")]
    [Tooltip("Object to toggle when monster hears noise (e.g., Yellow Sphere)")]
    public GameObject noiseIndicator;
    [Tooltip("Object to toggle when monster is slowed by light (e.g., White Sphere)")]
    public GameObject lightEffectIndicator;

    private float noiseIndicatorTimer = 0f;

    [Header("Patrol Settings")]
    public float patrolSpeed = 1.6f;
    public float chaseSpeed = 3.2f;
    public float patrolRadius = 60f;
    public float waitTimeAtDestination = 2f;
    public bool useRandomPatrol = true;

    [Header("Noise Detection")]
    public float noiseDetectionRange = 15f;
    public LayerMask noiseSourceLayer;

    [Header("Sleep Settings")]
    public float getUpDuration = 1.5f;

    [Header("Spawn Settings")]
    public Transform spawnPoint;

    [Header("Debug")]
    public bool debugToggleActive = false;

    [Header("Audio")]
    public AudioClip footstepSound;
    public AudioClip wakeUpSound;
    [Range(0f, 1f)] public float footstepVolume = 0.5f;
    [Range(0f, 1f)] public float wakeUpVolume = 0.8f;

    [Header("Light Breaking")]
    public float lightBreakRadius = 4f;
    public float lightCheckInterval = 0.5f;
    protected float lastLightCheckTime = 0f;

    [Header("Door Interaction")]
    public float doorCheckRadius = 2.5f;
    public float doorCheckInterval = 0.3f;
    private float lastDoorCheckTime = 0f;

    [Header("Flashlight Interaction")]
    public bool affectedByFlashlight = false;
    public float flashlightSlowMultiplier = 0.3f;
    public float flashlightEffectDuration = 0.5f;
    protected float flashlightEffectTimer = 0f;
    protected bool isFlashlightShining = false;

    protected Transform player;
    protected float lastAttackTime;
    protected bool isAsleep = false;

    // Networked active state — clients use this to show/hide the monster
    private NetworkVariable<bool> networkIsActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool isActive => networkIsActive.Value;

    protected Vector3 originalPosition;
    protected Quaternion originalRotation;
    protected Animator animator;
    protected NavMeshAgent agent;

    protected enum AIState { Idle, Patrolling, Chasing, Attacking }
    protected AIState currentState = AIState.Idle;
    protected bool isWaiting = false;
    protected Vector3 lastKnownPlayerPosition;
    private Vector3 lastSetDestination = Vector3.zero;
    private float lastPathUpdateTime = 0f;

    // Stuck detection
    private float stuckTimer = 0f;
    private Vector3 stuckCheckPosition;
    private bool isUnstucking = false;

    protected AudioSource footstepAudioSource;
    protected AudioSource effectAudioSource;

    // Cached renderers for show/hide (instead of SetActive)
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;

    void Awake()
    {
        if (spawnPoint == null)
        {
            originalPosition = transform.position;
            originalRotation = transform.rotation;
        }
        else
        {
            originalPosition = spawnPoint.position;
            originalRotation = spawnPoint.rotation;
        }

        agent = GetComponent<NavMeshAgent>();
        agent.speed = patrolSpeed;
        agent.stoppingDistance = attackRange * 0.8f;

        // Keep the Rigidbody kinematic at all times while NavMeshAgent drives movement.
        // A non-kinematic Rigidbody fights NavMesh and accumulates bad state when the
        // player collides with the monster, causing compounding spin/erratic movement.
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.freezeRotation = true;
        }

        animator = GetComponent<Animator>();

        SetupAudio();

        // Cache renderers and colliders for show/hide
        cachedRenderers = GetComponentsInChildren<Renderer>();
        cachedColliders = GetComponentsInChildren<Collider>();
    }

    void SetupAudio()
    {
        footstepAudioSource = gameObject.AddComponent<AudioSource>();
        footstepAudioSource.clip = footstepSound;
        footstepAudioSource.loop = true;
        footstepAudioSource.volume = footstepVolume;
        footstepAudioSource.spatialBlend = 1f;
        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.maxDistance = 20f;

        effectAudioSource = gameObject.AddComponent<AudioSource>();
        effectAudioSource.loop = false;
        effectAudioSource.volume = wakeUpVolume;
        effectAudioSource.spatialBlend = 1f;
        effectAudioSource.playOnAwake = false;
        effectAudioSource.maxDistance = 25f;
    }


    public override void OnNetworkSpawn()
    {
        networkIsActive.OnValueChanged += OnActiveStateChanged;

        // Apply initial state
        if (!IsServer)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
        }

        // Always show renderers so monster is visible from the start.
        // AI movement is gated separately by networkIsActive.
        SetRendererVisibility(true);
        SetColliderState(networkIsActive.Value);
    }

    public override void OnNetworkDespawn()
    {
        networkIsActive.OnValueChanged -= OnActiveStateChanged;
    }

    private void OnActiveStateChanged(bool previousValue, bool newValue)
    {
        SetColliderState(newValue);

        if (newValue)
        {
            if (wakeUpSound != null && effectAudioSource != null)
                effectAudioSource.PlayOneShot(wakeUpSound, wakeUpVolume);
        }
        else
        {
            StopFootsteps();
        }

        if (MinimapManager.Instance != null) MinimapManager.Instance.UpdateMonsterIcon(this, newValue);
    }

    private void SetRendererVisibility(bool visible)
    {
        if (cachedRenderers == null) return;
        foreach (var r in cachedRenderers)
            if (r != null) r.enabled = visible;
    }

    private void SetColliderState(bool active)
    {
        if (cachedColliders != null)
            foreach (var c in cachedColliders)
                if (c != null) c.enabled = active;

        if (noiseIndicator != null) noiseIndicator.SetActive(false);
        if (lightEffectIndicator != null) lightEffectIndicator.SetActive(false);
    }

    // Kept for external callers that expect this method name
    private void SetMonsterVisibility(bool visible)
    {
        SetRendererVisibility(visible);
        SetColliderState(visible);
    }

    // Find the closest alive player from GameManager's list.
    // Active jukeboxes within range override normal targeting (taunt).
    protected virtual Transform FindClosestPlayer()
    {
        if (GameManager.Instance == null) return null;

        // Check for active jukebox taunt — always takes priority
        Transform tauntTarget = FindJukeboxTaunt();
        if (tauntTarget != null) return tauntTarget;

        List<GameObject> players = GameManager.Instance.GetActivePlayers();
        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (var p in players)
        {
            if (p == null) continue;

            // Skip dead players
            var health = p.GetComponent<PlayerHealth>();
            if (health != null && health.GetCurrentHealth() <= 0) continue;

            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = p.transform;
            }
        }

        return closest;
    }

    protected Transform FindJukeboxTaunt()
    {
        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (var jukebox in JukeboxWeapon.ActiveJukeboxes)
        {
            if (jukebox == null || !jukebox.IsActive()) continue;

            Vector3 jukeboxPos = jukebox.GetPlayerPosition();
            float dist = Vector3.Distance(transform.position, jukeboxPos);

            if (dist <= jukebox.GetAggroRadius() && dist < closestDist)
            {
                closestDist = dist;
                closest = jukebox.transform.root;
            }
        }

        return closest;
    }

    protected virtual void Update()
    {
        if (!IsServer) return;

        // Debug toggle: activate monster whenever the checkbox is on and it isn't active yet.
        // Does NOT deactivate — so it never fights a terminal-triggered release.
        if (debugToggleActive && !isActive)
            ActivateMonster();

        if (isAsleep || !isActive || isFrozen)
        {
            StopFootsteps();
            if (agent.enabled) agent.isStopped = true;
            return;
        }

        // Update player target each frame (closest alive player)
        player = FindClosestPlayer();

        // Check for lights to break
        if (Time.time >= lastLightCheckTime + lightCheckInterval)
        {
            CheckForLightsToBreak();
            lastLightCheckTime = Time.time;
        }

        // Check for doors to open
        if (Time.time >= lastDoorCheckTime + doorCheckInterval)
        {
            CheckForDoorsToOpen();
            lastDoorCheckTime = Time.time;
        }

        // Handle flashlight effect
        UpdateFlashlightEffect();

        // Handle Noise Indicator Timer
        if (noiseIndicatorTimer > 0)
        {
            noiseIndicatorTimer -= Time.deltaTime;
            if (noiseIndicator != null) noiseIndicator.SetActive(true);
        }
        else
        {
            if (noiseIndicator != null) noiseIndicator.SetActive(false);
        }

        // If no player target, keep patrolling but skip combat logic
        if (player == null)
        {
            if (useRandomPatrol && !isWaiting && (currentState == AIState.Idle || currentState == AIState.Patrolling))
            {
                Patrol();
            }
            UpdateFootsteps();
            UpdateAnimator();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Check for noise
        CheckForNoise();

        switch (currentState)
        {
            case AIState.Idle:
            case AIState.Patrolling:
                if (DetectPlayer())
                {
                    EnterChaseState();
                }
                else if (useRandomPatrol && !isWaiting)
                {
                    Patrol();
                }
                else
                {
                    StopFootsteps();
                }
                break;

            case AIState.Chasing:
                CheckStuckState();
                ChasePlayer();
                if (distanceToPlayer <= attackRange)
                {
                    EnterAttackState();
                }
                else if (distanceToPlayer > detectionRange * 2f && HasReachedDestination())
                {
                    EnterPatrolState();
                }
                break;

            case AIState.Attacking:
                if (distanceToPlayer > attackRange * 1.2f)
                {
                    EnterChaseState();
                }
                else
                {
                    TryAttack();
                }
                break;
        }

        UpdateFootsteps();
        UpdateAnimator();
    }

    protected virtual bool DetectPlayer()
    {
        // Jukebox taunt overrides normal detection range
        if (FindJukeboxTaunt() != null) return true;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        return distanceToPlayer <= detectionRange;
    }

    protected void EnterPatrolState()
    {
        currentState = AIState.Patrolling;
        agent.speed = patrolSpeed;
        agent.isStopped = false;
    }

    protected void EnterChaseState()
    {
        if (isWaiting)
        {
            StopAllCoroutines();
            isWaiting = false;
        }
        currentState = AIState.Chasing;
        agent.speed = chaseSpeed;
        agent.isStopped = false;
        isUnstucking = false;
        lastKnownPlayerPosition = player.position;
    }

    protected void EnterAttackState()
    {
        currentState = AIState.Attacking;
        agent.isStopped = true;
    }

    protected virtual void CheckForNoise()
    {
        Collider[] noiseSources = Physics.OverlapSphere(transform.position, noiseDetectionRange, noiseSourceLayer);

        if (noiseSources.Length > 0 && currentState != AIState.Chasing && currentState != AIState.Attacking)
        {
            float closestDistance = float.MaxValue;
            Vector3 closestNoisePosition = transform.position;

            foreach (Collider noise in noiseSources)
            {
                float dist = Vector3.Distance(transform.position, noise.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestNoisePosition = noise.transform.position;
                }
            }
            OnNoiseDetected(closestNoisePosition);
        }
    }

    void Patrol()
    {
        if (!agent.hasPath || HasReachedDestination())
        {
            if (!isWaiting) StartCoroutine(WaitAtDestination());
        }
    }

    protected bool HasReachedDestination()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f) return true;
        }
        return false;
    }

    IEnumerator WaitAtDestination()
    {
        isWaiting = true;
        agent.isStopped = true;

        yield return new WaitForSeconds(waitTimeAtDestination);

        Vector3 randomPoint = GetRandomNavMeshPoint(originalPosition, patrolRadius);
        if (randomPoint != Vector3.zero)
        {
            agent.isStopped = false;
            agent.SetDestination(randomPoint);
        }

        isWaiting = false;
    }

    protected Vector3 GetRandomNavMeshPoint(Vector3 center, float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection.y = 0f;
        randomDirection += center;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return Vector3.zero;
    }

    void ChasePlayer()
    {
        Vector3 target = player != null ? player.position : lastKnownPlayerPosition;

        if (player != null)
            lastKnownPlayerPosition = player.position;

        // Already close enough — no need to keep pathing
        if (Vector3.Distance(transform.position, target) <= agent.stoppingDistance + 0.1f)
            return;

        bool targetMoved = Vector3.Distance(target, lastSetDestination) > 0.5f;

        // When the path is partial (stuck against geometry), rate-limit recalculation to avoid
        // rapid micro-lurches in random directions as each new partial path resolves differently.
        bool pathIsClean = agent.pathStatus == NavMeshPathStatus.PathComplete;
        float retryInterval = pathIsClean ? 0f : 1.5f;
        bool allowUpdate = Time.time >= lastPathUpdateTime + retryInterval;

        if (targetMoved && allowUpdate)
        {
            // Flatten Y so we don't send the agent up/down toward the player's exact Y
            Vector3 flatTarget = new Vector3(target.x, transform.position.y, target.z);
            agent.SetDestination(flatTarget);
            lastSetDestination = target;
            lastPathUpdateTime = Time.time;
        }
    }

    protected void UpdateFootsteps()
    {
        if (agent.velocity.magnitude > 0.1f && currentState != AIState.Attacking) PlayFootsteps();
        else StopFootsteps();
    }

    protected void UpdateAnimator()
    {
        if (animator == null) return;
        // agent.velocity lags by one physics step after SetDestination, causing idle-gliding
        // at chase start. desiredVelocity updates immediately when a path is assigned.
        bool shouldRun = agent.velocity.magnitude > 0.1f || agent.desiredVelocity.magnitude > 0.1f;
        animator.SetBool("Run", shouldRun);
    }

    void PlayFootsteps()
    {
        if (footstepSound != null && !footstepAudioSource.isPlaying) footstepAudioSource.Play();
    }

    void StopFootsteps()
    {
        if (footstepAudioSource != null && footstepAudioSource.isPlaying) footstepAudioSource.Stop();
    }

    void TryAttack()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            Attack();
            lastAttackTime = Time.time;
        }
    }

    void Attack()
    {
        Debug.Log("Monster attacks player");
        StopFootsteps();

        if (animator != null) animator.SetTrigger("Attack");

        if (player == null) return;

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamageServerRpc(attackDamage);
        }
    }

    // Detects when the monster is stuck and navigates to a flanking position.
    // On recovery, eases speed back up to avoid the launch-from-stop jerk.
    void CheckStuckState()
    {
        stuckTimer += Time.deltaTime;
        if (stuckTimer < 1.2f) return;

        float moved = Vector3.Distance(transform.position, stuckCheckPosition);
        stuckCheckPosition = transform.position;
        stuckTimer = 0f;

        if (isUnstucking)
        {
            if (moved >= 0.4f)
            {
                // Moving freely again — restore full chase speed
                agent.speed = chaseSpeed;
                isUnstucking = false;
            }
            else if (player != null)
            {
                // Still stuck after the flank attempt — try another position
                TryFlankPlayer();
            }
            return;
        }

        if (moved < 0.4f && player != null)
            TryFlankPlayer();
    }

    void TryFlankPlayer()
    {
        Vector3 flankPos = FindReachablePositionNearTarget(player.position);
        if (flankPos == Vector3.zero) return;

        agent.velocity = Vector3.zero; // clear residual momentum from fighting geometry
        agent.speed = patrolSpeed;     // ease out at walk speed instead of launching
        isUnstucking = true;
        agent.SetDestination(flankPos);
        lastSetDestination = player.position;
        lastPathUpdateTime = Time.time;
    }

    // Finds the nearest NavMesh position around targetPos that this agent can reach
    // via a complete (not partial) path. Used to route around blocking geometry.
    Vector3 FindReachablePositionNearTarget(Vector3 targetPos)
    {
        NavMeshPath path = new NavMeshPath();
        float[] radii = { 1.5f, 2.5f, 3.5f, 5f };

        foreach (float radius in radii)
        {
            Vector3 bestPos = Vector3.zero;
            float bestDist = float.MaxValue;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                Vector3 candidate = targetPos + dir * radius;

                NavMeshHit hit;
                if (!NavMesh.SamplePosition(candidate, out hit, 1.5f, NavMesh.AllAreas)) continue;

                path.ClearCorners();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    float dist = Vector3.Distance(hit.position, targetPos);
                    if (dist < bestDist) { bestDist = dist; bestPos = hit.position; }
                }
            }

            if (bestPos != Vector3.zero) return bestPos;
        }

        return Vector3.zero;
    }

    public void Sleep(float duration)
    {
        if (!IsServer) return;
        if (!isAsleep && isActive) StartCoroutine(SleepCoroutine(duration));
    }

    IEnumerator SleepCoroutine(float duration)
    {
        isAsleep = true;
        currentState = AIState.Idle;

        agent.ResetPath();
        agent.isStopped = true;
        StopFootsteps();

        // Kinematic prevents Rigidbody from rolling while stopped
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        if (noiseIndicator != null) noiseIndicator.SetActive(false);
        if (lightEffectIndicator != null) lightEffectIndicator.SetActive(false);

        if (animator != null) animator.SetTrigger("Sleep");
        yield return new WaitForSeconds(duration);

        if (wakeUpSound != null) effectAudioSource.PlayOneShot(wakeUpSound, wakeUpVolume);
        if (animator != null) animator.SetTrigger("WakeUp");
        yield return new WaitForSeconds(getUpDuration);

        isAsleep = false;
        agent.isStopped = false;
        EnterPatrolState();
    }

    public void Freeze(float duration)
    {
        if (!IsServer) return;
        if (!isActive || isFrozen) return;
        StartCoroutine(FreezeCoroutine(duration));
    }

    protected bool isFrozen = false;

    IEnumerator FreezeCoroutine(float duration)
    {
        isFrozen = true;
        currentState = AIState.Idle;
        if (agent != null)
        {
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }
        StopFootsteps();

        // Freeze animator on all clients
        FreezeAnimatorClientRpc(true);

        yield return new WaitForSeconds(duration);

        isFrozen = false;
        if (agent != null) agent.isStopped = false;
        EnterPatrolState();

        // Unfreeze animator on all clients
        FreezeAnimatorClientRpc(false);
    }

    [ClientRpc]
    private void FreezeAnimatorClientRpc(bool frozen)
    {
        if (animator != null)
            animator.speed = frozen ? 0f : 1f;
    }

    public void ActivateMonster()
    {
        if (!IsServer) return;

        networkIsActive.Value = true;

        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }

        EnterPatrolState();
    }

    public void DeactivateMonster()
    {
        if (!IsServer) return;

        if (isWaiting)
        {
            StopAllCoroutines();
            isWaiting = false;
        }

        networkIsActive.Value = false;
        isAsleep = false;
        currentState = AIState.Idle;

        if (agent != null) agent.isStopped = true;

        StopFootsteps();
        if (noiseIndicator != null) noiseIndicator.SetActive(false);
        if (lightEffectIndicator != null) lightEffectIndicator.SetActive(false);
    }

    public void ResetMonster()
    {
        if (!IsServer) return;

        StopAllCoroutines();
        isAsleep = false;
        lastAttackTime = 0;
        isWaiting = false;
        currentState = AIState.Idle;

        if (agent != null)
        {
            agent.Warp(originalPosition);
            agent.isStopped = true;
        }

        transform.position = originalPosition;
        transform.rotation = originalRotation;

        StopFootsteps();
        if (noiseIndicator != null) noiseIndicator.SetActive(false);
        if (lightEffectIndicator != null) lightEffectIndicator.SetActive(false);
    }

    void CheckForLightsToBreak()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, lightBreakRadius);
        foreach (Collider col in nearbyColliders)
        {
            RoomLight light = col.GetComponent<RoomLight>();
            if (light != null && light.isOn && !light.isBroken && light.canBeBroken)
            {
                light.BreakLight();
            }
        }
    }

    void CheckForDoorsToOpen()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, doorCheckRadius);
        foreach (Collider col in nearbyColliders)
        {
            Door door = col.GetComponent<Door>();
            if (door == null) door = col.GetComponentInParent<Door>();
            if (door != null && !door.isOpen && !door.isCellDoor)
            {
                door.OpenDoor();
            }
        }
    }

    void UpdateFlashlightEffect()
    {
        if (!affectedByFlashlight) return;

        if (lightEffectIndicator != null)
        {
            lightEffectIndicator.SetActive(isFlashlightShining);
        }

        if (flashlightEffectTimer > 0)
        {
            flashlightEffectTimer -= Time.deltaTime;

            if (flashlightEffectTimer <= 0)
            {
                isFlashlightShining = false;
                RestoreNormalSpeed();
            }
        }
    }

    public virtual void OnFlashlightShone()
    {
        if (!affectedByFlashlight || !isActive) return;

        isFlashlightShining = true;
        flashlightEffectTimer = flashlightEffectDuration;

        if (agent != null)
        {
            float currentSpeed = (currentState == AIState.Chasing) ? chaseSpeed : patrolSpeed;
            agent.speed = currentSpeed * flashlightSlowMultiplier;
        }
    }

    void RestoreNormalSpeed()
    {
        if (agent != null)
        {
            agent.speed = (currentState == AIState.Chasing) ? chaseSpeed : patrolSpeed;
        }
    }

    public void OnNoiseDetected(Vector3 noisePosition)
    {
        if (!IsServer) return;

        if (!isAsleep && isActive && currentState != AIState.Chasing && currentState != AIState.Attacking)
        {
            lastKnownPlayerPosition = noisePosition;
            agent.SetDestination(noisePosition);
            currentState = AIState.Chasing;
            agent.speed = chaseSpeed;
            Debug.Log($"Monster investigating noise at {noisePosition}");

            noiseIndicatorTimer = 0.2f;
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, noiseDetectionRange);

        Vector3 center = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, patrolRadius);
    }
}
