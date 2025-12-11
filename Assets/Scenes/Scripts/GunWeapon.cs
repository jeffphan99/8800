using System.Collections;
using UnityEngine;
using StarterAssets;

public class FreezeGunWeapon : WeaponBase
{
    [Header("Freeze Gun Settings")]
    public float shootRange = 50f;
    public float freezeDuration = 5f;
    public float fireRate = 0.5f;
    public int maxAmmo = 10;
    public float reloadTime = 2f;

    [Header("Ice Block Settings")]
    public GameObject iceBlockPrefab; // Assign a cube prefab in Inspector
    public Vector3 iceBlockSize = new Vector3(3f, 4f, 3f); // Size of ice block
    public Vector3 iceBlockOffset = new Vector3(0f, 2f, 0f); // Offset from monster position
    [Tooltip("Ice color - adjust the Alpha slider for transparency (0=invisible, 1=solid)")]
    public Color iceColor = new Color(0.5f, 0.8f, 1f, 0.5f); 
    public float thawStartTime = 1f; 
    public float shakeIntensity = 0.1f;

    [Header("Raycast Settings")]
    public LayerMask shootableLayers;

    [Header("Visual Effects")]
    public LineRenderer lineRenderer;
    public float lineDuration = 0.1f;
    public ParticleSystem freezeImpactEffect; 
    public ParticleSystem muzzleFlashEffect;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip freezeShootSound;
    public AudioClip freezeHitSound;
    public AudioClip iceShatterSound;
    public AudioClip emptySound;
    public AudioClip reloadSound;

    private float nextFireTime = 0f;
    private int currentAmmo;
    private bool isReloading = false;

    void Start()
    {
        currentAmmo = maxAmmo;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
            lineRenderer.startWidth = 0.05f;
            lineRenderer.endWidth = 0.05f;
            lineRenderer.material.color = new Color(0.5f, 0.8f, 1f, 1f);
        }

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
        if (_input == null) return;

        if (!gameObject.activeInHierarchy) return;

        // Don't shoot if minigame active
        Terminal activeTerminal = FindObjectOfType<Terminal>();
        if (activeTerminal != null && activeTerminal.minigameActive)
            return;

        if (isReloading)
            return;

