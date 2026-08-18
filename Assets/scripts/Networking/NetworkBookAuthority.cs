using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Incantation.Networking.Ritual;
using Incantation.UI;
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
        private readonly SyncVar<uint> presentationMovementSequence = new(0);
        private readonly SyncVar<string> presentedRitualSessionId = new(string.Empty);
        private readonly SyncVar<uint> presentedRitualSequence = new(0);
        private readonly SyncVar<string> presentedWinnerPlayerId = new(string.Empty);
        private readonly SyncVar<bool> isPresentationMoving = new(false);
        private readonly SyncVar<BookPresentationState> presentationState =
            new(BookPresentationState.Closed);

        [Header("Persistent Book Presentation")]
        [SerializeField] private BookMover bookMover;
        [SerializeField] private Transform presentationTransform;

        private SeatManager seatManager;
        private NetworkRitualAuthority ritualAuthority;
        private RitualBookMovementCommand activeMovementCommand;
        private bool hasActiveMovementCommand;
        private bool visibleBookStateCaptured;
        private bool lastVisibleBookActiveSelf;
        private bool lastVisibleBookActiveInHierarchy;
        private Vector3 lobbyPosition;
        private Quaternion lobbyRotation;
        private bool hasLobbyPose;
        private bool usesLocalLobbyPresentation = true;

        public static NetworkBookAuthority Instance { get; private set; }
        public bool IsNetworkSessionActive => IsServerInitialized || IsClientInitialized;
        public int TargetSeatId => targetSeatId.Value;
        public uint MovementSequence => movementSequence.Value;
        public bool IsMoving => isMoving.Value;
        public uint PresentationMovementSequence => presentationMovementSequence.Value;
        public string PresentedRitualSessionId => presentedRitualSessionId.Value;
        public uint PresentedRitualSequence => presentedRitualSequence.Value;
        public string PresentedWinnerPlayerId => presentedWinnerPlayerId.Value;
        public bool IsPresentationMoving => isPresentationMoving.Value;
        public BookPresentationState PresentationState => presentationState.Value;
        public bool IsOpen => presentationState.Value == BookPresentationState.Open;

        public event Action<int, int> TargetSeatChanged;
        public event Action<uint, uint> MovementSequenceChanged;
        public event Action<bool, bool> MovementStateChanged;
        public event Action<BookPresentationState, BookPresentationState> PresentationStateChanged;

        private void Awake()
        {
            Instance = this;
            EnsureGameOverPresentationComponents();
            CaptureLobbyPose();
            AlignProxyToVisibleBook("Awake before FishNet scene-object initialization");
            CaptureVisibleBookActiveState();
            LogVisibleBookResolved("Awake");
        }

        private void EnsureGameOverPresentationComponents()
        {
            if (GetComponent<RitualGameOverPresenter>() == null)
                gameObject.AddComponent<RitualGameOverPresenter>();

            if (GetComponent<NetworkGameOverPresentationController>() == null)
                gameObject.AddComponent<NetworkGameOverPresentationController>();

            if (GetComponent<NetworkPostGameLifecycleController>() == null)
                gameObject.AddComponent<NetworkPostGameLifecycleController>();
        }

        private void CaptureLobbyPose()
        {
            if (presentationTransform == null)
                return;

            lobbyPosition = presentationTransform.position;
            lobbyRotation = presentationTransform.rotation;
            hasLobbyPose = true;
        }

        private void LateUpdate()
        {
            LogVisibleBookActiveStateChange();

            if (usesLocalLobbyPresentation || !IsNetworkSessionActive ||
                presentationTransform == null)
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

            if (IsServerInitialized)
                AlignProxyToVisibleBook("OnStartNetwork server confirmation");

            ApplyTargetSeat(targetSeatId.Value);
            LogVisibleBookResolved("OnStartNetwork");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            AlignProxyToVisibleBook("OnStartServer");
            LogVisibleBookResolved("OnStartServer");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            LogVisibleBookResolved("OnStartClient");
        }

        public override void OnStopNetwork()
        {
            targetSeatId.OnChange -= HandleTargetSeatIdChanged;
            movementSequence.OnChange -= HandleMovementSequenceChanged;
            isMoving.OnChange -= HandleMovementStateChanged;
            presentationState.OnChange -= HandlePresentationStateChanged;
            EnterLocalLobbyPresentation();
            base.OnStopNetwork();
        }

        /// <summary>
        /// Gives this process ownership of its existing visible Book for lobby and customization
        /// presentation. No network state or ritual movement is produced by this transition.
        /// </summary>
        public void EnterLocalLobbyPresentation()
        {
            usesLocalLobbyPresentation = true;
            RestoreLocalLobbyPose();
        }

        /// <summary>
        /// Reconciles the visible Book and proxy to the authored lobby pose before the existing
        /// server-authoritative ritual movement pipeline resumes.
        /// </summary>
        public void EnterSharedBookPresentation()
        {
            if (!hasLobbyPose || presentationTransform == null)
                return;

            ResetBookRotationToFront();
            presentationTransform.SetPositionAndRotation(lobbyPosition, lobbyRotation);

            if (IsServerInitialized)
                transform.SetPositionAndRotation(lobbyPosition, lobbyRotation);

            usesLocalLobbyPresentation = false;
        }

        private void RestoreLocalLobbyPose()
        {
            if (!hasLobbyPose || presentationTransform == null)
                return;

            ResetBookRotationToFront();
            presentationTransform.SetPositionAndRotation(lobbyPosition, lobbyRotation);
        }

        private void ResetBookRotationToFront()
        {
            BookRotationController rotationController =
                presentationTransform != null
                    ? presentationTransform.GetComponentInParent<BookRotationController>()
                    : null;
            rotationController?.ResetToFrontImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void AlignProxyToVisibleBook(string reason)
        {
            if (presentationTransform == null)
            {
                Debug.LogWarning(
                    "[SharedBook]\n" +
                    "Visible Book resolution failed\n" +
                    $"Reason = {reason}\n" +
                    "PresentationTransform = Missing",
                    this);
                return;
            }

            transform.SetPositionAndRotation(
                presentationTransform.position,
                presentationTransform.rotation);
        }

        private void CaptureVisibleBookActiveState()
        {
            if (presentationTransform == null)
                return;

            visibleBookStateCaptured = true;
            lastVisibleBookActiveSelf =
                presentationTransform.gameObject.activeSelf;
            lastVisibleBookActiveInHierarchy =
                presentationTransform.gameObject.activeInHierarchy;
        }

        private void LogVisibleBookActiveStateChange()
        {
            if (presentationTransform == null)
                return;

            bool activeSelf = presentationTransform.gameObject.activeSelf;
            bool activeInHierarchy =
                presentationTransform.gameObject.activeInHierarchy;
            if (visibleBookStateCaptured &&
                activeSelf == lastVisibleBookActiveSelf &&
                activeInHierarchy == lastVisibleBookActiveInHierarchy)
            {
                return;
            }

            string previousState = visibleBookStateCaptured
                ? $"ActiveSelf={lastVisibleBookActiveSelf}, ActiveInHierarchy={lastVisibleBookActiveInHierarchy}"
                : "Uncaptured";
            Debug.LogWarning(
                "[SharedBook]\n" +
                "Visible Book active state changed\n" +
                $"Previous = {previousState}\n" +
                $"ActiveSelf = {activeSelf}\n" +
                $"ActiveInHierarchy = {activeInHierarchy}\n" +
                $"ProxyActive = {gameObject.activeInHierarchy}",
                this);
            CaptureVisibleBookActiveState();
        }

        private void LogVisibleBookResolved(string lifecycle)
        {
            if (presentationTransform == null)
                return;

            GameObject visibleBook = presentationTransform.gameObject;
            Debug.Log(
                "[SharedBook]\n" +
                "Visible Book resolved\n" +
                $"Lifecycle = {lifecycle}\n" +
                $"Name = {visibleBook.name}\n" +
                $"ActiveSelf = {visibleBook.activeSelf}\n" +
                $"ActiveInHierarchy = {visibleBook.activeInHierarchy}\n" +
                $"Parent = {(presentationTransform.parent != null ? presentationTransform.parent.name : "none")}\n" +
                $"ContainsNetworkObject = {visibleBook.GetComponent<NetworkObject>() != null}\n" +
                $"ContainsNetworkTransform = {visibleBook.GetComponent<NetworkTransform>() != null}\n" +
                $"ProxyActiveSelf = {gameObject.activeSelf}\n" +
                $"ProxyActiveInHierarchy = {gameObject.activeInHierarchy}\n" +
                $"ServerInitialized = {IsServerInitialized}\n" +
                $"ClientInitialized = {IsClientInitialized}",
                this);
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

            IReadOnlyList<Seat> physicalSeatOrder = GetPhysicalSeatOrder(
                resolvedSeatManager);
            Seat previousSeat = resolvedSeatManager.currentBookSeat;
            bool inferredInitialSeat = previousSeat == null &&
                TryResolvePhysicalStartSeat(
                    presentationTransform != null
                        ? presentationTransform.position
                        : transform.position,
                    physicalSeatOrder,
                    out previousSeat);
            targetSeatId.Value = command.TargetSeatId;
            movementSequence.Value = command.MovementSequence;
            isMoving.Value = true;
            activeMovementCommand = command;
            hasActiveMovementCommand = true;
            resolvedSeatManager.SetCurrentBookSeat(targetSeat);
            List<Seat> route = BuildPhysicalSeatRoute(
                previousSeat,
                targetSeat,
                physicalSeatOrder);
            if (inferredInitialSeat && route.Count > 0)
                route.Insert(0, previousSeat);
            Action completion = () => CompleteRitualMovement(command.MovementSequence);
            if (route.Count == 0 ||
                !bookMover.MoveAlongSeatRouteAuthoritatively(
                    route,
                    physicalSeatOrder,
                    completion))
            {
                bookMover.MoveToSeatAuthoritatively(targetSeat, completion);
            }
            return true;
        }

        /// <summary>
        /// Moves the persistent Book for an authoritative game-over presentation. This path is
        /// deliberately separate from ritual turn movement and never creates a Book arrival report.
        /// </summary>
        public bool TryExecuteWinnerPresentationMovement(
            string ritualSessionId,
            uint ritualSequence,
            string winnerPlayerId,
            int winnerSeatId)
        {
            if (!IsServerInitialized || bookMover == null ||
                string.IsNullOrEmpty(ritualSessionId) || ritualSequence == 0 ||
                string.IsNullOrEmpty(winnerPlayerId))
            {
                return false;
            }

            NetworkRitualAuthority resolvedRitualAuthority = ResolveRitualAuthority();
            if (resolvedRitualAuthority == null)
                return false;

            RitualSnapshot snapshot = resolvedRitualAuthority.Snapshot;
            if (!snapshot.IsGameOver ||
                snapshot.Phase != RitualPhase.Completed ||
                !string.Equals(snapshot.RitualSessionId, ritualSessionId, StringComparison.Ordinal) ||
                snapshot.SequenceId.Value != ritualSequence ||
                !string.Equals(snapshot.WinnerPlayerId, winnerPlayerId, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"{nameof(NetworkBookAuthority)} rejected winner presentation movement because it does not match the authoritative completed ritual snapshot.",
                    this);
                return false;
            }

            if (string.Equals(
                    presentedRitualSessionId.Value,
                    ritualSessionId,
                    StringComparison.Ordinal) &&
                presentedRitualSequence.Value == ritualSequence &&
                string.Equals(
                    presentedWinnerPlayerId.Value,
                    winnerPlayerId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            SeatManager resolvedSeatManager = ResolveSeatManager();
            Seat targetSeat = resolvedSeatManager != null
                ? resolvedSeatManager.GetSeatById(winnerSeatId)
                : null;
            if (targetSeat == null || presentationMovementSequence.Value == uint.MaxValue)
            {
                Debug.LogWarning(
                    $"{nameof(NetworkBookAuthority)} rejected winner presentation movement because Seat ID {winnerSeatId} is unavailable or its sequence is exhausted.",
                    this);
                return false;
            }

            uint nextPresentationSequence = presentationMovementSequence.Value + 1;
            presentedRitualSessionId.Value = ritualSessionId;
            presentedRitualSequence.Value = ritualSequence;
            presentedWinnerPlayerId.Value = winnerPlayerId;
            presentationMovementSequence.Value = nextPresentationSequence;
            targetSeatId.Value = winnerSeatId;
            isPresentationMoving.Value = true;
            isMoving.Value = true;
            resolvedSeatManager.SetCurrentBookSeat(targetSeat);
            bookMover.MoveToSeatAuthoritatively(targetSeat);
            StartCoroutine(CompletePresentationMovementAfterDuration(nextPresentationSequence));

            Debug.Log(
                "[GameOverPresentation]\n" +
                "Winner Book Movement Started\n" +
                $"RitualSessionId = {ritualSessionId}\n" +
                $"RitualSequence = {ritualSequence}\n" +
                $"PresentationSequence = {nextPresentationSequence}\n" +
                $"WinnerPlayerId = {winnerPlayerId}\n" +
                $"WinnerSeatId = {winnerSeatId}",
                this);
            return true;
        }

        /// <summary>
        /// Restores the one persistent Book to its authored lobby pose without creating ritual
        /// movement or arrival state.
        /// </summary>
        public bool TryResetToLobbyPose()
        {
            if (!IsServerInitialized || !hasLobbyPose || bookMover == null ||
                presentationTransform == null)
            {
                return false;
            }

            NetworkRitualAuthority resolvedRitualAuthority = ResolveRitualAuthority();
            if (resolvedRitualAuthority == null ||
                resolvedRitualAuthority.Snapshot.Phase != RitualPhase.Inactive)
            {
                return false;
            }

            StopAllCoroutines();
            bookMover.StopAuthoritativeMovement();
            hasActiveMovementCommand = false;
            activeMovementCommand = default;
            isMoving.Value = false;
            isPresentationMoving.Value = false;
            targetSeatId.Value = NoTargetSeatId;
            presentedRitualSessionId.Value = string.Empty;
            presentedRitualSequence.Value = 0;
            presentedWinnerPlayerId.Value = string.Empty;
            presentationTransform.SetPositionAndRotation(lobbyPosition, lobbyRotation);
            transform.SetPositionAndRotation(lobbyPosition, lobbyRotation);
            ResolveSeatManager()?.SetCurrentBookSeat(null);
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

        private NetworkRitualAuthority ResolveRitualAuthority()
        {
            if (ritualAuthority == null)
                ritualAuthority = GetComponent<NetworkRitualAuthority>();

            return ritualAuthority;
        }

        private IReadOnlyList<Seat> GetPhysicalSeatOrder(SeatManager resolvedSeatManager)
        {
            NetworkRitualAuthority resolvedRitualAuthority = ResolveRitualAuthority();
            RitualTraversalDirection direction = resolvedRitualAuthority != null
                ? resolvedRitualAuthority.Snapshot.TraversalDirection
                : RitualTraversalDirection.Clockwise;
            SeatTraversalDirection seatDirection =
                direction == RitualTraversalDirection.Clockwise
                    ? SeatTraversalDirection.Clockwise
                    : SeatTraversalDirection.CounterClockwise;
            return resolvedSeatManager.GetPhysicalSeats(seatDirection);
        }

        private static List<Seat> BuildPhysicalSeatRoute(
            Seat previousSeat,
            Seat targetSeat,
            IReadOnlyList<Seat> orderedSeats)
        {
            List<Seat> route = new();
            if (previousSeat == null || targetSeat == null ||
                orderedSeats == null || orderedSeats.Count == 0)
            {
                return route;
            }

            int previousIndex = IndexOfSeat(orderedSeats, previousSeat);
            int targetIndex = IndexOfSeat(orderedSeats, targetSeat);
            if (previousIndex < 0 || targetIndex < 0 || previousIndex == targetIndex)
                return route;

            for (int offset = 1; offset <= orderedSeats.Count; offset++)
            {
                Seat waypoint = orderedSeats[(previousIndex + offset) % orderedSeats.Count];
                route.Add(waypoint);
                if (waypoint == targetSeat)
                    return route;
            }

            route.Clear();
            return route;
        }

        private static bool TryResolvePhysicalStartSeat(
            Vector3 bookPosition,
            IReadOnlyList<Seat> orderedSeats,
            out Seat resolvedSeat)
        {
            resolvedSeat = null;
            if (orderedSeats == null || orderedSeats.Count < 2)
                return false;

            float adjacentDistanceTotal = 0f;
            int adjacentSegmentCount = 0;
            float closestDistance = float.PositiveInfinity;
            Vector2 bookTablePosition = new(bookPosition.x, bookPosition.z);

            for (int index = 0; index < orderedSeats.Count; index++)
            {
                Seat seat = orderedSeats[index];
                Seat nextSeat = orderedSeats[(index + 1) % orderedSeats.Count];
                Transform destination = seat != null ? seat.GetBookDestination() : null;
                Transform nextDestination = nextSeat != null
                    ? nextSeat.GetBookDestination()
                    : null;
                if (destination == null)
                    continue;

                Vector2 destinationTablePosition = new(
                    destination.position.x,
                    destination.position.z);
                float distance = Vector2.Distance(
                    bookTablePosition,
                    destinationTablePosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    resolvedSeat = seat;
                }

                if (nextDestination == null)
                    continue;

                adjacentDistanceTotal += Vector2.Distance(
                    destinationTablePosition,
                    new Vector2(nextDestination.position.x, nextDestination.position.z));
                adjacentSegmentCount++;
            }

            if (resolvedSeat == null || adjacentSegmentCount == 0)
            {
                resolvedSeat = null;
                return false;
            }

            float averageAdjacentDistance = adjacentDistanceTotal / adjacentSegmentCount;
            bool isNearPhysicalSeat = averageAdjacentDistance > Mathf.Epsilon &&
                closestDistance <= averageAdjacentDistance * 0.5f;
            if (!isNearPhysicalSeat)
                resolvedSeat = null;

            return isNearPhysicalSeat;
        }

        private static int IndexOfSeat(IReadOnlyList<Seat> seats, Seat target)
        {
            for (int index = 0; index < seats.Count; index++)
            {
                if (seats[index] == target)
                    return index;
            }

            return -1;
        }

        private void CompleteRitualMovement(uint expectedSequence)
        {

            if (!IsServerInitialized ||
                movementSequence.Value != expectedSequence ||
                !hasActiveMovementCommand ||
                activeMovementCommand.MovementSequence != expectedSequence)
            {
                return;
            }

            RitualBookMovementCommand completedCommand = activeMovementCommand;
            hasActiveMovementCommand = false;
            isMoving.Value = false;

            double completionNetworkTime = TimeManager != null
                ? TimeManager.TicksToTime()
                : 0d;
            RitualBookArrivalReport report = new(
                completedCommand.MovementSequence,
                completedCommand.RitualSequence,
                completedCommand.TurnSequence,
                completedCommand.TargetSeatId,
                completionNetworkTime);

            NetworkRitualAuthority resolvedRitualAuthority = ResolveRitualAuthority();
            if (resolvedRitualAuthority == null ||
                !resolvedRitualAuthority.TryCommitBookArrival(report))
            {
                Debug.LogWarning(
                    $"{nameof(NetworkBookAuthority)} completed movement {expectedSequence}, but NetworkRitualAuthority rejected or could not receive its arrival report.",
                    this);
            }
        }

        private IEnumerator CompletePresentationMovementAfterDuration(uint expectedSequence)
        {
            float duration = Mathf.Max(0f, bookMover.moveDuration);
            if (duration > 0f)
                yield return new WaitForSeconds(duration);

            if (!IsServerInitialized ||
                presentationMovementSequence.Value != expectedSequence ||
                !isPresentationMoving.Value)
            {
                yield break;
            }

            isPresentationMoving.Value = false;
            isMoving.Value = false;

            Debug.Log(
                "[GameOverPresentation]\n" +
                "Winner Book Movement Completed\n" +
                $"PresentationSequence = {expectedSequence}\n" +
                $"WinnerPlayerId = {presentedWinnerPlayerId.Value}\n" +
                "RitualBookArrivalReport = not generated",
                this);
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
