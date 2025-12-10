using System.Collections;
using UnityEngine;
using StarterAssets;

public class GunWeapon : WeaponBase
{
    [Header("Gun Settings")]
    public float shootRange = 50f;
    public float sleepDuration = 5f;
    public float fireRate = 0.5f;
    public int maxAmmo = 10;
    public float reloadTime = 2f;

    [Header("Raycast Settings")]
    public LayerMask shootableLayers;

    [Header("Visual Effects")]
    public LineRenderer lineRenderer;
    public float lineDuration = 0.1f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip shootSound;
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
            lineRenderer.startWidth = 0.02f;
            lineRenderer.endWidth = 0.02f;
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
        Debug.Log($"Shot fired! Ammo: {currentAmmo}/{maxAmmo}");

        if (audioSource != null && shootSound != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * shootRange, Color.green, 2f);

        if (Physics.Raycast(ray, out hit, shootRange))
        {
            Debug.Log($">>> HIT: {hit.collider.gameObject.name}");

            if (lineRenderer != null)
            {
                StartCoroutine(ShowBulletTrail(ray.origin, hit.point));
            }

            MonsterAI monster = hit.collider.GetComponentInParent<MonsterAI>();

            if (monster != null)
            {
                Debug.Log(">>> FOUND MONSTER! Putting to sleep...");
                monster.Sleep(sleepDuration);
            }
            else
            {
                Debug.Log(">>> Not a monster");
            }
        }
        else
        {
            if (lineRenderer != null)
            {
                StartCoroutine(ShowBulletTrail(ray.origin, ray.origin + ray.direction * shootRange));
            }
            Debug.Log(">>> MISSED");
        }
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
        Debug.Log("Reloading...");

        if (actionText != null)
        {
            actionText.enabled = true;
            actionText.text = "RELOADING...";
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
            statusText.text = $"Ammo: {currentAmmo}/{maxAmmo}";

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
                statusText.color = Color.white;
            }
        }
    }

    IEnumerator ShowBulletTrail(Vector3 start, Vector3 end)
    {
        if (lineRenderer == null) yield break;

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);

        yield return new WaitForSeconds(lineDuration);

        lineRenderer.enabled = false;
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