        if (_input.fire && Time.time >= nextFireTime)
        {
            PrimaryAction();
            nextFireTime = Time.time + fireRate;
            _input.fire = false;
        }
    }

    public override void PrimaryAction()
    {
        if (currentAmmo <= 0)
        {
            if (audioSource != null && emptySound != null)
            {
                audioSource.PlayOneShot(emptySound);
            }

            if (statusText != null)
            {
                StartCoroutine(FlashStatusText());
            }

            Debug.Log("Out of ammo! Press R to reload");
            return;
        }

        currentAmmo--;
        UpdateStatusUI();
        Debug.Log($"Freeze shot fired! Ammo: {currentAmmo}/{maxAmmo}");

        // Play shoot sound
        if (audioSource != null && freezeShootSound != null)
        {
            audioSource.PlayOneShot(freezeShootSound, 0.7f);
        }

        // Play muzzle flash
        if (muzzleFlashEffect != null)
        {
            muzzleFlashEffect.Play();
        }

        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * shootRange, Color.cyan, 2f);

        Debug.Log($"[FreezeGun] Raycast from {ray.origin} in direction {ray.direction}");
        Debug.Log($"[FreezeGun] Shootable Layers mask: {shootableLayers.value}");

        if (Physics.Raycast(ray, out hit, shootRange))
        {
            Debug.Log($"[FreezeGun] Hit SOMETHING (no layer filter): {hit.collider.gameObject.name}, Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
        }
        else
        {
            Debug.Log($"[FreezeGun] Hit NOTHING even without layer filter!");
        }

        if (Physics.Raycast(ray, out hit, shootRange, shootableLayers))
        {
            Debug.Log($">>> FREEZE HIT: {hit.collider.gameObject.name}");
            Debug.Log($">>> Hit object layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            Debug.Log($">>> Hit position: {hit.point}");
            Debug.Log($">>> Checking for MonsterAI component...");

            // Show beam effect
            if (lineRenderer != null)
            {
                StartCoroutine(ShowFreezeBeam(ray.origin, hit.point));
            }

            // Play hit sound
            if (audioSource != null && freezeHitSound != null)
            {
                audioSource.PlayOneShot(freezeHitSound, 0.5f);
            }

            // Spawn impact particles
            if (freezeImpactEffect != null)
            {
                ParticleSystem impact = Instantiate(freezeImpactEffect, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impact.gameObject, 2f);
            }

            // Check if we hit a monster
            MonsterAI monster = hit.collider.GetComponentInParent<MonsterAI>();

            if (monster != null)
            {
                Debug.Log($">>> MONSTER FOUND: {monster.gameObject.name}");
                Debug.Log($">>> Monster type: {monster.GetType().Name}");
                FreezeMonster(monster, hit.point);
            }
            else
            {
                Debug.Log(">>> Not a monster - GetComponentInParent<MonsterAI>() returned null");
                MonsterAI directMonster = hit.collider.GetComponent<MonsterAI>();
                if (directMonster != null)
                {
                    Debug.Log(">>> Found monster with direct GetComponent!");
                    FreezeMonster(directMonster, hit.point);
                }
                else
                {
                    Debug.Log(">>> No MonsterAI component found on hit object or parents");
                }
            }
        }
        else
        {
            // Missed - show beam to max range
            if (lineRenderer != null)
            {
                StartCoroutine(ShowFreezeBeam(ray.origin, ray.origin + ray.direction * shootRange));
            }
            Debug.Log(">>> MISSED");
        }
    }

    void FreezeMonster(MonsterAI monster, Vector3 hitPosition)
    {

        Animator monsterAnimator = monster.GetComponent<Animator>();
        if (monsterAnimator != null)
        {
            monsterAnimator.speed = 0f;
        }

        UnityEngine.AI.NavMeshAgent agent = monster.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.velocity = Vector3.zero; // Kill current momentum
            agent.isStopped = true;        // Stop pathfinding
        }
        if (monster != null)
        {
            monster.enabled = false;
        }

        Debug.Log($"[FreezeGun] Completely froze {monster.gameObject.name}");

        CreateIceBlock(monster, monsterAnimator);
    }


    void CreateIceBlock(MonsterAI monster, Animator monsterAnimator)
    {
        GameObject iceBlock = null; // Initialize to null

        // Calculate spawn position with offset
        Vector3 spawnPosition = monster.transform.position + iceBlockOffset;

        if (iceBlockPrefab != null)
        {
            // 1. Instantiate the block
            iceBlock = Instantiate(iceBlockPrefab, spawnPosition, Quaternion.identity);

            // 2. GET THE COMPONENT
            IceBlock blockScript = iceBlock.GetComponent<IceBlock>();

            // 3. PASS THE DATA
            if (blockScript != null)
            {
                blockScript.monster = monster;               // Pass the monster reference
                blockScript.monsterAnimator = monsterAnimator; // Pass the animator reference
                blockScript.duration = freezeDuration;       // Sync duration settings
                blockScript.thawStartTime = thawStartTime;   // Sync thaw settings
                blockScript.shakeIntensity = shakeIntensity;
                blockScript.shatterSound = iceShatterSound;  // Pass audio clip if needed
            }
            else
            {
                Debug.LogError("The IceBlock Prefab does not have the IceBlock script attached!");
            }
        }

        Debug.Log($"Ice block created around {monster.gameObject.name}");
    }

    IEnumerator ShowFreezeBeam(Vector3 start, Vector3 end)
    {
        if (lineRenderer == null) yield break;

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);

        yield return new WaitForSeconds(lineDuration);

        lineRenderer.enabled = false;
    }

    public override void SecondaryAction()
    {
        if (!isReloading && currentAmmo < maxAmmo)
        {
            StartCoroutine(Reload());
        }
    }

    IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log("Reloading freeze gun...");

        if (actionText != null)
        {
            actionText.enabled = true;
            actionText.text = "RELOADING...";
            actionText.color = Color.cyan;
        }

        if (audioSource != null && reloadSound != null)
        {
            audioSource.PlayOneShot(reloadSound);
        }

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = maxAmmo;
        isReloading = false;

        if (actionText != null)
        {
            actionText.enabled = false;
        }

        UpdateStatusUI();
        Debug.Log("Reload complete!");
    }

    public override void UpdateStatusUI()
    {
        if (statusText != null)
        {
            statusText.text = $"Cryo: {currentAmmo}/{maxAmmo}";

            if (currentAmmo == 0)
            {
                statusText.color = Color.red;
            }
            else if (currentAmmo <= maxAmmo / 3)
            {
                statusText.color = Color.yellow;
            }
            else
            {
                statusText.color = Color.cyan;
            }
        }
    }

    IEnumerator FlashStatusText()
    {
        if (statusText == null) yield break;

        Color originalColor = statusText.color;

        for (int i = 0; i < 3; i++)
        {
            statusText.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            statusText.color = originalColor;
            yield return new WaitForSeconds(0.1f);
        }
    }

    public override void OnEquip()
    {
        base.OnEquip();
        Debug.Log("Freeze Gun equipped");
    }

    public override void OnUnequip()
    {
        base.OnUnequip();

        if (isReloading)
        {
            StopAllCoroutines();
            isReloading = false;

            if (actionText != null)
            {
                actionText.enabled = false;
            }
        }
    }

    public override bool IsBusy()
    {
        return isReloading;
    }

    public int GetCurrentAmmo()
    {
        return currentAmmo;
    }

    public int GetMaxAmmo()
    {
        return maxAmmo;
    }

    public bool IsReloading()
    {
        return isReloading;
    }
}