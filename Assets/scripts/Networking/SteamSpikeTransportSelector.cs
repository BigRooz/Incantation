using FishNet.Managing.Transporting;
using FishNet.Transporting;
using UnityEngine;

namespace Incantation.Networking
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class SteamSpikeTransportSelector : MonoBehaviour
    {
        [SerializeField] private SteamSpikeTransportMode defaultMode = SteamSpikeTransportMode.TugboatDevelopment;
        [SerializeField] private Transport tugboatTransport;
        [SerializeField] private Transport steamTransport;
        [SerializeField] private GameObject networkRuntimeRoot;

        public static SteamSpikeTransportMode SelectedMode { get; private set; } =
            SteamSpikeTransportMode.TugboatDevelopment;

        public void Configure(Transport tugboat, Transport steam, GameObject runtimeRoot)
        {
            tugboatTransport = tugboat;
            steamTransport = steam;
            networkRuntimeRoot = runtimeRoot;
        }

        private void Awake()
        {
            SelectedMode = SteamSpikeTransportArguments.Resolve(defaultMode);

            if (SelectedMode == SteamSpikeTransportMode.SteamSpike &&
                (SteamPlatformBootstrap.Instance == null || !SteamPlatformBootstrap.Instance.IsInitialized))
            {
                if (!TryValidateGate())
                {
                    return;
                }

                networkRuntimeRoot.GetComponent<TransportManager>().Transport = tugboatTransport;
                string failureReason = SteamPlatformBootstrap.Instance != null
                    ? SteamPlatformBootstrap.Instance.FailureReason
                    : "SteamPlatformBootstrap did not run before transport selection.";
                Debug.LogError(
                    $"ONLINE-001A could not select SteamSpike because Steam initialization failed. {failureReason}",
                    this);
                networkRuntimeRoot.SetActive(true);
                return;
            }

            Transport selectedTransport = SelectedMode == SteamSpikeTransportMode.SteamSpike
                ? steamTransport
                : tugboatTransport;

            if (selectedTransport == null)
            {
                Debug.LogError($"ONLINE-001A could not select {SelectedMode}: its transport reference is missing.", this);
                enabled = false;
                return;
            }

            if (!TryValidateGate())
            {
                return;
            }

            networkRuntimeRoot.GetComponent<TransportManager>().Transport = selectedTransport;
            Debug.Log($"ONLINE-001A selected FishNet transport: {selectedTransport.GetType().Name} ({SelectedMode}).", this);
            networkRuntimeRoot.SetActive(true);
        }

        private bool TryValidateGate()
        {
            if (networkRuntimeRoot == null)
            {
                Debug.LogError("ONLINE-001A cannot initialize FishNet: its runtime root reference is missing.", this);
                enabled = false;
                return false;
            }

            if (networkRuntimeRoot.activeSelf)
            {
                Debug.LogError("ONLINE-001A cannot gate FishNet because its runtime root was already active.", this);
                enabled = false;
                return false;
            }

            if (networkRuntimeRoot.GetComponent<TransportManager>() == null)
            {
                Debug.LogError("ONLINE-001A cannot initialize FishNet: TransportManager is missing from the runtime root.", this);
                enabled = false;
                return false;
            }

            return true;
        }
    }
}
