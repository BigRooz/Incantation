using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Incantation.Networking
{
    public enum RitualJoinStatus
    {
        None,
        Waiting,
        Joining,
        Joined,
        CantJoin
    }

    /// <summary>
    /// Resolves a human-readable Ritual Seal to a LAN host without exposing transport
    /// details to the Book UI. FishNet remains responsible for the actual connection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RitualSealService : MonoBehaviour
    {
        private const int DirectoryPort = 47742;
        private const float AnnouncementInterval = 1f;
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const string ProtocolPrefix = "INCANTATION_RITUAL_V1";

        [Header("Join Lifecycle")]
        [SerializeField, Min(1f)] private float directoryLookupTimeout = 5f;
        [SerializeField, Min(1f)] private float connectionAttemptTimeout = 15f;

        private UdpClient directorySocket;
        private FishNetFoundationController foundationController;
        private string pendingJoinSeal = string.Empty;
        private float nextAnnouncementTime;
        private float joinDeadline;
        private bool joinAttemptActive;
        private bool endpointResolved;
        private bool hostCreationPending;
        private readonly string instanceId = Guid.NewGuid().ToString("N");

        public static RitualSealService Instance { get; private set; }
        public string ActiveSeal { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = string.Empty;
        public bool HasActiveRitual => !string.IsNullOrEmpty(ActiveSeal);
        public bool IsJoining => !string.IsNullOrEmpty(pendingJoinSeal);
        public bool IsHostingRitual => HasActiveRitual && foundationController != null && foundationController.IsHostRunning;
        public bool IsCreatingRitual => hostCreationPending;
        public RitualJoinStatus JoinStatus { get; private set; }
        public string JoinFailureReason { get; private set; } = string.Empty;

        public event Action Changed;

        private void Awake()
        {
            Instance = this;
            foundationController = GetComponent<FishNetFoundationController>();
            OpenDirectorySocket();
        }

        private void OnEnable()
        {
            if (foundationController != null)
            {
                foundationController.StateChanged += HandleFoundationStateChanged;
                foundationController.SceneSynchronizationStarted += HandleSceneSynchronizationStarted;
                foundationController.SceneSynchronizationCompleted += HandleSceneSynchronizationCompleted;
            }

            NetworkPlayer.LocalPlayerCreated += HandleLocalPlayerCreated;
            NetworkPlayer.ActivePlayerRemoved += HandleNetworkPlayerRemoved;
        }

        private void OnDisable()
        {
            if (foundationController != null)
            {
                foundationController.StateChanged -= HandleFoundationStateChanged;
                foundationController.SceneSynchronizationStarted -= HandleSceneSynchronizationStarted;
                foundationController.SceneSynchronizationCompleted -= HandleSceneSynchronizationCompleted;
            }

            NetworkPlayer.LocalPlayerCreated -= HandleLocalPlayerCreated;
            NetworkPlayer.ActivePlayerRemoved -= HandleNetworkPlayerRemoved;
        }

        private void Update()
        {
            ReceiveDirectoryMessages();

            if (HasActiveRitual && foundationController != null && foundationController.IsHostRunning &&
                Time.unscaledTime >= nextAnnouncementTime)
            {
                BroadcastAvailability();
                nextAnnouncementTime = Time.unscaledTime + AnnouncementInterval;
            }

            if (joinAttemptActive && Time.unscaledTime >= joinDeadline)
            {
                if (endpointResolved)
                {
                    Debug.LogWarning("Ritual join timed out before the local NetworkPlayer was created.", this);
                    foundationController?.StopIncompleteClientAttempt();
                    FailJoin("Connection timed out");
                }
                else
                {
                    Debug.LogWarning($"Ritual Seal lookup expired without finding {pendingJoinSeal}.", this);
                    FailJoin("Ritual not found");
                }
            }
        }

        private void OnDestroy()
        {
            directorySocket?.Close();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool CreateRitual()
        {
            if (foundationController == null)
            {
                SetStatus("The ritual network is unavailable.");
                return false;
            }

            if (IsHostingRitual || IsCreatingRitual)
            {
                return false;
            }

            if (!foundationController.IsHostRunning && !foundationController.StartHost())
            {
                SetStatus("The ritual could not be created.");
                return false;
            }

            ActiveSeal = GenerateSeal();
            pendingJoinSeal = string.Empty;
            hostCreationPending = !foundationController.IsHostRunning;
            SetStatus(foundationController.IsHostRunning
                ? "Waiting for other mages..."
                : "Creating ritual...");

            if (foundationController.IsHostRunning)
            {
                Broadcast($"ANNOUNCE|{ActiveSeal}|{foundationController.Port}|{instanceId}");
            }

            return true;
        }

        public bool QuitHostedRitual()
        {
            if (foundationController == null || (!IsHostingRitual && !IsCreatingRitual))
            {
                return false;
            }

            ActiveSeal = string.Empty;
            pendingJoinSeal = string.Empty;
            joinAttemptActive = false;
            endpointResolved = false;
            hostCreationPending = false;
            JoinStatus = RitualJoinStatus.None;
            JoinFailureReason = string.Empty;
            StatusMessage = string.Empty;

            bool disconnectRequested = foundationController.Disconnect();
            Changed?.Invoke();
            return disconnectRequested;
        }

        public bool JoinRitual(string seal)
        {
            string normalizedSeal = NormalizeSeal(seal);
            Debug.Log($"Validate Seal requested. Normalized Seal: {normalizedSeal}.", this);
            if (normalizedSeal.Length != 4 || foundationController == null)
            {
                SetJoinStatus(RitualJoinStatus.CantJoin, "Enter four characters");
                return false;
            }

            if (JoinStatus == RitualJoinStatus.Joining || IsJoining)
            {
                return false;
            }

            pendingJoinSeal = normalizedSeal;
            joinAttemptActive = true;
            endpointResolved = false;
            joinDeadline = Time.unscaledTime + directoryLookupTimeout;
            SetJoinStatus(RitualJoinStatus.Joining);
            Debug.Log($"Ritual Seal directory lookup started for {pendingJoinSeal}.", this);
            Broadcast($"QUERY|{pendingJoinSeal}");
            return true;
        }

        public void BeginJoinEntry()
        {
            pendingJoinSeal = string.Empty;
            joinAttemptActive = false;
            endpointResolved = false;
            SetJoinStatus(RitualJoinStatus.Waiting);
        }

        public void CancelJoin()
        {
            pendingJoinSeal = string.Empty;
            joinAttemptActive = false;
            endpointResolved = false;
            JoinStatus = RitualJoinStatus.None;
            JoinFailureReason = string.Empty;
            StatusMessage = string.Empty;
            Changed?.Invoke();
        }

        public static string NormalizeSeal(string seal)
        {
            StringBuilder result = new StringBuilder(4);
            string source = (seal ?? string.Empty).ToUpperInvariant();
            for (int i = 0; i < source.Length && result.Length < 4; i++)
            {
                if (Alphabet.IndexOf(source[i]) >= 0)
                {
                    result.Append(source[i]);
                }
            }

            return result.ToString();
        }

        private void OpenDirectorySocket()
        {
            try
            {
                directorySocket = new UdpClient();
                directorySocket.ExclusiveAddressUse = false;
                directorySocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                directorySocket.Client.Bind(new IPEndPoint(IPAddress.Any, DirectoryPort));
                directorySocket.EnableBroadcast = true;
                directorySocket.Client.Blocking = false;
            }
            catch (SocketException exception)
            {
                Debug.LogError($"Ritual Seal directory could not start: {exception.Message}", this);
                directorySocket?.Close();
                directorySocket = null;
            }
        }

        private void ReceiveDirectoryMessages()
        {
            if (directorySocket == null)
            {
                return;
            }

            while (directorySocket.Available > 0)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] bytes;
                try
                {
                    bytes = directorySocket.Receive(ref sender);
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.WouldBlock)
                {
                    break;
                }

                string[] parts = Encoding.ASCII.GetString(bytes).Split('|');
                if (parts.Length < 3 || parts[0] != ProtocolPrefix)
                {
                    continue;
                }

                if (parts[1] == "QUERY" && HasActiveRitual && parts[2] == ActiveSeal &&
                    foundationController != null && foundationController.IsHostRunning)
                {
                    BroadcastAvailability();
                }
                else if (parts[1] == "ANNOUNCE" && IsJoining && parts[2] == pendingJoinSeal &&
                         parts.Length >= 5 && parts[4] != instanceId && ushort.TryParse(parts[3], out ushort port))
                {
                    string joinedSeal = pendingJoinSeal;
                    pendingJoinSeal = string.Empty;
                    endpointResolved = true;
                    joinDeadline = Time.unscaledTime + connectionAttemptTimeout;
                    Debug.Log($"Ritual Seal found. Resolved endpoint: {sender.Address}:{port}.", this);
                    if (foundationController.IsClientConnected || foundationController.IsClientConnecting)
                    {
                        Debug.LogWarning("Resolved Ritual Seal while a FishNet client was already started or starting.", this);
                        FailJoin("Connection rejected");
                    }
                    else if (foundationController.StartClient(sender.Address.ToString(), port))
                    {
                        ActiveSeal = joinedSeal;
                        SetJoinStatus(RitualJoinStatus.Joining);
                    }
                    else
                    {
                        FailJoin("Connection rejected");
                    }
                }
                else if (parts[1] == "FULL" && IsJoining && parts[2] == pendingJoinSeal &&
                         parts.Length >= 4 && parts[3] != instanceId)
                {
                    pendingJoinSeal = string.Empty;
                    Debug.Log($"Ritual Seal {parts[2]} was found, but the ritual is full.", this);
                    FailJoin("Ritual is full");
                }
                else if (parts[1] == "ANNOUNCE" && HasActiveRitual && parts[2] == ActiveSeal &&
                         parts.Length >= 5 && parts[4] != instanceId && foundationController != null &&
                         foundationController.IsHostRunning)
                {
                    ActiveSeal = GenerateSeal();
                    SetStatus("Seal collision avoided. Share the new Seal.");
                    Broadcast($"ANNOUNCE|{ActiveSeal}|{foundationController.Port}|{instanceId}");
                }
            }
        }

        private string GenerateSeal()
        {
            char[] characters = new char[4];
            for (int i = 0; i < characters.Length; i++)
            {
                characters[i] = Alphabet[UnityEngine.Random.Range(0, Alphabet.Length)];
            }

            return new string(characters);
        }

        private void Broadcast(string payload)
        {
            Send(IPAddress.Broadcast, payload);
        }

        private void BroadcastAvailability()
        {
            bool isFull = NetworkPlayer.CircleMemberCount >= NetworkPlayer.MaximumCircleMembers;
            Broadcast(isFull
                ? $"FULL|{ActiveSeal}|{instanceId}"
                : $"ANNOUNCE|{ActiveSeal}|{foundationController.Port}|{instanceId}");
        }

        private void Send(IPAddress address, string payload)
        {
            if (directorySocket == null)
            {
                return;
            }

            byte[] bytes = Encoding.ASCII.GetBytes($"{ProtocolPrefix}|{payload}");
            directorySocket.Send(bytes, bytes.Length, new IPEndPoint(address, DirectoryPort));
        }

        private void SetStatus(string value)
        {
            StatusMessage = value;
            Changed?.Invoke();
        }

        private void SetJoinStatus(RitualJoinStatus status, string reason = "")
        {
            JoinStatus = status;
            JoinFailureReason = reason;
            StatusMessage = status switch
            {
                RitualJoinStatus.Waiting => "Waiting...",
                RitualJoinStatus.Joining => "Joining...",
                RitualJoinStatus.CantJoin => "Can't Join",
                _ => string.Empty
            };
            Changed?.Invoke();
        }

        private void HandleFoundationStateChanged()
        {
            if (foundationController == null)
            {
                return;
            }

            if (hostCreationPending)
            {
                if (foundationController.IsHostRunning)
                {
                    hostCreationPending = false;
                    SetStatus("Waiting for other mages...");
                    Broadcast($"ANNOUNCE|{ActiveSeal}|{foundationController.Port}|{instanceId}");
                }
                else if (!foundationController.CanDisconnect ||
                         (!foundationController.IsClientConnecting &&
                          foundationController.ServerState == FishNet.Transporting.LocalConnectionState.Started))
                {
                    hostCreationPending = false;
                    ActiveSeal = string.Empty;
                    if (foundationController.CanDisconnect)
                    {
                        foundationController.Disconnect();
                    }

                    SetStatus(string.IsNullOrEmpty(foundationController.ServerFailureStatus)
                        ? "The ritual could not be created."
                        : foundationController.ServerFailureStatus);
                }

                return;
            }

            if (HasActiveRitual && foundationController.IsHostRunning)
            {
                return;
            }

            if (HasActiveRitual &&
                JoinStatus == RitualJoinStatus.None &&
                !foundationController.CanDisconnect)
            {
                ActiveSeal = string.Empty;
                Changed?.Invoke();
                return;
            }

            if (JoinStatus != RitualJoinStatus.Joining)
            {
                return;
            }

            if (foundationController.IsClientConnected)
            {
                Debug.Log("FishNet client reached Started; awaiting scene synchronization and local NetworkPlayer creation.", this);
            }
            else if (endpointResolved && !foundationController.IsClientConnecting)
            {
                Debug.LogWarning("FishNet client stopped before the ritual join completed.", this);
                FailJoin("Connection rejected");
            }
        }

        private void HandleSceneSynchronizationStarted()
        {
            if (joinAttemptActive)
            {
                Debug.Log("Ritual join observed scene synchronization begin.", this);
            }
        }

        private void HandleSceneSynchronizationCompleted()
        {
            if (joinAttemptActive)
            {
                Debug.Log("Ritual join observed scene synchronization complete; awaiting local NetworkPlayer.", this);
            }
        }

        private void HandleLocalPlayerCreated(NetworkPlayer player)
        {
            if (!joinAttemptActive || player == null || !player.IsLocalPlayer)
            {
                return;
            }

            pendingJoinSeal = string.Empty;
            joinAttemptActive = false;
            endpointResolved = false;
            SetJoinStatus(RitualJoinStatus.Joined);
            Debug.Log("Ritual join completed after local NetworkPlayer creation.", this);
        }

        private void HandleNetworkPlayerRemoved(NetworkPlayer player)
        {
            if (player == null || player != NetworkPlayer.LocalPlayer ||
                JoinStatus != RitualJoinStatus.Joined)
            {
                return;
            }

            ActiveSeal = string.Empty;
            SetJoinStatus(RitualJoinStatus.None);
            Debug.Log("[Circle] Disconnected local membership cleared.", this);
        }

        private void FailJoin(string reason)
        {
            pendingJoinSeal = string.Empty;
            joinAttemptActive = false;
            endpointResolved = false;
            ActiveSeal = string.Empty;
            SetJoinStatus(RitualJoinStatus.CantJoin, reason);
        }
    }
}
