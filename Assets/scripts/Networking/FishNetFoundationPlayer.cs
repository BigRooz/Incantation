using FishNet.Object;
using UnityEngine;

namespace Incantation.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FishNetFoundationPlayer : NetworkBehaviour
    {
        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.Log($"FishNet test player spawned. Owner: {IsOwner}");
        }
    }
}
