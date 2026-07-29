using FishNet.Object;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Reserved server-owned network boundary for future ritual synchronization.
    /// This foundation intentionally contains no ritual state or gameplay behavior.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRitualAuthority : NetworkBehaviour
    {
    }
}
