using System;
using System.Collections.Generic;
using System.Globalization;
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
        private SteamRitualLobbyDirectory steamLobbyDirectory;
        private string pendingJoinSeal = string.Empty;
        private float nextAnnouncementTime;
        private float joinDeadline;
        private bool joinAttemptActive;
        private bool endpointResolved;
        private bool hostCreationPending;
        private bool sealReplacementPending;
        private bool clientLeavePending;
        private bool disconnectHostOnSteamCreationFailure;
        private bool invitedLobbyJoinPending;
        private float sealReplacementDeadline;
        private string pendingReplacementSeal = string.Empty;
        private readonly Dictionary<string, LanRitualAdvertisement> knownLanRitualsByHost = new();
        private readonly string instanceId = Guid.NewGuid().ToString("N");

        private readonly struct LanRitualAdvertisement
        {
            public LanRitualAdvertisement(string seal, float lastSeen)
            {
                Seal = seal;
                LastSeen = lastSeen;
            }

            public string Seal { get; }
            public float LastSeen { get; }
        }

        public static RitualSealService Instance { get; private set; }
        public string ActiveSeal { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = string.Empty;
        public bool HasActiveRitual => !string.IsNullOrEmpty(ActiveSeal);
        public bool IsJoining => !string.IsNullOrEmpty(pendingJoinSeal) || invitedLobbyJoinPending;
        public bool IsHostingRitual => HasActiveRitual && foundationController != null && foundationController.IsHostRunning;
        public bool IsCreatingRitual => hostCreationPending;
        public bool IsReplacingSeal => sealReplacementPending;
        public bool IsLeavingJoinedRitual => clientLeavePending;
        public RitualJoinStatus JoinStatus { get; private set; }
        public string JoinFailureReason { get; private set; } = string.Empty;

        public event Action Changed;

        private void Awake()
        {
            Instance = this;
            foundationController = GetComponent<FishNetFoundationController>();
            steamLobbyDirectory = GetComponent<SteamRitualLobbyDirectory>();
            if (!IsSteamMode)
            {
                OpenDirectorySocket();
            }
        }

        private void OnEnable()
        {
            if (foundationController != null)
            {
                foundationController.StateChanged += HandleFoundationStateChanged;
                foundationController.SceneSynchronizationStarted += HandleSceneSynchronizationStarted;
                foundationController.SceneSynchronizationCompleted += HandleSceneSynchronizationCompleted;
            }

            if (steamLobbyDirectory != null)
            {
                steamLobbyDirectory.HostedLobbyReady += HandleSteamHostedLobbyReady;
                steamLobbyDirectory.JoinedLobbyReady += HandleSteamJoinedLobbyReady;
                steamLobbyDirectory.InvitedLobbyJoinRequested += HandleInvitedLobbyJoinRequested;
                steamLobbyDirectory.OperationFailed += HandleSteamLobbyOperationFailed;
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

            if (steamLobbyDirectory != null)
            {
                steamLobbyDirectory.HostedLobbyReady -= HandleSteamHostedLobbyReady;
                steamLobbyDirectory.JoinedLobbyReady -= HandleSteamJoinedLobbyReady;
                steamLobbyDirectory.InvitedLobbyJoinRequested -= HandleInvitedLobbyJoinRequested;
                steamLobbyDirectory.OperationFailed -= HandleSteamLobbyOperationFailed;
            }

            NetworkPlayer.LocalPlayerCreated -= HandleLocalPlayerCreated;
            NetworkPlayer.ActivePlayerRemoved -= HandleNetworkPlayerRemoved;
        }

        private void Update()
        {
            ReceiveDirectoryMessages();

            if (!IsSteamMode && HasActiveRitual && foundationController != null && foundationController.IsHostRunning &&
                Time.unscaledTime >= nextAnnouncementTime)
            {
                BroadcastAvailability();
                nextAnnouncementTime = Time.unscaledTime + AnnouncementInterval;
            }

            if (joinAttemptActive && Time.unscaledTime >= joinDeadline)
            {
                if (IsSteamMode)
                {
                    steamLobbyDirectory?.CancelPendingOperation();
                }

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

            if (sealReplacementPending && Time.unscaledTime >= sealReplacementDeadline)
            {
                ActiveSeal = pendingReplacementSeal;
                pendingReplacementSeal = string.Empty;
                sealReplacementPending = false;
                SetStatus(string.Empty);
                BroadcastAvailability();
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

            if (IsSteamMode && !CanUseSteamLobbyDirectory())
            {
                SetStatus("Steam is unavailable.");
                return false;
            }

            bool hostWasRunning = foundationController.IsHostRunning;
            hostCreationPending = true;
            disconnectHostOnSteamCreationFailure = IsSteamMode && !hostWasRunning;
            bool hostStartAccepted = hostWasRunning ||
                                     (IsSteamMode
                                         ? foundationController.StartHost()
                                         : foundationController.TrySelectAvailableHostPort() &&
                                           foundationController.StartHost());
            if (!hostStartAccepted)
            {
                hostCreationPending = false;
                disconnectHostOnSteamCreationFailure = false;
                SetStatus("The ritual could not be created.");
                return false;
            }

            pendingJoinSeal = string.Empty;
            invitedLobbyJoinPending = false;
            SetStatus("Creating ritual...");

            if (foundationController.IsHostRunning)
            {
                if (IsSteamMode)
                {
                    BeginSteamLobbyCreation();
                }
                else
                {
                    CompleteLanHostCreation();
                }
            }

            return true;
        }

        public bool QuitHostedRitual()
        {
            if (foundationController == null || (!IsHostingRitual && !IsCreatingRitual))
            {
                return false;
            }

            if (IsHostingRitual)
            {
                if (IsSteamMode)
                {
                    steamLobbyDirectory?.CloseHostedLobby();
                }
                else
                {
                    Broadcast($"WITHDRAW|{instanceId}");
                }
            }
            else if (IsSteamMode)
            {
                steamLobbyDirectory?.LeaveActiveLobby();
            }

            ActiveSeal = string.Empty;
            pendingJoinSeal = string.Empty;
            invitedLobbyJoinPending = false;
            joinAttemptActive = false;
            endpointResolved = false;
            hostCreationPending = false;
            sealReplacementPending = false;
            pendingReplacementSeal = string.Empty;
            JoinStatus = RitualJoinStatus.None;
            JoinFailureReason = string.Empty;
            StatusMessage = string.Empty;
            disconnectHostOnSteamCreationFailure = false;

            bool disconnectRequested = foundationController.Disconnect();
            Changed?.Invoke();
            return disconnectRequested;
        }

        public bool LeaveJoinedRitual()
        {
            if (clientLeavePending || foundationController == null ||
                IsHostingRitual || JoinStatus != RitualJoinStatus.Joined)
            {
                return false;
            }

            clientLeavePending = true;
            if (IsSteamMode)
            {
                steamLobbyDirectory?.LeaveActiveLobby();
            }
            pendingJoinSeal = string.Empty;
            invitedLobbyJoinPending = false;
            joinAttemptActive = false;
            endpointResolved = false;
            ActiveSeal = string.Empty;
            JoinStatus = RitualJoinStatus.None;
            JoinFailureReason = string.Empty;
            StatusMessage = string.Empty;

            bool disconnectRequested = foundationController.Disconnect();
            if (!disconnectRequested)
            {
                clientLeavePending = false;
            }

            Changed?.Invoke();
            return disconnectRequested || !foundationController.CanDisconnect;
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
            joinDeadline = Time.unscaledTime +
                           (IsSteamMode && steamLobbyDirectory != null
                               ? steamLobbyDirectory.OperationTimeout
                               : directoryLookupTimeout);
            SetJoinStatus(RitualJoinStatus.Joining);
            if (IsSteamMode)
            {
                if (!CanUseSteamLobbyDirectory() || !steamLobbyDirectory.BeginJoinLobby(pendingJoinSeal))
                {
                    FailJoin("Steam is unavailable");
                    return false;
                }

                Debug.Log($"Steam Ritual Lobby search started for Seal {pendingJoinSeal}.", this);
            }
            else
            {
                Debug.Log($"Ritual Seal directory lookup started for {pendingJoinSeal}.", this);
                Broadcast($"QUERY|{pendingJoinSeal}");
            }
            return true;
        }

        public bool RequestSteamInvite()
        {
            if (!IsSteamMode)
            {
                SetStatus("Steam invitations require Steam mode.");
                return false;
            }

            if (!CanUseSteamLobbyDirectory())
            {
                SetStatus("Steam is unavailable.");
                return false;
            }

            if (!IsHostingRitual)
            {
                SetStatus("Only the Ritual Host can invite a Priest.");
                return false;
            }

            if (!steamLobbyDirectory.OpenHostedLobbyInviteOverlay(out string failureReason))
            {
                SetStatus(failureReason);
                return false;
            }

            Debug.Log("Steam Ritual Lobby invitation overlay requested.", this);
            return true;
        }

        public void BeginJoinEntry()
        {
            pendingJoinSeal = string.Empty;
            invitedLobbyJoinPending = false;
            joinAttemptActive = false;
            endpointResolved = false;
            SetJoinStatus(RitualJoinStatus.Waiting);
        }

        public void CancelJoin()
        {
            if (IsSteamMode)
            {
                steamLobbyDirectory?.CancelPendingOperation();
            }

            pendingJoinSeal = string.Empty;
            invitedLobbyJoinPending = false;
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

        public bool RequestSealReplacement(string requestedSeal)
        {
            string normalizedSeal = NormalizeSeal(requestedSeal);
            if (!IsHostingRitual)
            {
                SetStatus("Seal service unavailable");
                return false;
            }

            if (IsSteamMode)
            {
                SetStatus("Seal editing is unavailable for Steam Rituals");
                return false;
            }

            if (normalizedSeal.Length != 4)
            {
                SetStatus("Enter four characters");
                return false;
            }

            if (normalizedSeal == ActiveSeal)
            {
                SetStatus(string.Empty);
                return true;
            }

            if (sealReplacementPending)
            {
                return false;
            }

            if (IsSealAdvertisedByAnotherHost(normalizedSeal))
            {
                SetStatus("Seal already used");
                return false;
            }

            pendingReplacementSeal = normalizedSeal;
            sealReplacementPending = true;
            sealReplacementDeadline = Time.unscaledTime + directoryLookupTimeout;
            SetStatus("Checking Seal...");
            Broadcast($"QUERY|{normalizedSeal}");
            return true;
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

                if (parts[1] == "WITHDRAW")
                {
                    if (parts[2] != instanceId)
                    {
                        knownLanRitualsByHost.Remove(parts[2]);
                    }

                    continue;
                }

                if ((parts[1] == "ANNOUNCE" || parts[1] == "FULL") &&
                    parts.Length >= 4 && parts[^1] != instanceId)
                {
                    string advertisedSeal = NormalizeSeal(parts[2]);
                    string advertisedHostId = parts[^1];
                    knownLanRitualsByHost[advertisedHostId] =
                        new LanRitualAdvertisement(advertisedSeal, Time.unscaledTime);
                    if (sealReplacementPending && advertisedSeal == pendingReplacementSeal)
                    {
                        pendingReplacementSeal = string.Empty;
                        sealReplacementPending = false;
                        SetStatus("Seal already used");
                    }
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

        private bool IsSealAdvertisedByAnotherHost(string seal)
        {
            float now = Time.unscaledTime;
            foreach (KeyValuePair<string, LanRitualAdvertisement> entry in knownLanRitualsByHost)
            {
                if (entry.Key != instanceId &&
                    entry.Value.Seal == seal &&
                    now - entry.Value.LastSeen <= directoryLookupTimeout)
                {
                    return true;
                }
            }

            return false;
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

            if (clientLeavePending)
            {
                if (!foundationController.CanDisconnect)
                {
                    clientLeavePending = false;
                    Changed?.Invoke();
                }

                return;
            }

            if (hostCreationPending)
            {
                if (foundationController.IsHostRunning)
                {
                    if (IsSteamMode)
                    {
                        if (steamLobbyDirectory != null && !steamLobbyDirectory.IsCreating)
                        {
                            BeginSteamLobbyCreation();
                        }
                    }
                    else
                    {
                        CompleteLanHostCreation();
                    }
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
            invitedLobbyJoinPending = false;
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
            if (IsSteamMode)
            {
                steamLobbyDirectory?.LeaveActiveLobby();
            }

            pendingJoinSeal = string.Empty;
            invitedLobbyJoinPending = false;
            joinAttemptActive = false;
            endpointResolved = false;
            ActiveSeal = string.Empty;
            SetJoinStatus(RitualJoinStatus.CantJoin, reason);
        }

        private bool IsSteamMode =>
            SteamSpikeTransportSelector.SelectedMode == SteamSpikeTransportMode.SteamSpike;

        private bool CanUseSteamLobbyDirectory()
        {
            return steamLobbyDirectory != null &&
                   SteamPlatformBootstrap.Instance != null &&
                   SteamPlatformBootstrap.Instance.IsInitialized;
        }

        private void BeginSteamLobbyCreation()
        {
            if (steamLobbyDirectory != null && steamLobbyDirectory.BeginCreateLobby(GenerateSeal))
            {
                return;
            }

            FailSteamHostCreation("Steam could not create the Ritual lobby.");
        }

        private void CompleteLanHostCreation()
        {
            ActiveSeal = GenerateSeal();
            hostCreationPending = false;
            SetStatus("Waiting for other mages...");
            Broadcast($"ANNOUNCE|{ActiveSeal}|{foundationController.Port}|{instanceId}");
        }

        private void HandleSteamHostedLobbyReady(string seal)
        {
            if (!hostCreationPending || !foundationController.IsHostRunning)
            {
                steamLobbyDirectory?.LeaveActiveLobby();
                return;
            }

            ActiveSeal = NormalizeSeal(seal);
            hostCreationPending = false;
            disconnectHostOnSteamCreationFailure = false;
            SetStatus("Waiting for other mages...");
            Debug.Log($"Steam Ritual Lobby created for Seal {ActiveSeal}.", this);
        }

        private void HandleSteamJoinedLobbyReady(string lobbySeal, ulong hostSteamId64)
        {
            if (!joinAttemptActive || !IsJoining || hostSteamId64 == 0)
            {
                steamLobbyDirectory?.LeaveActiveLobby();
                return;
            }

            string joinedSeal = NormalizeSeal(lobbySeal);
            if (joinedSeal.Length != 4)
            {
                FailJoin("Ritual invitation is invalid");
                return;
            }

            pendingJoinSeal = string.Empty;
            invitedLobbyJoinPending = false;
            endpointResolved = true;
            joinDeadline = Time.unscaledTime + connectionAttemptTimeout;
            string hostAddress = hostSteamId64.ToString(CultureInfo.InvariantCulture);
            Debug.Log($"Steam Ritual Lobby resolved Seal {joinedSeal} to its Host SteamID64.", this);

            if (foundationController.IsClientConnected || foundationController.IsClientConnecting)
            {
                FailJoin("Connection rejected");
            }
            else if (foundationController.StartClient(hostAddress))
            {
                ActiveSeal = joinedSeal;
                SetJoinStatus(RitualJoinStatus.Joining);
            }
            else
            {
                FailJoin("Connection rejected");
            }
        }

        private void HandleInvitedLobbyJoinRequested(ulong lobbyId)
        {
            if (!CanAcceptSteamInvitation())
            {
                const string reason = "Leave the current Ritual before accepting another invitation.";
                Debug.LogWarning(reason, this);
                SetStatus(reason);
                return;
            }

            invitedLobbyJoinPending = true;
            joinAttemptActive = true;
            endpointResolved = false;
            joinDeadline = Time.unscaledTime + steamLobbyDirectory.OperationTimeout;
            SetJoinStatus(RitualJoinStatus.Joining);

            if (!steamLobbyDirectory.BeginJoinInvitedLobby(lobbyId))
            {
                FailJoin("Steam invitation could not be joined");
                return;
            }

            Debug.Log("Accepted Steam invitation is joining its Ritual Lobby.", this);
        }

        private bool CanAcceptSteamInvitation()
        {
            return IsSteamMode &&
                   CanUseSteamLobbyDirectory() &&
                   foundationController != null &&
                   !foundationController.CanDisconnect &&
                   !IsHostingRitual &&
                   !IsCreatingRitual &&
                   !joinAttemptActive &&
                   !IsJoining &&
                   !clientLeavePending &&
                   JoinStatus != RitualJoinStatus.Joining &&
                   JoinStatus != RitualJoinStatus.Joined;
        }

        private void HandleSteamLobbyOperationFailed(string reason)
        {
            if (hostCreationPending)
            {
                FailSteamHostCreation(reason);
            }
            else if (joinAttemptActive)
            {
                FailJoin(reason);
            }
        }

        private void FailSteamHostCreation(string reason)
        {
            steamLobbyDirectory?.LeaveActiveLobby();
            ActiveSeal = string.Empty;
            hostCreationPending = false;
            bool shouldDisconnect = disconnectHostOnSteamCreationFailure;
            disconnectHostOnSteamCreationFailure = false;
            if (shouldDisconnect && foundationController != null && foundationController.CanDisconnect)
            {
                foundationController.Disconnect();
            }

            SetStatus(reason);
        }
    }
}
