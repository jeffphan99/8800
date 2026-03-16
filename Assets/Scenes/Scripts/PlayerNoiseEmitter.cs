using Unity.Netcode;
using UnityEngine;

public class PlayerNoiseEmitter : NetworkBehaviour
{
    [Header("Noise Settings")]
    public float noiseRadius = 12f;
    public float noiseCheckInterval = 0.3f;
    public float sprintThreshold = 5.5f;

    private SphereCollider noiseTrigger;
    private float lastCheckTime = 0f;
    private CharacterController characterController;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        characterController = GetComponent<CharacterController>();

        // Create child GameObject on NoiseSource layer with trigger collider
        GameObject noiseObj = new GameObject("NoiseSource");
        noiseObj.transform.SetParent(transform);
        noiseObj.transform.localPosition = Vector3.zero;
        int noiseLayer = LayerMask.NameToLayer("NoiseSource");
        if (noiseLayer == -1)
        {
            Debug.LogError("[PlayerNoiseEmitter] 'NoiseSource' layer not found! Add it in Edit > Project Settings > Tags and Layers.");
        }
        noiseObj.layer = noiseLayer;

        noiseTrigger = noiseObj.AddComponent<SphereCollider>();
        noiseTrigger.isTrigger = true;
        noiseTrigger.radius = noiseRadius;
        noiseTrigger.enabled = false;
    }

    void Update()
    {
        if (!IsServer || characterController == null) return;

        if (Time.time < lastCheckTime + noiseCheckInterval) return;
        lastCheckTime = Time.time;

        bool isSprinting = characterController.velocity.magnitude > sprintThreshold;

        if (noiseTrigger != null)
        {
            noiseTrigger.enabled = isSprinting;
        }
    }
}
