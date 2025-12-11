using UnityEngine;

public class IceBlock : MonoBehaviour
{
    [Header("Settings - Set by FreezeGun")]
    public float duration = 5f;
    public float thawStartTime = 1f; 
    public float shakeIntensity = 0.1f;
    public AudioClip shatterSound;
    public AudioSource audioSource;
    public Animator monsterAnimator; // Reference to monster's animator to re-enable
    public MonsterAI monster; // Reference to the monster to unfreeze

    private float spawnTime;
    private Vector3 originalPosition;
    private bool isShaking = false;
    private Renderer iceRenderer;
    private Material iceMaterial;
    private Color originalColor;

    void Start()
    {
        spawnTime = Time.time;
        originalPosition = transform.position;

        iceRenderer = GetComponent<Renderer>();
        if (iceRenderer != null)
        {
            iceMaterial = iceRenderer.material;
            originalColor = iceMaterial.color;
        }

        // Debug check
        Debug.Log($"[IceBlock] Start - monsterAnimator is {(monsterAnimator == null ? "NULL" : "assigned")}");
        Debug.Log($"[IceBlock] Start - monster is {(monster == null ? "NULL" : "assigned")}");

        // Schedule destruction
        Invoke(nameof(Shatter), duration);

        Debug.Log($"Ice block will last {duration}s, start shaking at {duration - thawStartTime}s");
    }

    void Update()
    {
        float timeAlive = Time.time - spawnTime;
        float timeRemaining = duration - timeAlive;

        // Start shaking when close to thawing
        if (timeRemaining <= thawStartTime && !isShaking)
        {
            isShaking = true;
            Debug.Log("Ice block starting to shake!");
        }

        if (isShaking)
        {
            // Shake effect - simple random offset
            Vector3 shakeOffset = new Vector3(
                Random.Range(-shakeIntensity, shakeIntensity),
                Random.Range(-shakeIntensity, shakeIntensity),
                Random.Range(-shakeIntensity, shakeIntensity)
            );

            transform.position = originalPosition + shakeOffset;

            // Make ice more transparent as it thaws
            if (iceMaterial != null)
            {
                float fadePercent = timeRemaining / thawStartTime;
                Color fadingColor = originalColor;
                fadingColor.a = originalColor.a * fadePercent;
                iceMaterial.color = fadingColor;
            }
        }
    }

    void Shatter()
    {
        Debug.Log("[IceBlock] ========== SHATTER START ==========");

        if (monsterAnimator != null)
        {
            monsterAnimator.speed = 1f;
        }

        if (monster != null)
        {
            monster.enabled = true;

            UnityEngine.AI.NavMeshAgent agent = monster.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.isStopped = false;
            }

            Debug.Log("[IceBlock] Monster unfrozen and AI re-enabled");
        }

        if (shatterSound != null && audioSource != null)
        {
            AudioSource.PlayClipAtPoint(shatterSound, transform.position, 0.5f);
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Clean up material
        if (iceMaterial != null)
        {
            Destroy(iceMaterial);
        }
    }
}