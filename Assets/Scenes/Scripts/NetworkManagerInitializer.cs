using UnityEngine;

// This attribute makes this script run before most others, ensuring the NetworkManager is active in time.
[DefaultExecutionOrder(-100)]
public class NetworkManagerInitializer : MonoBehaviour
{
    void Awake()
    {
        Debug.Log("[Initializer] Searching for NetworkManager...");

        // Find the NetworkManager in the scene, even if it's inactive.
        // We have to check all root objects because FindObjectOfType only works on active objects.
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        GameObject networkManagerObject = null;

        foreach (var obj in rootObjects)
        {
            if (obj.name == "NetworkManager")
            {
                networkManagerObject = obj;
                break;
            }
        }

        if (networkManagerObject != null)
        {
            Debug.Log("[Initializer] Found NetworkManager. Is active? " + networkManagerObject.activeSelf);
            if (!networkManagerObject.activeSelf)
            {
                Debug.LogWarning("[Initializer] NetworkManager was inactive. Forcibly activating it now.");
                networkManagerObject.SetActive(true);
            }
        }
        else
        {
            Debug.LogError("[Initializer] CRITICAL: Could not find GameObject named 'NetworkManager' in the scene!");
        }
    }
}
