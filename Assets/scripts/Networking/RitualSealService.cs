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
        private const float JoinTimeout = 5f;
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const string ProtocolPrefix = "INCANTATION_RITUAL_V1";

        private UdpClient directorySocket;
        private FishNetFoundationController foundationController;
        private string pendingJoinSeal = string.Empty;
        private float nextAnnouncementTime;
        private float joinDeadline;
        private readonly string instanceId = Guid.NewGuid().ToString("N");

        public static RitualSealService Instance { get; private set; }
        public string ActiveSeal { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = string.Empty;
        public bool HasActiveRitual => !string.IsNullOrEmpty(ActiveSeal);
        public bool IsJoining => !string.IsNullOrEmpty(pendingJoinSeal);
        public bool IsHostingRitual => HasActiveRitual && foundationController != null && foundationController.IsHostRunning;
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
            }
        }

        private void OnDisable()
        {
            if (foundationController != null)
            {
                foundationController.StateChanged -= HandleFoundationStateChanged;
            }
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

            if (IsJoining && Time.unscaledTime >= joinDeadline)
            {
                pendingJoinSeal = string.Empty;
                SetJoinStatus(RitualJoinStatus.CantJoin, "Ritual not found");
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

            if (!foundationController.IsHostRunning && !foundationController.StartHost())
            {
                SetStatus("The ritual could not be created.");
                return false;
            }

            ActiveSeal = GenerateSeal();
            pendingJoinSeal = string.Empty;
            SetStatus("Waiting for other mages...");
            Broadcast($"ANNOUNCE|{ActiveSeal}|{foundationController.Port}|{instanceId}");
            return true;
        }

        public bool JoinRitual(string seal)
        {
            string normalizedSeal = NormalizeSeal(seal);
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
            joinDeadline = Time.unscaledTime + JoinTimeout;
            SetJoinStatus(RitualJoinStatus.Joining);
            Broadcast($"QUERY|{pendingJoinSeal}");
            return true;
        }

        public void BeginJoinEntry()
        {
            pendingJoinSeal = string.Empty;
            SetJoinStatus(RitualJoinStatus.Waiting);
        }

        public void CancelJoin()
        {
            pendingJoinSeal = string.Empty;
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
                    if (foundationController.IsClientConnected || foundationController.IsClientConnecting)
                    {
                        ActiveSeal = joinedSeal;
                        SetJoinStatus(RitualJoinStatus.Joining);
                    }
                    else if (foundationController.StartClient(sender.Address.ToString(), port))
                    {
                        ActiveSeal = joinedSeal;
                        SetJoinStatus(RitualJoinStatus.Joining);
                    }
                    else
                    {
                        SetJoinStatus(RitualJoinStatus.CantJoin, "Connection rejected");
                    }
                }
                else if (parts[1] == "FULL" && IsJoining && parts[2] == pendingJoinSeal &&
                         parts.Length >= 4 && parts[3] != instanceId)
                {
                    pendingJoinSeal = string.Empty;
                    SetJoinStatus(RitualJoinStatus.CantJoin, "Ritual is full");
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
            bool isFull = NetworkPlayer.ActivePlayers.Count >= 8;
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
            if (JoinStatus != RitualJoinStatus.Joining || foundationController == null)
            {
                return;
            }

            if (foundationController.IsClientConnected)
            {
                pendingJoinSeal = string.Empty;
                Changed?.Invoke();
            }
            else if (!foundationController.IsClientConnecting && !string.IsNullOrEmpty(ActiveSeal))
            {
                ActiveSeal = string.Empty;
                SetJoinStatus(RitualJoinStatus.CantJoin, "Connection rejected");
            }
        }
    }
}
