using UnityEngine;

public class MonsterWatcher : MonsterAI
{
    [Header("Watcher-Specific Settings")]
    public float proximityAggroRadius = 3f;
    public float unwatchedSpeedMultiplier = 2f;
    public float watchedSpeedMultiplier = 0.3f;

    [Header("Flashlight Interaction")]
    public bool isBeingWatched = false;
    private float lastFlashlightCheckTime = 0f;
    public float flashlightCheckInterval = 0.1f;

    protected override void Update()
    {
        if (Time.time >= lastFlashlightCheckTime + flashlightCheckInterval)
        {
            CheckIfBeingWatched();
            lastFlashlightCheckTime = Time.time;
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

        if (isBeingWatched)
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
        base.OnFlashlightShone();
        isBeingWatched = true;
        lastFlashlightCheckTime = Time.time;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = isBeingWatched ? Color.blue : Color.magenta;
        Gizmos.DrawWireSphere(transform.position, proximityAggroRadius);
    }
}