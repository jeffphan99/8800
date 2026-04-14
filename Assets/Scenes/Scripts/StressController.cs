using UnityEngine;
using UnityEngine.Rendering; 


public class StressController : MonoBehaviour
{
    [Header("Settings")]
    public Volume stressVolume;
    public float maxStressDistance = 15f;
    public float minStressDistance = 3f;
    public float stressChangeSpeed = 2f;

    [Header("Audio")]
    public AudioClip heartbeatSound;
    [Range(0f, 1f)] public float heartbeatThreshold = 0.25f;
    private AudioSource audioSource;

    [Header("Debug")]
    public float currentStress = 0f;
    public float closestDistance = float.MaxValue;

    private float targetWeight = 0f;

    void Start()
    {
        if (stressVolume == null)
        {
            stressVolume = GetComponent<Volume>();
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0f;
        if (heartbeatSound != null)
            audioSource.clip = heartbeatSound;
    }

    void Update()
    {
        CalculateStress();
        UpdateVolume();
    }

    void CalculateStress()
    {
        closestDistance = float.MaxValue;

        // Use GameManager to get all monsters
        if (GameManager.Instance != null && GameManager.Instance.allMonsters != null)
        {
            foreach (MonsterAI monster in GameManager.Instance.allMonsters)
            {
                // Only check active monsters
                if (monster != null && monster.isActive)
                {
                    float dist = Vector3.Distance(transform.position, monster.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                    }
                }
            }
        }


        if (closestDistance < maxStressDistance)
        {
 
            targetWeight = Mathf.InverseLerp(maxStressDistance, minStressDistance, closestDistance);
        }
        else
        {
            targetWeight = 0f;
        }
    }

    void UpdateVolume()
    {
        if (stressVolume != null)
        {
            stressVolume.weight = Mathf.Lerp(stressVolume.weight, targetWeight, Time.deltaTime * stressChangeSpeed);
            currentStress = stressVolume.weight;
        }

        if (audioSource != null && heartbeatSound != null)
        {
            float targetVolume = currentStress >= heartbeatThreshold
                ? Mathf.InverseLerp(heartbeatThreshold, 1f, currentStress)
                : 0f;
            audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, Time.deltaTime * stressChangeSpeed);

            if (targetVolume > 0f && !audioSource.isPlaying)
                audioSource.Play();
            else if (targetVolume <= 0f && audioSource.volume < 0.01f && audioSource.isPlaying)
                audioSource.Stop();
        }
    }
}