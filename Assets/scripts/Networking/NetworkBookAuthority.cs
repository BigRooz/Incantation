using System;
using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Owns network authority for the single ritual Book.
    /// The server selects movement commands and presentation state, NetworkTransform replicates
    /// the resulting physical position and rotation, and observers consume synchronized Seat and
    /// presentation state without running independent Book simulation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(BookMover))]
    public sealed class NetworkBookAuthority : NetworkBehaviour
    {
        public const int NoTargetSeatId = -1;

        private readonly SyncVar<int> targetSeatId = new(NoTargetSeatId);
        private readonly SyncVar<uint> movementSequence = new(0);
        private readonly SyncVar<bool> isMoving = new(false);
        private readonly SyncVar<BookPresentationState> presentationState =
            new(BookPresentationState.Closed);

        private BookMover bookMover;
        private SeatManager seatManager;

        public bool IsNetworkSessionActive => IsServerInitialized || IsClientInitialized;
        public int TargetSeatId => targetSeatId.Value;
        public uint MovementSequence => movementSequence.Value;
        public bool IsMoving => isMoving.Value;
        public BookPresentationState PresentationState => presentationState.Value;
        public bool IsOpen => presentationState.Value == BookPresentationState.Open;

        public event Action<int, int> TargetSeatChanged;
        public event Action<uint, uint> MovementSequenceChanged;
        public event Action<bool, bool> MovementStateChanged;
        public event Action<BookPresentationState, BookPresentationState> PresentationStateChanged;

        private void Awake()
        {
            bookMover = GetComponent<BookMover>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            targetSeatId.OnChange += HandleTargetSeatIdChanged;
            movementSequence.OnChange += HandleMovementSequenceChanged;
            isMoving.OnChange += HandleMovementStateChanged;
            presentationState.OnChange += HandlePresentationStateChanged;
            ApplyTargetSeat(targetSeatId.Value);
        }

        public override void OnStopNetwork()
        {
            targetSeatId.OnChange -= HandleTargetSeatIdChanged;
            movementSequence.OnChange -= HandleMovementSequenceChanged;
            isMoving.OnChange -= HandleMovementStateChanged;
            presentationState.OnChange -= HandlePresentationStateChanged;
            base.OnStopNetwork();
        }

        /// <summary>
        /// Consumes a Book movement request. Only the server may turn it into physical movement.
        /// Clients return without moving so their NetworkTransform remains observer-driven.
        /// </summary>
        public bool TryMoveToSeat(Seat seat)
        {
            if (!IsNetworkSessionActive || seat == null)
                return false;

            if (!IsServerInitialized)
                return true;

            SeatManager resolvedSeatManager = ResolveSeatManager();
            int resolvedSeatId = resolvedSeatManager != null
                ? resolvedSeatManager.GetSeatId(seat)
                : NetworkPlayer.UnassignedSeatId;

            if (resolvedSeatId == NetworkPlayer.UnassignedSeatId)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkBookAuthority)} rejected movement to {seat.name} because it is not in SeatManager's configured physical order.",
                    this);
                return true;
            }

            targetSeatId.Value = resolvedSeatId;
            movementSequence.Value++;
            isMoving.Value = true;
            resolvedSeatManager.SetCurrentBookSeat(seat);
            bookMover.MoveToSeatAuthoritatively(seat);
            StartCoroutine(CompleteMovementAfterDuration(movementSequence.Value));
            return true;
        }

        /// <summary>
        /// Changes the synchronized Book presentation state on the server.
        /// Future page, mood, reaction, hand, and VFX presenters should consume this authority
        /// instead of creating local state.
        /// </summary>
        public bool SetPresentationState(BookPresentationState state)
        {
            if (!IsServerInitialized)
                return false;

            presentationState.Value = state;
            return true;
        }

        public bool SetOpen(bool open)
        {
            return SetPresentationState(open
                ? BookPresentationState.Open
                : BookPresentationState.Closed);
        }

        private void HandleTargetSeatIdChanged(int previousSeatId, int currentSeatId, bool asServer)
        {
            ApplyTargetSeat(currentSeatId);
            TargetSeatChanged?.Invoke(previousSeatId, currentSeatId);
        }

        private void HandleMovementSequenceChanged(
            uint previousSequence,
            uint currentSequence,
            bool asServer)
        {
            MovementSequenceChanged?.Invoke(previousSequence, currentSequence);
        }

        private void HandleMovementStateChanged(bool previousValue, bool currentValue, bool asServer)
        {
            MovementStateChanged?.Invoke(previousValue, currentValue);
        }

        private void HandlePresentationStateChanged(
            BookPresentationState previousState,
            BookPresentationState currentState,
            bool asServer)
        {
            PresentationStateChanged?.Invoke(previousState, currentState);
        }

        private void ApplyTargetSeat(int seatId)
        {
            SeatManager resolvedSeatManager = ResolveSeatManager();
            if (resolvedSeatManager == null)
                return;

            resolvedSeatManager.SetCurrentBookSeat(resolvedSeatManager.GetSeatById(seatId));
        }

        private SeatManager ResolveSeatManager()
        {
            if (seatManager == null)
                seatManager = FindFirstObjectByType<SeatManager>();

            return seatManager;
        }

        private IEnumerator CompleteMovementAfterDuration(uint expectedSequence)
        {
            float duration = Mathf.Max(0f, bookMover.moveDuration);
            if (duration > 0f)
                yield return new WaitForSeconds(duration);

            if (IsServerInitialized && movementSequence.Value == expectedSequence)
                isMoving.Value = false;
        }
    }

    /// <summary>
    /// Stable synchronized high-level state for Book presentation consumers.
    /// Extend this enum without renumbering shipped values when a mutually exclusive Book state
    /// is introduced; independent future reactions should add focused synchronized channels.
    /// </summary>
    public enum BookPresentationState : byte
    {
        Closed = 0,
        Open = 1
    }
}
