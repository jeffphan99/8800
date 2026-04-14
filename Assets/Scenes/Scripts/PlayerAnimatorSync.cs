using UnityEngine;
using Unity.Netcode;

/// Syncs the player's Run animator bool across all clients.
/// Owner reads their CharacterController velocity and writes to a NetworkVariable.
/// Non-owners apply it from OnValueChanged.
public class PlayerAnimatorSync : NetworkBehaviour
{
    private Animator _animator;
    private CharacterController _cc;

    // Owner-writable so the client can push their own state without a ServerRpc
    private NetworkVariable<bool> networkIsRunning = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        _animator = GetComponentInChildren<Animator>();
        _cc = GetComponent<CharacterController>();

        if (!IsOwner)
            networkIsRunning.OnValueChanged += OnRunningChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
            networkIsRunning.OnValueChanged -= OnRunningChanged;
    }

    private void OnRunningChanged(bool prev, bool next)
    {
        if (_animator != null)
            _animator.SetBool("Run", next);
    }

    void Update()
    {
        if (!IsOwner || !IsSpawned) return;

        // CC may be recreated by WarpAfterNetworkSpawn — re-fetch if lost
        if (_cc == null)
            _cc = GetComponent<CharacterController>();
        if (_cc == null) return;

        bool running = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude > 0.1f;
        if (running != networkIsRunning.Value)
            networkIsRunning.Value = running;
    }
}
