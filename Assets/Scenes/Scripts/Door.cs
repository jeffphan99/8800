using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Door : NetworkBehaviour
{
    [Header("Door Settings")]
    public Transform doorTransform;
    public Vector3 openPosition = new Vector3(0, 0, 2);
    public float openSpeed = 3f;
    public bool startsOpen = false;
    public bool isCellDoor = false;

    [Header("Cell Door Settings")]
    [Tooltip("Radius to search for monsters to awaken when opened, and to check all doors are closed when closing")]
    public float monsterAwakenRadius = 10f;

    [Header("Interaction Settings")]
    [Range(1f, 10f)]
    public float interactDistance = 3f;

    [Header("Audio")]
    public AudioClip openSound;
    public AudioClip closeSound;
    private AudioSource audioSource;

    private Vector3 closedPosition;
    private NavMeshObstacle navObstacle;

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

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.maxDistance = 10f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.playOnAwake = false;

        navObstacle = doorTransform.GetComponent<NavMeshObstacle>();
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

        if (IsServer && isCellDoor)
        {
            if (newValue)
                AwakenNearbyMonsters();
            else
                TryRecontainMonster();
        }
    }

    private void AwakenNearbyMonsters()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, monsterAwakenRadius);
        foreach (Collider col in nearby)
        {
            MonsterAI monster = col.GetComponentInParent<MonsterAI>();
            if (monster != null && !monster.isActive)
            {
                monster.ActivateMonster();
                Debug.Log($"[Door] Cell door opened — awakened {monster.gameObject.name}");
            }
        }
    }

    private void TryRecontainMonster()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, monsterAwakenRadius);

        // If any cell door in range is still open, don't recontain yet
        foreach (Collider col in nearby)
        {
            Door door = col.GetComponent<Door>() ?? col.GetComponentInParent<Door>();
            if (door != null && door != this && door.isCellDoor && door.isOpen)
                return;
        }

        // All cell doors in range are closed — deactivate nearby monsters
        foreach (Collider col in nearby)
        {
            MonsterAI monster = col.GetComponentInParent<MonsterAI>();
            if (monster != null && monster.isActive)
            {
                monster.DeactivateMonster();
                monster.ResetMonster();
                Debug.Log($"[Door] All cell doors closed — recontained {monster.gameObject.name}");
            }
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

        AudioClip clip = open ? openSound : closeSound;
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);

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

        // Closed = obstacle carves into navmesh, blocking pathing through it
        // Open = no carving so monster can path through the doorway freely
        if (navObstacle != null)
            navObstacle.carving = !open;
    }
}
