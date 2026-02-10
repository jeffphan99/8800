using Unity.Netcode;
using UnityEngine;

namespace Unity.Template.Multiplayer.NGO.Runtime
{
    /// <summary>
    /// Manages the connection requests of players
    /// </summary>
    /// <remarks>
    /// This should be placed on the same GameObject as the NetworkManager.
    /// </remarks>
    public class ConnectionApprovalHandler : MonoBehaviour
    {
        /// <summary>
        /// used in ApprovalCheck. This is intended as a bit of light protection against DOS attacks that rely on sending silly big buffers of garbage.
        /// </summary>
        const int k_MaxConnectPayload = 1024;
        NetworkManager m_NetworkManager;

        /// <summary>
        /// Set by game code to control where new players spawn.
        /// </summary>
        public static Vector3? s_SpawnPosition;
        public static Quaternion? s_SpawnRotation;

        void Start()
        {
            m_NetworkManager = GetComponent<NetworkManager>();
            if (m_NetworkManager != null)
            {
                m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
                m_NetworkManager.ConnectionApprovalCallback = ApprovalCheck;
            }
        }

        void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            Debug.Log($"[DEBUG] ApprovalCheck: Receiving connection request from ClientNetworkId: {request.ClientNetworkId}");

            var connectionData = request.Payload;
            if (connectionData.Length > k_MaxConnectPayload)
            {
                Debug.Log($"[DEBUG] ApprovalCheck: Denying connection due to payload size ({connectionData.Length}) exceeding max ({k_MaxConnectPayload}).");
                response.Approved = false;
                response.Reason = "DOS attack detected";
                return;
            }

            int currentPlayers = m_NetworkManager.ConnectedClients.Count;
            int bots = CustomNetworkManager.Singleton.BotsSpawned;
            int expectedPlayers = CustomNetworkManager.Singleton.ExpectedPlayers;

            Debug.Log($"[DEBUG] ApprovalCheck: Server capacity check. CurrentPlayers: {currentPlayers}, Bots: {bots}, ExpectedPlayers: {expectedPlayers}");

            if ((currentPlayers + bots) >= expectedPlayers)
            {
                Debug.LogWarning($"[DEBUG] ApprovalCheck: Denying connection because server is full. ({currentPlayers} + {bots} >= {expectedPlayers})");
                response.Approved = false;
                response.Reason = "Server is full";
                return;
            }

            Debug.Log("[DEBUG] ApprovalCheck: Approving connection.");
            response.Approved = true;
            response.CreatePlayerObject = true;

            // Use spawn position if set by game code
            if (s_SpawnPosition.HasValue)
            {
                response.Position = s_SpawnPosition.Value;
                response.Rotation = s_SpawnRotation ?? Quaternion.identity;
                Debug.Log($"[DEBUG] ApprovalCheck: Spawn position set to {response.Position}");
            }
        }

        void OnClientDisconnectCallback(ulong obj)
        {
            if (!m_NetworkManager.IsServer && m_NetworkManager.DisconnectReason != string.Empty)
            {
                Debug.Log($"Server declined connection because: {m_NetworkManager.DisconnectReason}");
            }
        }
    }
}