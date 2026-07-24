using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Incantation.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class FishNetFoundationController : MonoBehaviour
    {
        private NetworkManager networkManager;
        private bool clientWasConnected;

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
        }

        private void OnEnable()
        {
            if (networkManager == null)
            {
                networkManager = GetComponent<NetworkManager>();
            }

            networkManager.ServerManager.OnServerConnectionState += HandleServerConnectionState;
            networkManager.ClientManager.OnClientConnectionState += HandleClientConnectionState;
        }

        private void OnDisable()
        {
            if (networkManager == null || !networkManager.Initialized)
            {
                return;
            }

            networkManager.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
            networkManager.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
        }

        public void StartHost()
        {
            if (!networkManager.ServerManager.Started)
            {
                networkManager.ServerManager.StartConnection();
            }

            if (!networkManager.ClientManager.Started)
            {
                networkManager.ClientManager.StartConnection("localhost");
            }
        }

        public void StartServer()
        {
            if (!networkManager.ServerManager.Started)
            {
                networkManager.ServerManager.StartConnection();
            }
        }

        public void StartClient()
        {
            if (!networkManager.ClientManager.Started)
            {
                networkManager.ClientManager.StartConnection("localhost");
            }
        }

        public void Disconnect()
        {
            if (networkManager.ClientManager.Started)
            {
                networkManager.ClientManager.StopConnection();
            }

            if (networkManager.ServerManager.Started)
            {
                networkManager.ServerManager.StopConnection(true);
            }
        }

        private void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                Debug.Log("Server Started");
            }
        }

        private void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                clientWasConnected = true;
                Debug.Log("Client Connected");
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped && clientWasConnected)
            {
                clientWasConnected = false;
                Debug.Log("Client Disconnected");
            }
        }
    }
}
