using System;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Provides guarded developer-only controls and diagnostics for the FishNet foundation.
    /// It observes FishNet's local server/client states and prevents duplicate lifecycle requests.
    /// It does not own production session flow, player authority, lobby state, or gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class FishNetFoundationController : MonoBehaviour
    {
        private const string PortUnavailableMessage =
            "Server failed to start. Another server instance may already be using the configured port.";
        private const string GameplaySceneName = "MainGame";

        private NetworkManager networkManager;
        private LocalConnectionState serverState = LocalConnectionState.Stopped;
        private LocalConnectionState clientState = LocalConnectionState.Stopped;
        private bool hostStartPending;
        private bool serverStartPending;
        private bool clientStartPending;
        private bool clientWasConnected;
        private bool gameplaySceneLoadRequested;
        private string serverFailureStatus = string.Empty;

        public LocalConnectionState ServerState =>
            serverStartPending && serverState == LocalConnectionState.Stopped
                ? LocalConnectionState.Starting
                : serverState;
        public LocalConnectionState ClientState =>
            clientStartPending && clientState == LocalConnectionState.Stopped
                ? LocalConnectionState.Starting
                : clientState;
        public bool IsHostRunning =>
            serverState == LocalConnectionState.Started &&
            clientState == LocalConnectionState.Started;
        public bool IsClientConnected => clientState == LocalConnectionState.Started;
        public bool IsClientConnecting =>
            clientState == LocalConnectionState.Starting || clientStartPending;
        public bool CanStartHost =>
            serverState == LocalConnectionState.Stopped &&
            clientState == LocalConnectionState.Stopped &&
            !serverStartPending &&
            !clientStartPending &&
            !hostStartPending;
        public bool CanStartServer =>
            serverState == LocalConnectionState.Stopped &&
            !serverStartPending;
        public bool CanStartClient =>
            clientState == LocalConnectionState.Stopped &&
            !clientStartPending &&
            !hostStartPending;
        public bool CanDisconnect =>
            serverState != LocalConnectionState.Stopped ||
            clientState != LocalConnectionState.Stopped ||
            serverStartPending ||
            clientStartPending ||
            hostStartPending;
        public ushort Port => GetTransport()?.GetPort() ?? 0;
        public string ClientAddress => GetTransport()?.GetClientAddress() ?? string.Empty;
        public string ServerFailureStatus => serverFailureStatus;

        public event Action StateChanged;

        public static FishNetFoundationController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            networkManager = GetComponent<NetworkManager>();
            RefreshInitialState();
        }

        private void Start()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Bootstrap")
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameplaySceneName);
            }
        }

        private void OnEnable()
        {
            if (networkManager == null)
            {
                networkManager = GetComponent<NetworkManager>();
            }

            RefreshInitialState();
            networkManager.ServerManager.OnServerConnectionState += HandleServerConnectionState;
            networkManager.ClientManager.OnClientConnectionState += HandleClientConnectionState;

            if (serverState == LocalConnectionState.Started)
            {
                LoadGameplayScene();
            }
        }

        private void OnDisable()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
            networkManager.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool StartHost()
        {
            Debug.Log("Start Host requested.");

            if (!CanStartHost)
            {
                Debug.LogWarning(
                    $"Start Host rejected: server is {FormatState(ServerState)} and client is {FormatState(ClientState)}.");
                return false;
            }

            ClearServerFailure();
            hostStartPending = true;
            serverStartPending = true;

            if (networkManager.ServerManager.StartConnection())
            {
                return true;
            }

            HandleImmediateServerStartFailure();
            return false;
        }

        public bool StartServer()
        {
            Debug.Log("Start Server requested.");

            if (!CanStartServer)
            {
                Debug.LogWarning($"Start Server rejected: server is {FormatState(ServerState)}.");
                return false;
            }

            ClearServerFailure();
            serverStartPending = true;

            if (networkManager.ServerManager.StartConnection())
            {
                return true;
            }

            HandleImmediateServerStartFailure();
            return false;
        }

        public bool StartClient()
        {
            return StartClient(ClientAddress);
        }

        public bool StartClient(string address)
        {
            return StartClient(address, Port);
        }

        public bool StartClient(string address, ushort port)
        {
            Debug.Log("Start Client requested.");

            if (!CanStartClient)
            {
                Debug.LogWarning($"Start Client rejected: client is {FormatState(ClientState)}.");
                return false;
            }

            clientStartPending = true;
            if (string.IsNullOrWhiteSpace(address))
            {
                clientStartPending = false;
                Debug.LogWarning("Start Client rejected: no resolved host address was supplied.");
                return false;
            }

            GetTransport()?.SetPort(port);
            if (networkManager.ClientManager.StartConnection(address))
            {
                return true;
            }

            clientStartPending = false;
            Debug.LogWarning("Start Client rejected: FishNet did not accept the connection request.");
            StateChanged?.Invoke();
            return false;
        }

        public bool Disconnect()
        {
            Debug.Log("Disconnect requested.");

            if (!CanDisconnect)
            {
                Debug.LogWarning("Disconnect ignored: server and client are already stopped.");
                return false;
            }

            hostStartPending = false;
            serverStartPending = false;
            clientStartPending = false;

            if (clientState != LocalConnectionState.Stopped)
            {
                networkManager.ClientManager.StopConnection();
            }

            if (serverState != LocalConnectionState.Stopped)
            {
                networkManager.ServerManager.StopConnection(true);
            }

            return true;
        }

        public string GetServerStatusText()
        {
            return $"Server: {FormatState(ServerState)}";
        }

        public string GetClientStatusText()
        {
            return $"Client: {FormatClientState(ClientState)}";
        }

        private void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            LocalConnectionState previousState = serverState;
            serverState = args.ConnectionState;

            switch (serverState)
            {
                case LocalConnectionState.Started:
                    serverStartPending = false;
                    Debug.Log("Server started.");
                    StartPendingHostClient();
                    LoadGameplayScene();
                    break;

                case LocalConnectionState.Stopped:
                    bool failedToStart =
                        serverStartPending &&
                        previousState != LocalConnectionState.Started;

                    serverStartPending = false;
                    hostStartPending = false;
                    gameplaySceneLoadRequested = false;

                    if (failedToStart)
                    {
                        SetServerFailure();
                    }

                    Debug.Log("Server stopped.");
                    break;
            }

            StateChanged?.Invoke();
        }

        private void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            clientState = args.ConnectionState;

            if (clientState == LocalConnectionState.Started)
            {
                clientStartPending = false;
                clientWasConnected = true;
                Debug.Log("Client connected.");
            }
            else if (clientState == LocalConnectionState.Stopped)
            {
                clientStartPending = false;

                if (clientWasConnected)
                {
                    clientWasConnected = false;
                    Debug.Log("Client disconnected.");
                }
            }

            StateChanged?.Invoke();
        }

        private void StartPendingHostClient()
        {
            if (!hostStartPending)
            {
                return;
            }

            hostStartPending = false;

            if (clientState != LocalConnectionState.Stopped)
            {
                Debug.LogWarning($"Start Host rejected: local client became {FormatState(clientState)} before host startup completed.");
                return;
            }

            clientStartPending = true;
            if (!networkManager.ClientManager.StartConnection(ClientAddress))
            {
                clientStartPending = false;
                Debug.LogWarning("Start Host rejected: FishNet did not accept the local client connection request.");
            }
        }

        private void LoadGameplayScene()
        {
            if (gameplaySceneLoadRequested || !networkManager.ServerManager.Started)
            {
                return;
            }

            gameplaySceneLoadRequested = true;

            SceneLoadData sceneLoadData = new(GameplaySceneName)
            {
                ReplaceScenes = ReplaceOption.All
            };

            networkManager.SceneManager.LoadGlobalScenes(sceneLoadData);
            Debug.Log($"FishNet synchronized gameplay scene load requested: {GameplaySceneName}.");
        }

        private void HandleImmediateServerStartFailure()
        {
            serverStartPending = false;
            hostStartPending = false;
            SetServerFailure();
            StateChanged?.Invoke();
        }

        private void SetServerFailure()
        {
            if (!string.IsNullOrEmpty(serverFailureStatus))
            {
                return;
            }

            serverFailureStatus = $"{PortUnavailableMessage} Port: {Port}.";
            Debug.LogError(serverFailureStatus);
        }

        private void ClearServerFailure()
        {
            if (string.IsNullOrEmpty(serverFailureStatus))
            {
                return;
            }

            serverFailureStatus = string.Empty;
            StateChanged?.Invoke();
        }

        private void RefreshInitialState()
        {
            if (networkManager == null)
            {
                return;
            }

            serverState = networkManager.ServerManager.Started
                ? LocalConnectionState.Started
                : LocalConnectionState.Stopped;
            clientState = networkManager.ClientManager.Started
                ? LocalConnectionState.Started
                : LocalConnectionState.Stopped;
            clientWasConnected = clientState == LocalConnectionState.Started;
        }

        private Transport GetTransport()
        {
            return networkManager != null
                ? networkManager.TransportManager.Transport
                : null;
        }

        private static string FormatState(LocalConnectionState state)
        {
            switch (state)
            {
                case LocalConnectionState.Starting:
                    return "Starting";
                case LocalConnectionState.Started:
                    return "Running";
                case LocalConnectionState.Stopping:
                    return "Stopping";
                default:
                    return "Stopped";
            }
        }

        private static string FormatClientState(LocalConnectionState state)
        {
            return state == LocalConnectionState.Started
                ? "Connected"
                : FormatState(state);
        }
    }
}
