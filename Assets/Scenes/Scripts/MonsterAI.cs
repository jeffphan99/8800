using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviour
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

    // CHANGED: We use a small buffer time instead of a long duration
    private float noiseIndicatorTimer = 0f;

    [Header("Patrol Settings")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float patrolRadius = 60f;
    public float waitTimeAtDestination = 2f;
    public bool useRandomPatrol = true;

    [Header("Noise Detection")]
    public float noiseDetectionRange = 15f;
    public LayerMask noiseSourceLayer;

    [Header("Sleep Settings")]
    public float sleepLayDownSpeed = 2f;

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

    [Header("Flashlight Interaction")]
    public bool affectedByFlashlight = false;
    public float flashlightSlowMultiplier = 0.3f;
    public float flashlightEffectDuration = 0.5f;
    protected float flashlightEffectTimer = 0f;
    protected bool isFlashlightShining = false;

    protected Transform player;
    protected float lastAttackTime;
    protected bool isAsleep = false;
    public bool isActive { get; protected set; }
    protected Vector3 originalPosition;
    protected Quaternion originalRotation;
    protected Animator animator;
    protected NavMeshAgent agent;

    protected enum AIState { Idle, Patrolling, Chasing, Attacking }
    protected AIState currentState = AIState.Idle;
    protected bool isWaiting = false;
    protected Vector3 lastKnownPlayerPosition;

    protected AudioSource footstepAudioSource;
    protected AudioSource effectAudioSource;

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

        SetupAudio();
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

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = patrolSpeed;
        agent.stoppingDistance = attackRange * 0.8f;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        if (spawnPoint == null) spawnPoint = transform;
        originalPosition = spawnPoint.position;
        originalRotation = spawnPoint.rotation;

        animator = GetComponent<Animator>();

        if (noiseIndicator != null) noiseIndicator.SetActive(false);
        if (lightEffectIndicator != null) lightEffectIndicator.SetActive(false);

        DeactivateMonster();
    }

    // --- FIXED UPDATE METHOD ---
    protected virtual void Update()
    {
        // Debug Toggle Logic (Fixed syntax)
        if (debugToggleActive != isActive)
        {
            if (debugToggleActive) ActivateMonster();
            else DeactivateMonster();
        }

        if (isAsleep || !isActive)
        {
            StopFootsteps();
            if (agent.enabled) agent.isStopped = true;
            return;
        }

        if (player == null) return;

        // Check for lights to break
        if (Time.time >= lastLightCheckTime + lightCheckInterval)
        {
            CheckForLightsToBreak();
            lastLightCheckTime = Time.time;
        }

        // Handle flashlight effect
        UpdateFlashlightEffect();

        // --- NEW: Handle Noise Indicator Timer ---
        if (noiseIndicatorTimer > 0)
        {
            noiseIndicatorTimer -= Time.deltaTime;
            if (noiseIndicator != null) noiseIndicator.SetActive(true);
        }
        else
        {
            if (noiseIndicator != null) noiseIndicator.SetActive(false);
        }
        // ----------------------------------------

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
                ChasePlayer();
                if (distanceToPlayer <= attackRange)
                {
                    EnterAttackState();
                }
                else if (distanceToPlayer > detectionRange * 2f && !HasReachedDestination())
                {
                    // Keep chasing
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
        currentState = AIState.Chasing;
        agent.speed = chaseSpeed;
        agent.isStopped = false;
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
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f) return true;
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
        if (player != null)
        {
            lastKnownPlayerPosition = player.position;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.SetDestination(lastKnownPlayerPosition);
        }
    }

    protected void UpdateFootsteps()
    {
        if (agent.velocity.magnitude > 0.1f && currentState != AIState.Attacking) PlayFootsteps();
        else StopFootsteps();
    }

    protected void UpdateAnimator()
    {
        if (animator != null) animator.SetBool("Run", agent.velocity.magnitude > 0.1f);
    }

    void PlayFootsteps()
    {
        if (footstepSound != null && !footstepAudioSource.isPlaying) footstepAudioSource.Play();
    }

    void StopFootsteps()
    {
        if (footstepAudioSource.isPlaying) footstepAudioSource.Stop();
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

        if (player != null)
        {
            Vector3 lookPos = player.position - transform.position;
            lookPos.y = 0;
            transform.rotation = Quaternion.LookRotation(lookPos);
        }

        if (animator != null) animator.SetTrigger("Attack");

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null) playerHealth.TakeDamage(attackDamage);
        else if (GameManager.Instance != null) GameManager.Instance.OnPlayerDeath();
    }

    public void Sleep(float duration)
    {
        if (!isAsleep && isActive) StartCoroutine(SleepCoroutine(duration));
    }

    IEnumerator SleepCoroutine(float duration)
    {
        isAsleep = true;
        currentState = AIState.Idle;
        agent.isStopped = true;
        StopFootsteps();

        if (animator != null) animator.SetTrigger("Sleep");

        Quaternion sleepRotation = Quaternion.Euler(90f, transform.eulerAngles.y, transform.eulerAngles.z);
        float elapsed = 0f;

        while (elapsed < sleepLayDownSpeed)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, sleepRotation, elapsed / sleepLayDownSpeed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = sleepRotation;

        if (noiseIndicator != null) noiseIndicator.SetActive(false);
        if (lightEffectIndicator != null) lightEffectIndicator.SetActive(false);

        yield return new WaitForSeconds(duration);

        if (wakeUpSound != null) effectAudioSource.PlayOneShot(wakeUpSound, wakeUpVolume);

        elapsed = 0f;
        while (elapsed < sleepLayDownSpeed)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, transform.eulerAngles.y, 0), elapsed / sleepLayDownSpeed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        isAsleep = false;
        agent.isStopped = false;
        EnterPatrolState();

        if (animator != null) animator.SetTrigger("WakeUp");
    }

    public void ActivateMonster()
    {
        isActive = true;
        gameObject.SetActive(true);

        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }

        if (wakeUpSound != null && effectAudioSource != null)
        {
            effectAudioSource.PlayOneShot(wakeUpSound, wakeUpVolume);
        }

        EnterPatrolState();
        if (MinimapManager.Instance != null) MinimapManager.Instance.UpdateMonsterIcon(this, true);
    }

    public void DeactivateMonster()
    {
        isActive = false;
        isAsleep = false;
        currentState = AIState.Idle;

        if (agent != null) agent.isStopped = true;

        StopFootsteps();
        if (noiseIndicator != null) noiseIndicator.SetActive(false);
        if (lightEffectIndicator != null) lightEffectIndicator.SetActive(false);

        if (MinimapManager.Instance != null) MinimapManager.Instance.UpdateMonsterIcon(this, false);
    }

    public void ResetMonster()
    {
        transform.position = originalPosition;
        transform.rotation = originalRotation;

        isAsleep = false;
        lastAttackTime = 0;
        isWaiting = false;
        currentState = AIState.Idle;

        if (agent != null)
        {
            agent.Warp(originalPosition);
            agent.isStopped = true;
        }

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