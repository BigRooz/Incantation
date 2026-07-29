using System;
using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Incantation.Networking.Ritual;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Owns the invisible network proxy for the single persistent ritual Book presentation.
    /// The server executes movement commands and copies the existing Book pose into NetworkTransform;
    /// client observers apply that proxy pose and synchronized Seat/presentation state to their
    /// existing BookModel without spawning or simulating another Book.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkBookAuthority : NetworkBehaviour
    {
        public const int NoTargetSeatId = -1;

        private readonly SyncVar<int> targetSeatId = new(NoTargetSeatId);
        private readonly SyncVar<uint> movementSequence = new(0);
        private readonly SyncVar<bool> isMoving = new(false);
        private readonly SyncVar<BookPresentationState> presentationState =
            new(BookPresentationState.Closed);

        [Header("Persistent Book Presentation")]
        [SerializeField] private BookMover bookMover;
        [SerializeField] private Transform presentationTransform;

        private SeatManager seatManager;

        public static NetworkBookAuthority Instance { get; private set; }
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
            Instance = this;
        }

        private void LateUpdate()
        {
            if (!IsNetworkSessionActive || presentationTransform == null)
                return;

            if (IsServerInitialized)
            {
                transform.SetPositionAndRotation(
                    presentationTransform.position,
                    presentationTransform.rotation);
            }
            else
            {
                presentationTransform.SetPositionAndRotation(
                    transform.position,
                    transform.rotation);
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            targetSeatId.OnChange += HandleTargetSeatIdChanged;
            movementSequence.OnChange += HandleMovementSequenceChanged;
            isMoving.OnChange += HandleMovementStateChanged;
            presentationState.OnChange += HandlePresentationStateChanged;

            if (IsServerInitialized && presentationTransform != null)
            {
                transform.SetPositionAndRotation(
                    presentationTransform.position,
                    presentationTransform.rotation);
            }

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

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Executes an authoritative stable-data movement command. Target and player selection
        /// have already occurred in NetworkRitualAuthority.
        /// </summary>
        public bool TryExecuteMovementCommand(RitualBookMovementCommand command)
        {
            if (!IsServerInitialized || bookMover == null)
                return false;

            SeatManager resolvedSeatManager = ResolveSeatManager();
            Seat targetSeat = resolvedSeatManager != null
                ? resolvedSeatManager.GetSeatById(command.TargetSeatId)
                : null;
            if (targetSeat == null)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkBookAuthority)} rejected movement command {command.MovementSequence} because target Seat ID {command.TargetSeatId} is not configured.",
                    this);
                return false;
            }

            uint expectedMovementSequence = movementSequence.Value + 1;
            if (movementSequence.Value == uint.MaxValue ||
                command.MovementSequence != expectedMovementSequence)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkBookAuthority)} rejected movement command {command.MovementSequence}; expected sequence {expectedMovementSequence}.",
                    this);
                return false;
            }

            targetSeatId.Value = command.TargetSeatId;
            movementSequence.Value = command.MovementSequence;
            isMoving.Value = true;
            resolvedSeatManager.SetCurrentBookSeat(targetSeat);
            bookMover.MoveToSeatAuthoritatively(targetSeat);
            StartCoroutine(CompleteMovementAfterDuration(command.MovementSequence));
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
