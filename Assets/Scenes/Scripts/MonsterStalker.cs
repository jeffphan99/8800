using System.Collections.Generic;
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
    public float brightLightCheckRadius = 5f;
    public float brightLightCheckInterval = 0.3f;
    private float lastBrightLightCheckTime = 0f;
    public bool isEnraged = false;
    private float enrageTimer = 0f;

    [Header("Jukebox Calming")]
    public float jukeboxCalmRadius = 40f;
    public float jukeboxCalmSpeedMultiplier = 0.3f;
    private bool isCalmByMusic = false;

    [Header("Lit Zone Detection")]
    public float litZoneDetectionRadius = 5f;

    protected override void Update()
    {
        base.Update();

        // Only server runs AI logic
        if (!IsServer) return;

        if (isEnraged)
        {
            enrageTimer -= Time.deltaTime;
            if (enrageTimer <= 0f)
            {
                CalmDown();
            }
        }

        if (Time.time >= lastBrightLightCheckTime + brightLightCheckInterval)
        {
            CheckForBrightLights();
            lastBrightLightCheckTime = Time.time;
        }

        CheckJukeboxCalming();

        // Base class EnterPatrolState/EnterChaseState reset agent.speed directly,
        // overwriting enrage/calm multipliers. Reapply every frame to keep modifiers active.
        if (isActive && !isFrozen)
            ApplySpeedModifiers();
    }

    protected override Transform FindClosestPlayer()
    {
        // Stalker ignores jukebox taunt — calmed by music, not lured
        if (GameManager.Instance == null) return null;

        List<GameObject> players = GameManager.Instance.GetActivePlayers();
        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (var p in players)
        {
            if (p == null) continue;

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

    protected override bool DetectPlayer()
    {
        if (player == null) return false;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Vision cone detection (existing behavior)
        if (distanceToPlayer <= visionRange)
        {
            Vector3 directionToPlayer = player.position - transform.position;
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

            if (angleToPlayer <= visionAngle / 2f)
            {
                Ray ray = new Ray(transform.position + Vector3.up, directionToPlayer);
                RaycastHit hit;

                if (!Physics.Raycast(ray, out hit, distanceToPlayer, visionBlockingLayers))
                {
                    Debug.Log("Stalker: Player spotted in vision cone!");
                    return true;
                }
            }
        }

        // Lit zone detection — player near a lit light is visible without cone check
        if (distanceToPlayer <= visionRange)
        {
            Collider[] nearPlayer = Physics.OverlapSphere(player.position, litZoneDetectionRadius);
            foreach (Collider col in nearPlayer)
            {
                RoomLight light = col.GetComponent<RoomLight>();
                if (light != null && light.isOn && !light.isBroken)
                {
                    // Player is in a lit zone — check LOS
                    Vector3 directionToPlayer = player.position - transform.position;
                    Ray ray = new Ray(transform.position + Vector3.up, directionToPlayer);
                    RaycastHit hit;

                    if (!Physics.Raycast(ray, out hit, distanceToPlayer, visionBlockingLayers))
                    {
                        Debug.Log("Stalker: Player spotted in lit zone!");
                        return true;
                    }
                    break;
                }
            }
        }

        return false;
    }

    public override void OnFlashlightShone()
    {
        if (!isActive) return;

        if (player != null)
        {
            lastKnownPlayerPosition = player.position;
            EnterChaseState();
            if (!isEnraged) Enrage();
        }
    }

    void CheckForBrightLights()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, brightLightCheckRadius);
        foreach (Collider col in nearbyColliders)
        {
            RoomLight light = col.GetComponent<RoomLight>();
            if (light != null && light.isOn && !light.isBroken)
            {
                if (!isEnraged)
                {
                    Enrage();
                }
                return;
            }
        }
    }

    void CheckJukeboxCalming()
    {
        bool nearJukebox = false;

        foreach (var jukebox in JukeboxWeapon.ActiveJukeboxes)
        {
            if (jukebox == null || !jukebox.IsActive()) continue;

            float dist = Vector3.Distance(transform.position, jukebox.transform.position);
            if (dist <= jukeboxCalmRadius)
            {
                nearJukebox = true;
                break;
            }
        }

        if (nearJukebox && !isCalmByMusic)
        {
            isCalmByMusic = true;
            ApplySpeedModifiers();
        }
        else if (!nearJukebox && isCalmByMusic)
        {
            isCalmByMusic = false;
            ApplySpeedModifiers();
        }
    }

    void Enrage()
    {
        isEnraged = true;
        enrageTimer = enrageDuration;
        ApplySpeedModifiers();
        Debug.Log("Stalker: ENRAGED by bright light!");
    }

    void CalmDown()
    {
        isEnraged = false;
        ApplySpeedModifiers();
        Debug.Log("Stalker: Calmed down from enrage");
    }

    void ApplySpeedModifiers()
    {
        if (agent == null) return;

        float baseSpeed = currentState == AIState.Chasing ? chaseSpeed : patrolSpeed;

        if (isCalmByMusic)
        {
            // Jukebox calming overrides enrage speed but enrage timer still ticks
            agent.speed = baseSpeed * jukeboxCalmSpeedMultiplier;
        }
        else if (isEnraged)
        {
            agent.speed = chaseSpeed * enrageSpeedMultiplier;
        }
        else
        {
            agent.speed = baseSpeed;
        }
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

        // Lit zone radius (shown at current position for reference)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, litZoneDetectionRadius);
    }
}
