using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Play Mode debug helper for validating local multiplayer seat flow without networking.
/// It simulates occupied, empty, and eliminated seats, then moves the one real book through
/// SeatManager's configured physical table order.
/// </summary>
public class DebugSeatFlowSimulator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SeatManager seatManager;
    [SerializeField] private BookController bookController;
    [SerializeField] private GrowingIncantationManager growingIncantationManager;

    [Header("Simulation")]
    [SerializeField, Range(2, 8)] private int simulatedActivePlayerCount = 4;
    [SerializeField] private SeatTraversalDirection traversalDirection = SeatTraversalDirection.Clockwise;
    [SerializeField] private bool resetPhraseOnSimulationReset = true;
    [SerializeField] private bool moveBookOnReset = true;

    [Header("Keyboard Shortcuts")]
    [SerializeField] private bool enableKeyboardShortcuts = true;
    [SerializeField] private KeyCode advanceTurnKey = KeyCode.N;
    [SerializeField] private KeyCode killCurrentSeatKey = KeyCode.K;
    [SerializeField] private KeyCode resetSimulationKey = KeyCode.R;
    [SerializeField] private KeyCode toggleDirectionKey = KeyCode.V;

    [Header("Debug State")]
    [SerializeField] private Seat currentSimulatedSeat;
    [SerializeField] private int completedRotationCount;
    [SerializeField] private bool completedRotationOnLastAdvance;
    [SerializeField] private List<Seat> simulatedOccupiedSeats = new List<Seat>();
    [SerializeField] private List<Seat> simulatedEliminatedSeats = new List<Seat>();
    [SerializeField] private List<Seat> completedSeatsThisRotation = new List<Seat>();

    private readonly Dictionary<Seat, GameObject> simulatedPlayersBySeat = new Dictionary<Seat, GameObject>();

    public Seat CurrentSimulatedSeat => currentSimulatedSeat;
    public int CompletedRotationCount => completedRotationCount;
    public bool CompletedRotationOnLastAdvance => completedRotationOnLastAdvance;

    private void Awake()
    {
        ResolveMissingReferences();
    }

    private void Update()
    {
        if (!Application.isPlaying || !enableKeyboardShortcuts ||
            !LocalInputContextGate.AllowsGameplayInput)
            return;

        if (Input.GetKeyDown(advanceTurnKey))
            AdvanceToNextSimulatedTurn();

        if (Input.GetKeyDown(killCurrentSeatKey))
            KillOrDeactivateCurrentSimulatedPlayer();

        if (Input.GetKeyDown(resetSimulationKey))
            ResetSimulation();

        if (Input.GetKeyDown(toggleDirectionKey))
            ToggleTraversalDirection();
    }

    [ContextMenu("Debug Seat Flow/Reset Simulation")]
    public void ResetSimulation()
    {
        ResolveMissingReferences();

        if (!HasUsableSeatManager())
            return;

        ClearSimulationState();

        List<Seat> orderedSeats = seatManager.GetPhysicalSeats(traversalDirection);
        int playersToSimulate = Mathf.Clamp(simulatedActivePlayerCount, 2, Mathf.Min(8, orderedSeats.Count));

        for (int seatIndex = 0; seatIndex < orderedSeats.Count; seatIndex++)
        {
            Seat seat = orderedSeats[seatIndex];

            if (seatIndex < playersToSimulate)
                OccupySeatWithSimulatedPlayer(seat, seatIndex + 1);
            else
                seat.Free();
        }

        if (resetPhraseOnSimulationReset && growingIncantationManager != null)
            growingIncantationManager.ResetPhrase();

        currentSimulatedSeat = seatManager.GetFirstActiveSeat(traversalDirection, IsSeatActiveInSimulation);

        if (moveBookOnReset)
            MoveBookToCurrentSimulatedSeat();

        LogSimulationState("Reset simulation");
    }

    public void SetSimulatedActivePlayerCount(int activePlayerCount)
    {
        simulatedActivePlayerCount = Mathf.Clamp(activePlayerCount, 2, 8);
        ResetSimulation();
    }

    [ContextMenu("Debug Seat Flow/Apply Simulated Active Player Count")]
    public void ApplySimulatedActivePlayerCount()
    {
        SetSimulatedActivePlayerCount(simulatedActivePlayerCount);
    }

    [ContextMenu("Debug Seat Flow/Advance To Next Simulated Turn")]
    public void AdvanceToNextSimulatedTurn()
    {
        if (!HasUsableSeatManager())
            return;

        completedRotationOnLastAdvance = false;

        if (currentSimulatedSeat == null)
            currentSimulatedSeat = seatManager.GetFirstActiveSeat(traversalDirection, IsSeatActiveInSimulation);

        if (currentSimulatedSeat == null)
        {
            Debug.LogWarning($"{nameof(DebugSeatFlowSimulator)} has no active simulated seats to advance.", this);
            return;
        }

        MarkCurrentSeatCompleted();

        if (HasCompletedFullActiveRotation())
            CompleteFullActiveRotation();

        Seat nextSeat = seatManager.GetNextActiveSeat(currentSimulatedSeat, traversalDirection, IsSeatActiveInSimulation);

        if (nextSeat == null)
        {
            Debug.LogWarning($"{nameof(DebugSeatFlowSimulator)} could not find another active simulated seat.", this);
            return;
        }

        currentSimulatedSeat = nextSeat;
        MoveBookToCurrentSimulatedSeat();
        LogSimulationState("Advance simulated turn");
    }

    [ContextMenu("Debug Seat Flow/Kill Or Deactivate Current Simulated Player")]
    public void KillOrDeactivateCurrentSimulatedPlayer()
    {
        if (!HasUsableSeatManager())
            return;

        if (currentSimulatedSeat == null)
            currentSimulatedSeat = seatManager.GetFirstActiveSeat(traversalDirection, IsSeatActiveInSimulation);

        if (currentSimulatedSeat == null)
        {
            Debug.LogWarning($"{nameof(DebugSeatFlowSimulator)} has no current simulated player to eliminate.", this);
            return;
        }

        if (!simulatedEliminatedSeats.Contains(currentSimulatedSeat))
            simulatedEliminatedSeats.Add(currentSimulatedSeat);

        completedSeatsThisRotation.Remove(currentSimulatedSeat);

        Seat eliminatedSeat = currentSimulatedSeat;
        currentSimulatedSeat = seatManager.GetNextActiveSeat(eliminatedSeat, traversalDirection, IsSeatActiveInSimulation);

        if (currentSimulatedSeat != null)
            MoveBookToCurrentSimulatedSeat();

        LogSimulationState("Eliminate simulated player");
    }

    [ContextMenu("Debug Seat Flow/Toggle Traversal Direction")]
    public void ToggleTraversalDirection()
    {
        traversalDirection = traversalDirection == SeatTraversalDirection.Clockwise
            ? SeatTraversalDirection.CounterClockwise
            : SeatTraversalDirection.Clockwise;

        currentSimulatedSeat = seatManager != null
            ? seatManager.GetFirstActiveSeat(traversalDirection, IsSeatActiveInSimulation)
            : null;

        MoveBookToCurrentSimulatedSeat();
        LogSimulationState("Toggle traversal direction");
    }

    private void ClearSimulationState()
    {
        foreach (KeyValuePair<Seat, GameObject> simulatedPlayerEntry in simulatedPlayersBySeat)
        {
            if (simulatedPlayerEntry.Value == null)
                continue;

            if (Application.isPlaying)
                Destroy(simulatedPlayerEntry.Value);
            else
                DestroyImmediate(simulatedPlayerEntry.Value);
        }

        simulatedPlayersBySeat.Clear();

        foreach (Seat seat in simulatedOccupiedSeats)
        {
            if (seat != null)
                seat.Free();
        }

        simulatedOccupiedSeats.Clear();
        simulatedEliminatedSeats.Clear();
        completedSeatsThisRotation.Clear();
        currentSimulatedSeat = null;
        completedRotationCount = 0;
        completedRotationOnLastAdvance = false;
    }

    private void OccupySeatWithSimulatedPlayer(Seat seat, int playerNumber)
    {
        if (seat == null)
            return;

        GameObject simulatedPlayer = new GameObject($"SimulatedPlayer_{playerNumber}");
        simulatedPlayer.transform.SetParent(transform);

        if (seat.playerSpawn != null)
        {
            simulatedPlayer.transform.position = seat.playerSpawn.position;
            simulatedPlayer.transform.rotation = seat.playerSpawn.rotation;
        }

        seat.Occupy(simulatedPlayer);
        simulatedPlayersBySeat[seat] = simulatedPlayer;
        simulatedOccupiedSeats.Add(seat);
    }

    private void MarkCurrentSeatCompleted()
    {
        if (currentSimulatedSeat == null || completedSeatsThisRotation.Contains(currentSimulatedSeat))
            return;

        completedSeatsThisRotation.Add(currentSimulatedSeat);
    }

    private bool HasCompletedFullActiveRotation()
    {
        List<Seat> activeSeats = GetActiveSimulatedSeats();

        if (activeSeats.Count == 0)
            return false;

        foreach (Seat activeSeat in activeSeats)
        {
            if (!completedSeatsThisRotation.Contains(activeSeat))
                return false;
        }

        return true;
    }

    private void CompleteFullActiveRotation()
    {
        completedRotationCount++;
        completedRotationOnLastAdvance = true;
        completedSeatsThisRotation.Clear();

        if (growingIncantationManager != null)
            growingIncantationManager.UnlockNextWord();
    }

    private List<Seat> GetActiveSimulatedSeats()
    {
        List<Seat> activeSeats = new List<Seat>();

        foreach (Seat occupiedSeat in simulatedOccupiedSeats)
        {
            if (IsSeatActiveInSimulation(occupiedSeat))
                activeSeats.Add(occupiedSeat);
        }

        return activeSeats;
    }

    private bool IsSeatActiveInSimulation(Seat seat)
    {
        return seat != null &&
            simulatedOccupiedSeats.Contains(seat) &&
            !simulatedEliminatedSeats.Contains(seat) &&
            !seat.IsFree();
    }

    private void MoveBookToCurrentSimulatedSeat()
    {
        if (currentSimulatedSeat == null || seatManager == null)
            return;

        seatManager.SetCurrentBookSeat(currentSimulatedSeat);

        if (bookController != null)
        {
            bookController.MoveToSeat(currentSimulatedSeat);
            return;
        }

        seatManager.MoveBookToSeat(currentSimulatedSeat);
    }

    private bool HasUsableSeatManager()
    {
        ResolveMissingReferences();

        if (seatManager == null)
        {
            Debug.LogWarning($"{nameof(DebugSeatFlowSimulator)} requires a {nameof(SeatManager)} reference.", this);
            return false;
        }

        if (!seatManager.HasConfiguredPhysicalSeatOrder())
        {
            Debug.LogWarning($"{nameof(DebugSeatFlowSimulator)} requires {nameof(SeatManager)} to have its clockwise physical seat order assigned as 1 -> 5 -> 3 -> 6 -> 2 -> 7 -> 4 -> 8.", this);
            return false;
        }

        return true;
    }

    private void ResolveMissingReferences()
    {
        if (seatManager == null)
            seatManager = FindFirstObjectByType<SeatManager>();

        if (bookController == null)
            bookController = FindFirstObjectByType<BookController>();

        if (growingIncantationManager == null)
            growingIncantationManager = FindFirstObjectByType<GrowingIncantationManager>();
    }

    private void LogSimulationState(string actionName)
    {
        string phrase = growingIncantationManager != null
            ? growingIncantationManager.GetCurrentPhrase()
            : "No GrowingIncantationManager";

        Debug.Log(
            $"{nameof(DebugSeatFlowSimulator)}::{actionName}\n" +
            $"Direction: {traversalDirection}\n" +
            $"Current seat: {(currentSimulatedSeat != null ? currentSimulatedSeat.name : "None")}\n" +
            $"Active seats: {GetActiveSimulatedSeats().Count}\n" +
            $"Eliminated seats: {simulatedEliminatedSeats.Count}\n" +
            $"Completed rotations: {completedRotationCount}\n" +
            $"Current phrase: {phrase}",
            this);
    }

    private void OnValidate()
    {
        simulatedActivePlayerCount = Mathf.Clamp(simulatedActivePlayerCount, 2, 8);
    }
}
