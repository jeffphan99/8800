using UnityEngine;
using UnityEngine.Rendering; 


public class StressController : MonoBehaviour
{
    [Header("Settings")]
    public Volume stressVolume; 
    public float maxStressDistance = 15f; 
    public float minStressDistance = 3f;  
    public float stressChangeSpeed = 2f;  

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
            // Smoothly interpolate the weight
            stressVolume.weight = Mathf.Lerp(stressVolume.weight, targetWeight, Time.deltaTime * stressChangeSpeed);
            currentStress = stressVolume.weight;
        }
    }
}