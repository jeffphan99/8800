using UnityEngine;

public class MonsterStalker : MonsterAI
{
    [Header("Stalker-Specific Settings")]
    public float visionRange = 30f;
    public float visionAngle = 60f;
    public float enrageSpeedMultiplier = 2f;
    public float enrageDuration = 3f;
    public LayerMask visionBlockingLayers;

    [Header("Light Sensitivity")]
    public float lightCheckRadius = 5f;
    public bool isEnraged = false;
    private float enrageTimer = 0f;

    protected override void Update()
    {
        base.Update();

        if (isEnraged)
        {
            enrageTimer -= Time.deltaTime;
            if (enrageTimer <= 0f)
            {
                CalmDown();
            }
        }

        CheckForBrightLights();
    }

    protected override bool DetectPlayer()
    {
        if (player == null) return false;

        Vector3 directionToPlayer = player.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer > visionRange)
            return false;

        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
        if (angleToPlayer > visionAngle / 2f)
            return false;

        Ray ray = new Ray(transform.position + Vector3.up, directionToPlayer);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, distanceToPlayer, visionBlockingLayers))
        {
            return false;
        }

        Debug.Log("Stalker: Player spotted!");
        return true;
    }

    void CheckForBrightLights()
    {
        RoomLight[] nearbyLights = FindObjectsOfType<RoomLight>();

        foreach (RoomLight light in nearbyLights)
        {
            if (light.isOn && !light.isBroken)
            {
                float distance = Vector3.Distance(transform.position, light.transform.position);

                if (distance <= lightCheckRadius)
                {
                    if (!isEnraged)
                    {
                        Enrage();
                    }
                    return;
                }
            }
        }
    }

    void Enrage()
    {
        isEnraged = true;
        enrageTimer = enrageDuration;

        if (agent != null)
        {
            agent.speed = chaseSpeed * enrageSpeedMultiplier;
        }

        Debug.Log("Stalker: ENRAGED by bright light!");
    }

    void CalmDown()
    {
        isEnraged = false;

        if (agent != null)
        {
            agent.speed = currentState == AIState.Chasing ? chaseSpeed : patrolSpeed;
        }

        Debug.Log("Stalker: Calmed down");
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.cyan;
        Vector3 forward = transform.forward * visionRange;
        Vector3 leftBoundary = Quaternion.Euler(0, -visionAngle / 2f, 0) * forward;
        Vector3 rightBoundary = Quaternion.Euler(0, visionAngle / 2f, 0) * forward;

        Gizmos.DrawRay(transform.position, leftBoundary);
        Gizmos.DrawRay(transform.position, rightBoundary);
        Gizmos.DrawRay(transform.position, forward);
    }
}