using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Incantation.Networking
{
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

        public event Action Changed;

        private void Awake()
        {
            Instance = this;
            foundationController = GetComponent<FishNetFoundationController>();
            OpenDirectorySocket();
        }

        private void Update()
        {
            ReceiveDirectoryMessages();

            if (HasActiveRitual && foundationController != null && foundationController.IsHostRunning &&
                Time.unscaledTime >= nextAnnouncementTime)
            {
                Broadcast($"ANNOUNCE|{ActiveSeal}|{foundationController.Port}|{instanceId}");
                nextAnnouncementTime = Time.unscaledTime + AnnouncementInterval;
            }

            if (IsJoining && Time.unscaledTime >= joinDeadline)
            {
                pendingJoinSeal = string.Empty;
                SetStatus("No active ritual bears that Seal.");
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
                SetStatus("Enter a valid four-character Ritual Seal.");
                return false;
            }

            pendingJoinSeal = normalizedSeal;
            joinDeadline = Time.unscaledTime + JoinTimeout;
            SetStatus("Seeking ritual...");
            Broadcast($"QUERY|{pendingJoinSeal}");
            return true;
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
                    Broadcast($"ANNOUNCE|{ActiveSeal}|{foundationController.Port}|{instanceId}");
                }
                else if (parts[1] == "ANNOUNCE" && IsJoining && parts[2] == pendingJoinSeal &&
                         parts.Length >= 5 && parts[4] != instanceId && ushort.TryParse(parts[3], out ushort port))
                {
                    string joinedSeal = pendingJoinSeal;
                    pendingJoinSeal = string.Empty;
                    if (foundationController.StartClient(sender.Address.ToString(), port))
                    {
                        ActiveSeal = joinedSeal;
                        SetStatus("Joining ritual...");
                    }
                    else
                    {
                        SetStatus("The ritual connection was rejected.");
                    }
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
    }
}
