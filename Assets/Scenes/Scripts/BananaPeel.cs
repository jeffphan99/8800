using UnityEngine;
using StarterAssets;

public class BananaPeel : MonoBehaviour
{
    [Header("Trip Settings")]
    public float monsterTripDuration = 4f;
    public float playerTripDuration = 2f;
    public float peelLifetime = 30f;
    public float armingDelay = 0.5f; // NEW: Wait before peel is active

    [Header("Audio")]
    public AudioClip slipSound;
    private AudioSource audioSource;

    private bool hasTrippedMonster = false;
    private bool hasTrippedPlayer = false;
    private bool isArmed = false; // NEW: Peel isn't active immediately

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        // Destroy after lifetime
        Destroy(gameObject, peelLifetime);

        // Arm the peel after delay
        Invoke(nameof(ArmPeel), armingDelay);

        Debug.Log($"Banana peel created at {transform.position}");
    }

    void ArmPeel()
    {
        isArmed = true;
        Debug.Log("Banana peel is now active!");
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isArmed) return; // Don't trigger until armed

        // Check for monster
        MonsterAI monster = other.GetComponentInParent<MonsterAI>();
        if (monster != null && !hasTrippedMonster)
        {
            TripMonster(monster);
            return;
        }

        // Check for player
        if (other.CompareTag("Player") && !hasTrippedPlayer)
        {
            TripPlayer(other.gameObject);
            return;
        }
    }

    void TripMonster(MonsterAI monster)
    {
        hasTrippedMonster = true;

        Debug.Log($"Monster tripped on banana peel for {monsterTripDuration}s!");

        if (slipSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(slipSound, 0.8f);
        }

        monster.Sleep(monsterTripDuration);

        Destroy(gameObject, 0.5f);
    }

    void TripPlayer(GameObject player)
    {
        hasTrippedPlayer = true;

        Debug.Log($"Player tripped on banana peel for {playerTripDuration}s!");

        if (slipSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(slipSound, 0.8f);
        }

        FirstPersonController controller = player.GetComponent<FirstPersonController>();
        if (controller != null)
        {
            StartCoroutine(SlowPlayer(controller));
        }
    }

    System.Collections.IEnumerator SlowPlayer(FirstPersonController controller)
    {
        float originalSpeed = controller.MoveSpeed;
        float originalSprintSpeed = controller.SprintSpeed;

        controller.MoveSpeed = originalSpeed * 0.2f;
        controller.SprintSpeed = originalSprintSpeed * 0.2f;

        Debug.Log("Player movement slowed!");

        yield return new WaitForSeconds(playerTripDuration);

        controller.MoveSpeed = originalSpeed;
        controller.SprintSpeed = originalSprintSpeed;

        Debug.Log("Player movement restored!");

        hasTrippedPlayer = false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isArmed ? Color.yellow : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}