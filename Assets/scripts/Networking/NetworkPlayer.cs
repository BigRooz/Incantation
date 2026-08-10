using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Incantation.Character;
using Incantation.Networking.Ritual;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Owns the durable network identity and shared state for one connected player.
    /// FishNet owns its connection and object ownership; future lobby, Seat, character,
    /// Book, ritual, voice, and cosmetic systems consume this component through its
    /// read-only properties, mutation APIs, and events.
    /// SeatId is the single authoritative network Seat assignment. AppearanceSlots is the
    /// server-owned, data-only appearance model.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public const int MaximumCircleMembers = 8;
        public const int UnassignedSeatId = -1;
        public const int MaximumSeatId = 7;

        public const int MaximumPriestNameLength = 24;
        private const int MaximumAppearanceValueId = 65535;

        private static readonly List<NetworkPlayer> activePlayers = new();

        private readonly SyncVar<bool> isHighPriest = new(false);
        private readonly SyncVar<bool> isCircleMember = new(false);
        private readonly SyncVar<string> playerId = new(string.Empty);
        private readonly SyncVar<string> priestName = new(string.Empty);
        private readonly SyncVar<LobbyPlayerState> lobbyPlayerState = new(LobbyPlayerState.NotSeated);
        private readonly SyncVar<ReadyState> readyState = new(ReadyState.NotReady);
        private readonly SyncVar<int> seatId = new(UnassignedSeatId);
        private readonly SyncList<AppearanceSlotValue> appearanceSlots = new();
        private bool isReadyRequestPending;
        private bool isPriestNameRequestPending;
        private bool ritualStartAuthorized;
        private uint localVoiceSubmissionTurnSequence;
        private uint localVoiceSubmissionSequence;

        public static IReadOnlyList<NetworkPlayer> ActivePlayers => activePlayers;
        public static NetworkPlayer LocalPlayer { get; private set; }
        public static event Action<NetworkPlayer> ActivePlayerAdded;
        public static event Action<NetworkPlayer> ActivePlayerRemoved;
        public static event Action<NetworkPlayer> LocalPlayerCreated;
        public static event Action CircleRosterChanged;
        public static event Action RitualStartAuthorized;

        public NetworkConnection Connection => Owner;
        public bool IsLocalPlayer => IsOwner;
        public bool IsHighPriest => isHighPriest.Value;
        public bool IsCircleMember => isCircleMember.Value;
        public string PlayerId => playerId.Value;
        public string PriestName => priestName.Value;
        public LobbyPlayerState LobbyPlayerState => lobbyPlayerState.Value;
        public ReadyState ReadyState => readyState.Value;
        public bool IsReady => readyState.Value == ReadyState.Ready;
        public int SeatId => seatId.Value;
        public IReadOnlyList<AppearanceSlotValue> AppearanceSlots => appearanceSlots;
        public bool HasAssignedSeat => seatId.Value != UnassignedSeatId;
        public bool IsPriestNameRequestPending => isPriestNameRequestPending;

        public event Action<bool, bool> HighPriestChanged;
        public event Action<bool, bool> CircleMembershipChanged;
        public event Action<string, string> PriestNameChanged;
        public event Action<LobbyPlayerState, LobbyPlayerState> LobbyPlayerStateChanged;
        public event Action<ReadyState, ReadyState> ReadyStateChanged;
        public event Action<int, int> SeatIdChanged;
        public event Action<AppearanceSlotValue> AppearanceSlotChanged;
        public event Action ClientStarted;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            SubscribeToReplicatedState();

            if (!activePlayers.Contains(this))
            {
                activePlayers.Add(this);
                ActivePlayerAdded?.Invoke(this);
                CircleRosterChanged?.Invoke();
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (string.IsNullOrEmpty(playerId.Value))
            {
                playerId.Value = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(priestName.Value))
            {
                priestName.Value = $"Priest {Owner.ClientId + 1}";
            }

            if (isCircleMember.Value)
            {
                return;
            }

            isCircleMember.Value = true;
            Debug.Log(
                $"[Circle] Member registered. Count: {CircleMemberCount}/{MaximumCircleMembers}.",
                this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
            {
                LocalPlayer = this;
                Debug.Log($"Local NetworkPlayer created. ConnectionId: {Owner.ClientId}.", this);
                LocalPlayerCreated?.Invoke(this);

                if (isCircleMember.Value)
                {
                    Debug.Log("[Circle] Local membership confirmed.", this);
                }
            }

            ClientStarted?.Invoke();
            Debug.Log($"NetworkPlayer spawned. ConnectionId: {Owner.ClientId}, IsLocalPlayer: {IsLocalPlayer}.");
            CircleRosterChanged?.Invoke();
            Debug.Log(
                $"[Circle] Synchronized member count: {CircleMemberCount}/{MaximumCircleMembers}.",
                this);
        }

        public override void OnStopServer()
        {
            if (isCircleMember.Value)
            {
                isCircleMember.Value = false;
                Debug.Log(
                    $"[Circle] Member removed. Count: {CircleMemberCount}/{MaximumCircleMembers}.",
                    this);
            }

            base.OnStopServer();
        }

        public override void OnStopNetwork()
        {
            UnsubscribeFromReplicatedState();
            if (activePlayers.Remove(this))
            {
                ActivePlayerRemoved?.Invoke(this);
                CircleRosterChanged?.Invoke();
            }

            if (LocalPlayer == this)
            {
                LocalPlayer = null;
            }

            isReadyRequestPending = false;
            base.OnStopNetwork();
        }

        public static int CircleMemberCount
        {
            get
            {
                int count = 0;
                foreach (NetworkPlayer player in activePlayers)
                {
                    if (player != null && player.IsCircleMember)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Sends locally recognized speech through this connection-owned player identity.
        /// The payload contains no trusted player ID; the server derives identity from sender.
        /// </summary>
        public bool RequestRitualVoiceSubmission(string recognizedText)
        {
            if (!IsOwner ||
                string.IsNullOrWhiteSpace(recognizedText) ||
                recognizedText.Length > RitualVoiceSubmission.MaximumRecognizedTextLength)
            {
                return false;
            }

            NetworkRitualAuthority authority = NetworkRitualAuthority.Instance;
            if (authority == null || !authority.IsNetworkSessionActive)
                return false;

            RitualSnapshot ritualSnapshot = authority.Snapshot;
            uint currentTurnSequence = ritualSnapshot.Turn.SequenceId.Value;
            if (currentTurnSequence == 0)
                return false;

            if (localVoiceSubmissionTurnSequence != currentTurnSequence)
            {
                localVoiceSubmissionTurnSequence = currentTurnSequence;
                localVoiceSubmissionSequence = 0;
            }

            if (localVoiceSubmissionSequence == uint.MaxValue)
                return false;

            localVoiceSubmissionSequence++;
            RitualVoiceSubmission submission = new(
                ritualSnapshot.SequenceId.Value,
                currentTurnSequence,
                localVoiceSubmissionSequence,
                recognizedText,
                Time.realtimeSinceStartupAsDouble);

            if (IsServerInitialized)
            {
                return authority.TryAcceptVoiceSubmission(Owner, submission);
            }

            SubmitRitualVoiceServerRpc(submission);
            return true;
        }

        [ServerRpc]
        private void SubmitRitualVoiceServerRpc(
            RitualVoiceSubmission submission,
            NetworkConnection sender = null)
        {
            NetworkRitualAuthority authority = NetworkRitualAuthority.Instance;
            if (authority == null)
            {
                Debug.LogWarning(
                    "[RitualAuthority]\n" +
                    "Voice Submission Rejected\n" +
                    "Reason = NetworkRitualAuthority is unavailable.",
                    this);
                return;
            }

            authority.TryAcceptVoiceSubmission(sender, submission);
        }

        public static int ReadyCircleMemberCount
        {
            get
            {
                int count = 0;
                foreach (NetworkPlayer player in activePlayers)
                {
                    if (player != null && player.IsCircleMember && player.IsReady)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public static bool IsLocalPlayerCircleMember =>
            LocalPlayer != null &&
            activePlayers.Contains(LocalPlayer) &&
            LocalPlayer.IsCircleMember;

        /// <summary>
        /// Changes the replicated host role. Only the initialized server may mutate it.
        /// </summary>
        public bool TrySetHighPriest(bool value)
        {
            if (!CanMutateReplicatedState())
            {
                return false;
            }

            isHighPriest.Value = value;
            return true;
        }

        /// <summary>
        /// Changes the replicated display name after trimming and applying its storage limit.
        /// Only the initialized server may mutate it.
        /// </summary>
        public bool TrySetPriestName(string value)
        {
            if (!CanMutateReplicatedState())
            {
                return false;
            }

            if (!TryNormalizePriestName(value, out string normalizedName, out _))
            {
                return false;
            }

            priestName.Value = normalizedName;
            return true;
        }

        public bool RequestPriestName(string value)
        {
            if (!IsOwner || isPriestNameRequestPending ||
                !TryNormalizePriestName(value, out string normalizedName, out _))
            {
                return false;
            }

            isPriestNameRequestPending = true;
            RequestPriestNameServerRpc(normalizedName);
            return true;
        }

        public static bool TryNormalizePriestName(
            string value,
            out string normalizedName,
            out string validationMessage)
        {
            normalizedName = (value ?? string.Empty).Trim();
            validationMessage = string.Empty;
            if (normalizedName.Length == 0)
            {
                validationMessage = "Enter a Priest Name";
                return false;
            }

            if (normalizedName.Length > MaximumPriestNameLength)
            {
                validationMessage = "Maximum 24 characters";
                return false;
            }

            for (int i = 0; i < normalizedName.Length; i++)
            {
                char character = normalizedName[i];
                if (!char.IsLetterOrDigit(character) &&
                    character != ' ' && character != '\'' && character != '-')
                {
                    validationMessage = "Use letters, numbers, spaces, apostrophes, or hyphens";
                    return false;
                }
            }

            return true;
        }

        [ServerRpc]
        private void RequestPriestNameServerRpc(string value)
        {
            TrySetPriestName(value);
        }

        /// <summary>
        /// Changes the replicated lobby lifecycle state. Transition policy remains owned by
        /// the lobby system and is intentionally not implemented here.
        /// </summary>
        public bool TrySetLobbyPlayerState(LobbyPlayerState value)
        {
            if (!CanMutateReplicatedState())
            {
                return false;
            }

            lobbyPlayerState.Value = value;
            return true;
        }

        /// <summary>
        /// Changes the replicated ready state. Ready-check rules remain owned by the lobby.
        /// </summary>
        public bool TrySetReadyState(ReadyState value)
        {
            if (!CanMutateReplicatedState())
            {
                return false;
            }

            readyState.Value = value;
            return true;
        }

        /// <summary>
        /// Requests that the server toggle the owning Circle member's authoritative Ready state.
        /// No local prediction or direct client mutation is performed.
        /// </summary>
        public bool RequestToggleReady()
        {
            if (!IsOwner || !IsCircleMember || isReadyRequestPending)
            {
                return false;
            }

            isReadyRequestPending = true;
            if (IsServerInitialized)
            {
                bool changed = TryToggleReady();
                if (!changed)
                {
                    isReadyRequestPending = false;
                }

                return changed;
            }

            RequestToggleReadyServerRpc();
            return true;
        }

        [ServerRpc]
        private void RequestToggleReadyServerRpc()
        {
            TryToggleReady();
        }

        private bool TryToggleReady()
        {
            if (!CanMutateReplicatedState() || !isCircleMember.Value)
            {
                return false;
            }

            readyState.Value = readyState.Value == ReadyState.Ready
                ? ReadyState.NotReady
                : ReadyState.Ready;
            return true;
        }

        /// <summary>
        /// Requests an authoritative ritual start from the hosting player's owned identity.
        /// The server verifies the request came from its local Host connection and that every
        /// currently connected Circle member is Ready before notifying every observer.
        /// </summary>
        public bool RequestRitualStart()
        {
            RitualSealService service = RitualSealService.Instance;
            FishNetFoundationController foundation = FishNetFoundationController.Instance;
            if (!IsOwner ||
                service == null ||
                !service.IsHostingRitual ||
                foundation == null ||
                !foundation.IsHostRunning)
            {
                Debug.LogWarning("Ritual start rejected locally: only the active Host may request it.", this);
                return false;
            }

            if (IsServerInitialized)
            {
                return TryAuthorizeRitualStart();
            }

            RequestRitualStartServerRpc();
            return true;
        }

        [ServerRpc]
        private void RequestRitualStartServerRpc()
        {
            TryAuthorizeRitualStart();
        }

        private bool TryAuthorizeRitualStart()
        {
            FishNetFoundationController foundation = FishNetFoundationController.Instance;
            if (!CanMutateReplicatedState() ||
                ritualStartAuthorized ||
                Owner == null ||
                !Owner.IsLocalClient ||
                foundation == null ||
                !foundation.IsHostRunning)
            {
                Debug.LogWarning("Ritual start rejected by the server: the requester is not the active Host.", this);
                return false;
            }

            int participatingPriestCount = 0;
            foreach (NetworkPlayer player in activePlayers)
            {
                if (player == null || !player.IsCircleMember)
                {
                    continue;
                }

                participatingPriestCount++;
                if (!player.IsReady)
                {
                    Debug.LogWarning(
                        $"Ritual start rejected by the server: connected Priest {player.Owner.ClientId} is not Ready.",
                        this);
                    return false;
                }
            }

            if (participatingPriestCount == 0)
            {
                Debug.LogWarning("Ritual start rejected by the server: no participating Priests are connected.", this);
                return false;
            }

            NetworkRitualAuthority authority = NetworkRitualAuthority.Instance;
            if (authority == null || !authority.TryStartAuthoritativeRitual())
            {
                Debug.LogWarning(
                    "Ritual start rejected by the server: NetworkRitualAuthority could not begin authoritative progression.",
                    this);
                return false;
            }

            ritualStartAuthorized = true;
            BroadcastRitualStartObserversRpc();
            return true;
        }

        [ObserversRpc]
        private void BroadcastRitualStartObserversRpc()
        {
            Debug.Log("Authoritative ritual start received.", this);
            RitualStartAuthorized?.Invoke();
        }

        /// <summary>
        /// Requests a Seat assignment for the owning player. The server validates that the
        /// Seat is not already assigned before changing the replicated authoritative value.
        /// </summary>
        public bool RequestSeatId(int value)
        {
            if (!IsValidSeatId(value) || !IsOwner)
            {
                return false;
            }

            if (IsServerInitialized)
            {
                return TrySetSeatId(value);
            }

            RequestSeatIdServerRpc(value);
            return true;
        }

        /// <summary>
        /// Changes the synchronized Seat assignment. Only the initialized server may mutate
        /// this value, and an assigned Seat ID may belong to only one active NetworkPlayer.
        /// </summary>
        public bool TrySetSeatId(int value)
        {
            if (!CanMutateReplicatedState() || !IsValidSeatId(value))
            {
                return false;
            }

            if (value != UnassignedSeatId && IsSeatIdAssignedToAnotherPlayer(value))
            {
                return false;
            }

            seatId.Value = value;
            return true;
        }

        /// <summary>
        /// Returns the active NetworkPlayer assigned to the supplied Seat ID, if any.
        /// </summary>
        public static NetworkPlayer FindBySeatId(int value)
        {
            if (value == UnassignedSeatId)
            {
                return null;
            }

            foreach (NetworkPlayer player in activePlayers)
            {
                if (player != null && player.SeatId == value)
                {
                    return player;
                }
            }

            return null;
        }

        [ServerRpc]
        private void RequestSeatIdServerRpc(int value)
        {
            TrySetSeatId(value);
        }

        private bool IsSeatIdAssignedToAnotherPlayer(int value)
        {
            foreach (NetworkPlayer player in activePlayers)
            {
                if (player != null && player != this && player.SeatId == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidSeatId(int value)
        {
            return value >= UnassignedSeatId && value <= MaximumSeatId;
        }

        /// <summary>
        /// Requests one appearance-slot change for the owning player. The server validates and
        /// stores only the lightweight slot/value pair.
        /// </summary>
        public bool RequestAppearanceSlot(AppearanceSlot slot, int valueId)
        {
            if (!IsOwner || !IsValidAppearanceValue(valueId))
            {
                return false;
            }

            if (IsServerInitialized)
            {
                return TrySetAppearanceSlot(slot, valueId);
            }

            RequestAppearanceSlotServerRpc(slot, valueId);
            return true;
        }

        /// <summary>
        /// Changes one authoritative appearance slot. Setting an existing slot produces one
        /// SyncList delta rather than rebuilding or retransmitting unrelated choices.
        /// </summary>
        public bool TrySetAppearanceSlot(AppearanceSlot slot, int valueId)
        {
            if (!CanMutateReplicatedState() || !IsValidAppearanceValue(valueId))
            {
                return false;
            }

            for (int i = 0; i < appearanceSlots.Count; i++)
            {
                if (appearanceSlots[i].Slot != slot)
                {
                    continue;
                }

                if (appearanceSlots[i].ValueId != valueId)
                {
                    appearanceSlots[i] = new AppearanceSlotValue(slot, valueId);
                }

                return true;
            }

            appearanceSlots.Add(new AppearanceSlotValue(slot, valueId));
            return true;
        }

        public bool TryGetAppearanceSlot(AppearanceSlot slot, out int valueId)
        {
            for (int i = 0; i < appearanceSlots.Count; i++)
            {
                if (appearanceSlots[i].Slot == slot)
                {
                    valueId = appearanceSlots[i].ValueId;
                    return true;
                }
            }

            valueId = default;
            return false;
        }

        [ServerRpc]
        private void RequestAppearanceSlotServerRpc(AppearanceSlot slot, int valueId)
        {
            TrySetAppearanceSlot(slot, valueId);
        }

        private static bool IsValidAppearanceValue(int valueId)
        {
            return valueId >= 0 && valueId <= MaximumAppearanceValueId;
        }

        private bool CanMutateReplicatedState()
        {
            return IsServerInitialized;
        }

        private void SubscribeToReplicatedState()
        {
            isHighPriest.OnChange += HandleHighPriestChanged;
            isCircleMember.OnChange += HandleCircleMembershipChanged;
            playerId.OnChange += HandlePlayerIdChanged;
            priestName.OnChange += HandlePriestNameChanged;
            lobbyPlayerState.OnChange += HandleLobbyPlayerStateChanged;
            readyState.OnChange += HandleReadyStateChanged;
            seatId.OnChange += HandleSeatIdChanged;
            appearanceSlots.OnChange += HandleAppearanceSlotsChanged;
        }

        private void UnsubscribeFromReplicatedState()
        {
            isHighPriest.OnChange -= HandleHighPriestChanged;
            isCircleMember.OnChange -= HandleCircleMembershipChanged;
            playerId.OnChange -= HandlePlayerIdChanged;
            priestName.OnChange -= HandlePriestNameChanged;
            lobbyPlayerState.OnChange -= HandleLobbyPlayerStateChanged;
            readyState.OnChange -= HandleReadyStateChanged;
            seatId.OnChange -= HandleSeatIdChanged;
            appearanceSlots.OnChange -= HandleAppearanceSlotsChanged;
        }

        private void HandleHighPriestChanged(bool previousValue, bool currentValue, bool asServer)
        {
            if (ShouldPublishChange(asServer))
            {
                HighPriestChanged?.Invoke(previousValue, currentValue);
            }
        }

        private void HandleCircleMembershipChanged(bool previousValue, bool currentValue, bool asServer)
        {
            if (ShouldPublishChange(asServer))
            {
                CircleMembershipChanged?.Invoke(previousValue, currentValue);
            }

            CircleRosterChanged?.Invoke();

            if (!asServer)
            {
                Debug.Log(
                    $"[Circle] Synchronized member count: {CircleMemberCount}/{MaximumCircleMembers}.",
                    this);
            }

            if (currentValue && IsOwner)
            {
                Debug.Log("[Circle] Local membership confirmed.", this);
            }
        }

        private void HandlePlayerIdChanged(string previousValue, string currentValue, bool asServer)
        {
            CircleRosterChanged?.Invoke();
        }

        private void HandlePriestNameChanged(string previousValue, string currentValue, bool asServer)
        {
            if (!asServer && IsOwner)
            {
                isPriestNameRequestPending = false;
            }

            if (ShouldPublishChange(asServer))
            {
                PriestNameChanged?.Invoke(previousValue, currentValue);
                CircleRosterChanged?.Invoke();
            }
        }

        private void HandleLobbyPlayerStateChanged(
            LobbyPlayerState previousValue,
            LobbyPlayerState currentValue,
            bool asServer)
        {
            if (ShouldPublishChange(asServer))
            {
                LobbyPlayerStateChanged?.Invoke(previousValue, currentValue);
            }
        }

        private void HandleReadyStateChanged(ReadyState previousValue, ReadyState currentValue, bool asServer)
        {
            if (ShouldPublishChange(asServer))
            {
                if (IsOwner)
                {
                    isReadyRequestPending = false;
                }

                ReadyStateChanged?.Invoke(previousValue, currentValue);
                CircleRosterChanged?.Invoke();
            }
        }

        private void HandleSeatIdChanged(int previousValue, int currentValue, bool asServer)
        {
            if (ShouldPublishChange(asServer))
            {
                SeatIdChanged?.Invoke(previousValue, currentValue);
            }
        }

        private void HandleAppearanceSlotsChanged(
            SyncListOperation operation,
            int index,
            AppearanceSlotValue previousValue,
            AppearanceSlotValue currentValue,
            bool asServer)
        {
            if ((operation == SyncListOperation.Add || operation == SyncListOperation.Set) &&
                ShouldPublishChange(asServer))
            {
                AppearanceSlotChanged?.Invoke(currentValue);
            }
        }

        private bool ShouldPublishChange(bool asServer)
        {
            return asServer || !IsServerInitialized;
        }
    }
}
