using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    void Start()
    {
        // This will load your Main scene immediately after this Init scene has loaded.
        SceneManager.LoadScene("Main");
    }
}
