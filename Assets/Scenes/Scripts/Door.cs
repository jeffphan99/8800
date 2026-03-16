using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Door : NetworkBehaviour
{
    [Header("Door Settings")]
    public Transform doorTransform;
    public Vector3 openPosition = new Vector3(0, 0, 2);
    public float openSpeed = 3f;
    public bool startsOpen = false;
    public bool isCellDoor = false;

    [Header("Interaction Settings")]
    [Range(1f, 10f)]
    public float interactDistance = 3f;

    private Vector3 closedPosition;

    // Networked open state
    private NetworkVariable<bool> networkIsOpen = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool isOpen => networkIsOpen.Value;

    private bool isMoving = false;

    void Awake()
    {
        if (doorTransform == null)
        {
            doorTransform = transform;
        }

        closedPosition = doorTransform.localPosition;
    }

    void Start()
    {
        if (startsOpen && !IsSpawned)
        {
            doorTransform.localPosition = closedPosition + openPosition;
        }

        AutoSetupInteraction();
    }

    public override void OnNetworkSpawn()
    {
        networkIsOpen.OnValueChanged += OnOpenStateChanged;

        // Set initial state
        if (IsServer)
        {
            networkIsOpen.Value = startsOpen;
        }
        else
        {
            // Client: snap to current state
            ApplyDoorPosition(networkIsOpen.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        networkIsOpen.OnValueChanged -= OnOpenStateChanged;
    }

    private void OnOpenStateChanged(bool previousValue, bool newValue)
    {
        // Animate the door on all clients
        if (!isMoving)
        {
            StartCoroutine(AnimateDoor(newValue));
        }
    }

    private void ApplyDoorPosition(bool open)
    {
        if (doorTransform == null) return;
        Vector3 targetPosition = open ? closedPosition + openPosition : closedPosition;
        doorTransform.localPosition = targetPosition;
    }

    void AutoSetupInteraction()
    {
        Interactable interactable = GetComponent<Interactable>();

        if (interactable != null)
        {
            interactable.interactionRange = interactDistance;
            interactable.onInteract.RemoveListener(ToggleDoor);
            interactable.onInteract.AddListener(ToggleDoor);
            Debug.Log($"[Door] Auto-wired interaction for {gameObject.name} with range {interactDistance}");
        }
        else
        {
            Debug.LogWarning($"[Door] {gameObject.name} has a Door script but NO Interactable script!");
        }
    }

    // Called by Interactable (player interaction) and by GameManager
    public void ToggleDoor()
    {
        if (isMoving) return;

        // If not network-spawned, fall back to local toggle
        if (!IsSpawned)
        {
            StartCoroutine(AnimateDoor(!startsOpen));
            return;
        }

        if (IsServer)
        {
            // Server can toggle directly
            networkIsOpen.Value = !networkIsOpen.Value;
        }
        else
        {
            // Client requests server to toggle
            ToggleDoorServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleDoorServerRpc()
    {
        if (isMoving) return;
        networkIsOpen.Value = !networkIsOpen.Value;
    }

    public void OpenDoor()
    {
        if (isMoving) return;
        if (!IsSpawned) { StartCoroutine(AnimateDoor(true)); return; }
        if (isOpen) return;

        if (IsServer)
        {
            networkIsOpen.Value = true;
        }
        else
        {
            OpenDoorServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void OpenDoorServerRpc()
    {
        if (isOpen || isMoving) return;
        networkIsOpen.Value = true;
    }

    public void CloseDoor()
    {
        if (isMoving) return;
        if (!IsSpawned) { StartCoroutine(AnimateDoor(false)); return; }
        if (!isOpen) return;

        if (IsServer)
        {
            networkIsOpen.Value = false;
        }
        else
        {
            CloseDoorServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CloseDoorServerRpc()
    {
        if (!isOpen || isMoving) return;
        networkIsOpen.Value = false;
    }

    IEnumerator AnimateDoor(bool open)
    {
        isMoving = true;
        Vector3 targetPosition = open ? closedPosition + openPosition : closedPosition;

        while (Vector3.Distance(doorTransform.localPosition, targetPosition) > 0.01f)
        {
            doorTransform.localPosition = Vector3.Lerp(
                doorTransform.localPosition,
                targetPosition,
                Time.deltaTime * openSpeed
            );
            yield return null;
        }

        doorTransform.localPosition = targetPosition;
        isMoving = false;
    }
}
