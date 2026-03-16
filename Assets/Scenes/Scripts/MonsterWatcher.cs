using UnityEngine;

public class MonsterWatcher : MonsterAI
{
    [Header("Watcher-Specific Settings")]
    public float proximityAggroRadius = 3f;
    public float unwatchedSpeedMultiplier = 2f;
    public float watchedSpeedMultiplier = 0.3f;

    [Header("Flashlight Suppression")]
    public float suppressionDuration = 0.3f;
    public float suppressionSpeedMultiplier = 0.05f;
    private float suppressionTimer = 0f;
    private bool isFlashlightSuppressed = false;

    [Header("Watch Detection")]
    public bool isBeingWatched = false;
    private float lastWatchCheckTime = 0f;
    public float watchCheckInterval = 0.1f;

    protected override void Update()
    {
        // Only server runs AI logic
        if (!IsServer) return;

        // Tick suppression timer before base.Update
        if (isFlashlightSuppressed)
        {
            suppressionTimer -= Time.deltaTime;
            if (suppressionTimer <= 0f)
            {
                isFlashlightSuppressed = false;
            }
        }

        if (Time.time >= lastWatchCheckTime + watchCheckInterval)
        {
            CheckIfBeingWatched();
            lastWatchCheckTime = Time.time;
        }

        UpdateSpeedBasedOnObservation();

        base.Update();
    }

    protected override bool DetectPlayer()
    {
        if (player == null) return false;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= proximityAggroRadius)
        {
            Debug.Log("Watcher: Player too close!");
            return true;
        }

        return false;
    }

    void CheckIfBeingWatched()
    {
        if (player == null) return;

        Camera playerCamera = Camera.main;
        if (playerCamera == null) return;

        Vector3 directionToMonster = transform.position - playerCamera.transform.position;
        float angleToMonster = Vector3.Angle(playerCamera.transform.forward, directionToMonster);

        if (angleToMonster < 30f)
        {
            Ray ray = new Ray(playerCamera.transform.position, directionToMonster);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                MonsterWatcher hitWatcher = hit.collider.GetComponentInParent<MonsterWatcher>();
                if (hitWatcher == this)
                {
                    isBeingWatched = true;
                    return;
                }
            }
        }

        isBeingWatched = false;
    }

    void UpdateSpeedBasedOnObservation()
    {
        if (agent == null) return;

        float baseSpeed = currentState == AIState.Chasing ? chaseSpeed : patrolSpeed;

        // Priority: flashlight suppressed > being watched > unwatched
        if (isFlashlightSuppressed)
        {
            agent.speed = baseSpeed * suppressionSpeedMultiplier;
        }
        else if (isBeingWatched)
        {
            agent.speed = baseSpeed * watchedSpeedMultiplier;
        }
        else
        {
            agent.speed = baseSpeed * unwatchedSpeedMultiplier;
        }
    }

    public override void OnFlashlightShone()
    {
        if (!isActive) return;

        isFlashlightSuppressed = true;
        suppressionTimer = suppressionDuration;
        isBeingWatched = true;
        lastWatchCheckTime = Time.time;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = isBeingWatched ? Color.blue : Color.magenta;
        Gizmos.DrawWireSphere(transform.position, proximityAggroRadius);
    }
}
