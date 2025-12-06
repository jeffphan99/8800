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

    [Header("Audio")]
    [Tooltip("Footstep audio clip")]
    public AudioClip footstepSound;
    [Tooltip("Sound played when monster wakes up")]
    public AudioClip wakeUpSound;
    [Tooltip("Volume of footstep sounds")]
    [Range(0f, 1f)]
    public float footstepVolume = 0.5f;
    [Tooltip("Volume of wake up sound")]
    [Range(0f, 1f)]
    public float wakeUpVolume = 0.8f;

    private Transform player;
    private float lastAttackTime;
    private bool isAsleep = false;
    public bool isActive { get; private set; }
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Animator animator;
    private NavMeshAgent agent;

    private enum AIState { Idle, Patrolling, Chasing, Attacking }
    private AIState currentState = AIState.Idle;
    private bool isWaiting = false;
    private Vector3 lastKnownPlayerPosition;

    private AudioSource footstepAudioSource;
    private AudioSource effectAudioSource;

    void Awake()
    {
        if (spawnPoint == null)
        {
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            Debug.LogWarning($"[Monster] {gameObject.name} has no spawn point assigned");
        }
        else
        {
            originalPosition = spawnPoint.position;
            originalRotation = spawnPoint.rotation;
            Debug.Log($"[Monster] {gameObject.name} spawn point set at position: {originalPosition}");
        }

        SetupAudio();
    }

    void SetupAudio()
    {
        footstepAudioSource = gameObject.AddComponent<AudioSource>();
        footstepAudioSource.clip = footstepSound;
        footstepAudioSource.loop = true;
        footstepAudioSource.volume = footstepVolume;
        footstepAudioSource.spatialBlend = 1f; // 3D sound
        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.maxDistance = 20f;

        effectAudioSource = gameObject.AddComponent<AudioSource>();
        effectAudioSource.loop = false;
        effectAudioSource.volume = wakeUpVolume;
        effectAudioSource.spatialBlend = 1f; // 3D sound
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

        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }

        originalPosition = spawnPoint.position;
        originalRotation = spawnPoint.rotation;

        animator = GetComponent<Animator>();

        DeactivateMonster();
    }

    void Update()
    {

        if (isAsleep || !isActive)
        {
            StopFootsteps();
            if (agent.enabled) agent.isStopped = true;
            return;
        }

        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        CheckForNoise();

        switch (currentState)
        {
            case AIState.Idle:
            case AIState.Patrolling:
                if (distanceToPlayer <= detectionRange)
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

        if (agent.velocity.magnitude > 0.1f && currentState != AIState.Attacking)
        {
            PlayFootsteps();
        }
        else
        {
            StopFootsteps();
        }

        if (animator != null)
        {
            animator.SetBool("Run", agent.velocity.magnitude > 0.1f);
        }
    }

    void EnterPatrolState()
    {
        currentState = AIState.Patrolling;
        agent.speed = patrolSpeed;
        agent.isStopped = false;
    }

    void EnterChaseState()
    {
        currentState = AIState.Chasing;
        agent.speed = chaseSpeed;
        agent.isStopped = false;
        lastKnownPlayerPosition = player.position;
    }

    void EnterAttackState()
    {
        currentState = AIState.Attacking;
        agent.isStopped = true;
    }

    void CheckForNoise()
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
            if (!isWaiting)
            {
                StartCoroutine(WaitAtDestination());
            }
        }
    }

    bool HasReachedDestination()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                return true;
            }
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

    Vector3 GetRandomNavMeshPoint(Vector3 center, float radius)
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

    void PlayFootsteps()
    {
        if (footstepSound != null && !footstepAudioSource.isPlaying)
        {
            footstepAudioSource.Play();
        }
    }

    void StopFootsteps()
    {
        if (footstepAudioSource.isPlaying)
        {
            footstepAudioSource.Stop();
        }
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

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
        }
        else
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerDeath();
            }
        }
    }

    // CALLED BY GUN
    public void Sleep(float duration)
    {
        if (!isAsleep && isActive)
        {
            StartCoroutine(SleepCoroutine(duration));
        }
    }

    IEnumerator SleepCoroutine(float duration)
    {
        Debug.Log("Monster is going to sleep");
        isAsleep = true;
        currentState = AIState.Idle;

        agent.isStopped = true;
        StopFootsteps();

        if (animator != null)
        {
            animator.SetTrigger("Sleep");
        }

        // Lay down
        Quaternion sleepRotation = Quaternion.Euler(90f, transform.eulerAngles.y, transform.eulerAngles.z);
        float elapsed = 0f;

        while (elapsed < sleepLayDownSpeed)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, sleepRotation, elapsed / sleepLayDownSpeed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = sleepRotation;
        Debug.Log("Monster is asleep");

        yield return new WaitForSeconds(duration);

        Debug.Log("Monster is waking up");

        // Play wake up sound
        if (wakeUpSound != null)
        {
            effectAudioSource.PlayOneShot(wakeUpSound, wakeUpVolume);
        }

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
        Debug.Log("Monster is awake!");

        if (animator != null)
        {
            animator.SetTrigger("WakeUp");
        }
    }

    // CALLED BY GAME MANAGER
    public void ActivateMonster()
    {
        isActive = true;
        gameObject.SetActive(true);

        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }
        effectAudioSource.PlayOneShot(wakeUpSound, wakeUpVolume);
        EnterPatrolState();
        Debug.Log($"Monster has been activated!");
    }

    public void DeactivateMonster()
    {
        isActive = false;
        isAsleep = false;
        currentState = AIState.Idle;

        if (agent != null)
        {
            agent.isStopped = true;
        }

        StopFootsteps();
        Debug.Log($"Monster has been deactivated");
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

        Debug.Log($"Monster has been reset");
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
        }
    }

    void OnDrawGizmosSelected()
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