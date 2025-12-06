using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using StarterAssets;

public class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    public string interactionPrompt = "Press E to interact";
    public float interactionRange = 3f;

    [Header("UI Settings")]
    public GameObject interactionUI;
    public Text promptText;

    [Header("Events")]
    public UnityEvent onInteract;

    private bool isInRange = false;
    private Transform player;
    private StarterAssetsInputs _input;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            _input = playerObj.GetComponent<StarterAssetsInputs>();
        }

        if (_input == null)
        {
            Debug.LogError("StarterAssetsInputs component not found on Player!");
        }

        if (interactionUI != null)
        {
            interactionUI.SetActive(false);
        }

        if (promptText != null)
        {
            promptText.text = interactionPrompt;
        }
    }

    void Update()
    {

        if (player == null || GameManager.Instance != null && !GameManager.Instance.IsRoundActive()) return;


        float distance = Vector3.Distance(transform.position, player.position);
        bool nowInRange = distance <= interactionRange;


        if (nowInRange != isInRange)
        {
            isInRange = nowInRange;
            UpdateUI();
        }

        if (isInRange && _input != null && _input.interact)
        {
            Interact();

            _input.interact = false;
        }
    }

    void UpdateUI()
    {
        if (interactionUI != null)
        {
            interactionUI.SetActive(isInRange);
        }
    }

    void Interact()
    {
        Debug.Log($"Interacted with {gameObject.name}");
        onInteract?.Invoke();
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}