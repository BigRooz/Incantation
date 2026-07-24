using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Owns the durable network identity and shared state for one connected player.
    /// FishNet owns its connection and object ownership; future lobby, Seat, character,
    /// Book, ritual, voice, and cosmetic systems consume this component through its
    /// read-only properties, mutation APIs, and events.
    /// SeatId is the single authoritative network Seat assignment. Character customization
    /// remains a future synchronized boundary.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public const int UnassignedSeatId = -1;
        public const int MaximumSeatId = 7;
        public const int DefaultCharacterCustomizationId = 0;

        private const int MaximumPriestNameLength = 32;

        private static readonly List<NetworkPlayer> activePlayers = new();

        private readonly SyncVar<bool> isHighPriest = new(false);
        private readonly SyncVar<string> priestName = new(string.Empty);
        private readonly SyncVar<LobbyPlayerState> lobbyPlayerState = new(LobbyPlayerState.NotSeated);
        private readonly SyncVar<ReadyState> readyState = new(ReadyState.NotReady);
        private readonly SyncVar<int> seatId = new(UnassignedSeatId);

        private int characterCustomizationId = DefaultCharacterCustomizationId;

        public static IReadOnlyList<NetworkPlayer> ActivePlayers => activePlayers;
        public static NetworkPlayer LocalPlayer { get; private set; }
        public static event Action<NetworkPlayer> ActivePlayerAdded;
        public static event Action<NetworkPlayer> ActivePlayerRemoved;

        public NetworkConnection Connection => Owner;
        public bool IsLocalPlayer => IsOwner;
        public bool IsHighPriest => isHighPriest.Value;
        public string PriestName => priestName.Value;
        public LobbyPlayerState LobbyPlayerState => lobbyPlayerState.Value;
        public ReadyState ReadyState => readyState.Value;
        public int SeatId => seatId.Value;
        public int CharacterCustomizationId => characterCustomizationId;
        public bool HasAssignedSeat => seatId.Value != UnassignedSeatId;

        public event Action<bool, bool> HighPriestChanged;
        public event Action<string, string> PriestNameChanged;
        public event Action<LobbyPlayerState, LobbyPlayerState> LobbyPlayerStateChanged;
        public event Action<ReadyState, ReadyState> ReadyStateChanged;
        public event Action<int, int> SeatIdChanged;
        public event Action<int, int> CharacterCustomizationIdChanged;
        public event Action ClientStarted;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            SubscribeToReplicatedState();

            if (!activePlayers.Contains(this))
            {
                activePlayers.Add(this);
                ActivePlayerAdded?.Invoke(this);
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
            {
                LocalPlayer = this;
            }

            ClientStarted?.Invoke();
            Debug.Log($"NetworkPlayer spawned. ConnectionId: {Owner.ClientId}, IsLocalPlayer: {IsLocalPlayer}.");
        }

        public override void OnStopNetwork()
        {
            UnsubscribeFromReplicatedState();
            if (activePlayers.Remove(this))
            {
                ActivePlayerRemoved?.Invoke(this);
            }

            if (LocalPlayer == this)
            {
                LocalPlayer = null;
            }

            base.OnStopNetwork();
        }

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

            string normalizedName = (value ?? string.Empty).Trim();
            if (normalizedName.Length > MaximumPriestNameLength)
            {
                normalizedName = normalizedName.Substring(0, MaximumPriestNameLength);
            }

            priestName.Value = normalizedName;
            return true;
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
        /// Stores the future validated character customization selection without synchronizing it.
        /// This method does not create or modify character presentation.
        /// </summary>
        public bool TrySetCharacterCustomizationId(int value)
        {
            if (!CanMutateServerOwnedState() || value < 0)
            {
                return false;
            }

            int previousValue = characterCustomizationId;
            if (previousValue == value)
            {
                return true;
            }

            characterCustomizationId = value;
            CharacterCustomizationIdChanged?.Invoke(previousValue, characterCustomizationId);
            return true;
        }

        private bool CanMutateReplicatedState()
        {
            return IsServerInitialized;
        }

        private bool CanMutateServerOwnedState()
        {
            return IsServerInitialized;
        }

        private void SubscribeToReplicatedState()
        {
            isHighPriest.OnChange += HandleHighPriestChanged;
            priestName.OnChange += HandlePriestNameChanged;
            lobbyPlayerState.OnChange += HandleLobbyPlayerStateChanged;
            readyState.OnChange += HandleReadyStateChanged;
            seatId.OnChange += HandleSeatIdChanged;
        }

        private void UnsubscribeFromReplicatedState()
        {
            isHighPriest.OnChange -= HandleHighPriestChanged;
            priestName.OnChange -= HandlePriestNameChanged;
            lobbyPlayerState.OnChange -= HandleLobbyPlayerStateChanged;
            readyState.OnChange -= HandleReadyStateChanged;
            seatId.OnChange -= HandleSeatIdChanged;
        }

        private void HandleHighPriestChanged(bool previousValue, bool currentValue, bool asServer)
        {
            if (ShouldPublishChange(asServer))
            {
                HighPriestChanged?.Invoke(previousValue, currentValue);
            }
        }

        private void HandlePriestNameChanged(string previousValue, string currentValue, bool asServer)
        {
            if (ShouldPublishChange(asServer))
            {
                PriestNameChanged?.Invoke(previousValue, currentValue);
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
                ReadyStateChanged?.Invoke(previousValue, currentValue);
            }
        }

        private void HandleSeatIdChanged(int previousValue, int currentValue, bool asServer)
        {
            if (ShouldPublishChange(asServer))
            {
                SeatIdChanged?.Invoke(previousValue, currentValue);
            }
        }

        private bool ShouldPublishChange(bool asServer)
        {
            return asServer || !IsServerInitialized;
        }
    }
}
