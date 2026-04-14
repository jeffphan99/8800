using System.Collections;
using UnityEngine;

public class LoadingScreen : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup canvasGroup;

    [Header("Settings")]
    public float fadeDuration = 0.8f;

    void Start()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        StartCoroutine(WaitThenFade());
    }

    IEnumerator WaitThenFade()
    {
        // Wait until the local player is spawned and camera binding has run
        yield return new WaitUntil(() => PlayerHealth.LocalPlayer != null);

        // Small buffer — RetryBindCinemachine can take a few frames
        yield return new WaitForSeconds(0.3f);

        // Fade out
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
}
