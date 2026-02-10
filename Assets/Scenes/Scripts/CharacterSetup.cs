using Unity.Netcode;
using UnityEngine;
using StarterAssets;

public class CharacterSetup : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Enable CharacterController
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            // Enable FirstPersonController
            var fpc = GetComponent<FirstPersonController>();
            if (fpc != null) fpc.enabled = true;
        }
    }
}
