using Unity.Netcode.Components;

/// <summary>
/// Owner-authoritative NetworkTransform.
/// The owner (client) controls its own position and syncs to the server,
/// instead of the server controlling the position. Required for
/// CharacterController-based movement where physics runs on the owner.
/// </summary>
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
