using Unity.Netcode;
using UnityEngine;

public class FreezeGunNetwork : NetworkBehaviour
{
    public GameObject iceBlockPrefab;

    [ServerRpc(RequireOwnership = false)]
    public void RequestFreezeServerRpc(ulong monsterNetworkObjectId, float duration,
        Vector3 iceBlockPosition, Vector3 iceBlockSize, float thawStartTime, float shakeIntensity)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(monsterNetworkObjectId, out var netObj))
            return;

        MonsterAI monster = netObj.GetComponent<MonsterAI>();
        if (monster == null || !monster.isActive) return;

        // Server freezes the monster
        monster.Freeze(duration);

        // Tell all clients to show the ice block visual
        SpawnIceBlockClientRpc(monsterNetworkObjectId, iceBlockPosition, iceBlockSize,
            duration, thawStartTime, shakeIntensity);
    }

    [ClientRpc]
    private void SpawnIceBlockClientRpc(ulong monsterNetworkObjectId, Vector3 position,
        Vector3 size, float duration, float thawStartTime, float shakeIntensity)
    {
        if (iceBlockPrefab == null) return;

        GameObject iceBlock = Instantiate(iceBlockPrefab, position, Quaternion.identity);
        iceBlock.transform.localScale = size;

        IceBlock blockScript = iceBlock.GetComponent<IceBlock>();
        if (blockScript != null)
        {
            blockScript.duration = duration;
            blockScript.thawStartTime = thawStartTime;
            blockScript.shakeIntensity = shakeIntensity;
            // Monster/animator refs are null — IceBlock is visual-only now
            // Server handles freeze/unfreeze via MonsterAI.Freeze()
        }
    }
}
