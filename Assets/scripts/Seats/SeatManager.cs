using System.Collections.Generic;
using UnityEngine;

public enum SeatTraversalDirection
{
    Clockwise,
    CounterClockwise
}

public class SeatManager : MonoBehaviour
{
    [Header("Sièges détectés")]
    public List<Seat> seats = new List<Seat>();

    [Header("Physical Table Order")]
    [Tooltip("Official clockwise physical seat order: 1 -> 5 -> 3 -> 6 -> 2 -> 7 -> 4 -> 8. Assign Seat references in that exact order.")]
    [SerializeField] private List<Seat> clockwisePhysicalSeatOrder = new List<Seat>();

    [Header("Livre")]
    public BookMover bookMover;
    public Seat currentBookSeat;

    [Header("Debug")]
    [SerializeField] private bool allowMultipleDebugOccupants = false;
    [SerializeField] private bool enableDebugLogs = false;

    private readonly Dictionary<Seat, GameObject> debugOccupantsBySeat = new Dictionary<Seat, GameObject>();
    private readonly List<Seat> eliminatedSeats = new List<Seat>();

    private void Awake()
    {
        FindSeats();
    }

    private void Update()
    {
        // TEST : appuie sur ESPACE pour envoyer le livre au prochain joueur occupé
        if (Input.GetKeyDown(KeyCode.Space))
        {
            MoveBookToNextOccupiedSeat();
        }
    }

    private void OnValidate()
    {
        FindSeats();
    }

    [ContextMenu("Find Seats")]
    public void FindSeats()
    {
        seats.Clear();

        Seat[] foundSeats = GetComponentsInChildren<Seat>(true);

        foreach (Seat seat in foundSeats)
        {
            if (seat != null)
                seats.Add(seat);
        }
    }

    public IReadOnlyList<Seat> GetClockwisePhysicalSeatOrder()
    {
        return clockwisePhysicalSeatOrder;
    }

    public bool HasConfiguredPhysicalSeatOrder()
    {
        if (clockwisePhysicalSeatOrder == null || clockwisePhysicalSeatOrder.Count != 8)
            return false;

        HashSet<Seat> uniqueSeats = new HashSet<Seat>();

        foreach (Seat seat in clockwisePhysicalSeatOrder)
        {
            if (seat == null || uniqueSeats.Contains(seat))
                return false;

            uniqueSeats.Add(seat);
        }

        return true;
    }

    public List<Seat> GetPhysicalSeats(SeatTraversalDirection direction)
    {
        List<Seat> orderedSeats = new List<Seat>();

        if (!HasConfiguredPhysicalSeatOrder())
            return orderedSeats;

        if (direction == SeatTraversalDirection.Clockwise)
        {
            orderedSeats.AddRange(clockwisePhysicalSeatOrder);
            return orderedSeats;
        }

        for (int seatIndex = clockwisePhysicalSeatOrder.Count - 1; seatIndex >= 0; seatIndex--)
            orderedSeats.Add(clockwisePhysicalSeatOrder[seatIndex]);

        return orderedSeats;
    }

    public Seat GetFirstActiveSeat(SeatTraversalDirection direction, System.Predicate<Seat> isSeatActive)
    {
        List<Seat> orderedSeats = GetPhysicalSeats(direction);

        foreach (Seat seat in orderedSeats)
        {
            if (isSeatActive == null || isSeatActive(seat))
                return seat;
        }

        return null;
    }

    public Seat GetNextActiveSeat(Seat currentSeat, SeatTraversalDirection direction, System.Predicate<Seat> isSeatActive)
    {
        List<Seat> orderedSeats = GetPhysicalSeats(direction);

        if (orderedSeats.Count == 0)
            return null;

        int currentIndex = currentSeat != null ? orderedSeats.IndexOf(currentSeat) : -1;

        for (int offset = 1; offset <= orderedSeats.Count; offset++)
        {
            int nextIndex = currentIndex < 0 ? offset - 1 : currentIndex + offset;
            nextIndex %= orderedSeats.Count;

            Seat candidateSeat = orderedSeats[nextIndex];

            if (isSeatActive == null || isSeatActive(candidateSeat))
                return candidateSeat;
        }

        return null;
    }

    public void TrySit(Seat seat)
    {
        if (seat == null)
            return;

        if (allowMultipleDebugOccupants)
        {
            ToggleDebugOccupant(seat);
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("Aucun objet avec le tag Player trouvé.");
            return;
        }

        // Libère l’ancien siège du joueur si nécessaire
        foreach (Seat s in seats)
        {
            if (s.currentPlayer == player)
                s.Free();
        }

        player.transform.position = seat.playerSpawn.position;
        player.transform.rotation = seat.playerSpawn.rotation;

        seat.Occupy(player);

        LogDebug($"Le joueur est maintenant assis sur {seat.name}");
    }

    public void MoveBookToSeat(Seat seat)
    {
        if (seat == null)
            return;

        SetCurrentBookSeat(seat);

        LogDebug("SeatManager.MoveBookToSeat used for: " + seat.name);

        if (bookMover != null)
            bookMover.MoveToSeat(seat);
    }

    public void SetCurrentBookSeat(Seat seat)
    {
        currentBookSeat = seat;
    }

    public void MoveBookToNextOccupiedSeat()
    {
        Seat nextSeat = GetNextOccupiedSeat(currentBookSeat);

        if (nextSeat == null)
        {
            LogDebug("Aucun autre siège occupé.");
            return;
        }

        MoveBookToSeat(nextSeat);

        LogDebug("Livre envoyé vers : " + nextSeat.name);
    }

    public List<Seat> GetOccupiedSeats()
    {
        List<Seat> occupiedSeats = new List<Seat>();
        List<Seat> orderedSeats = HasConfiguredPhysicalSeatOrder()
            ? GetPhysicalSeats(SeatTraversalDirection.Clockwise)
            : seats;

        foreach (Seat seat in orderedSeats)
        {
            if (IsSeatOccupied(seat))
                occupiedSeats.Add(seat);
        }

        return occupiedSeats;
    }

    public IReadOnlyList<Seat> GetEliminatedSeats()
    {
        return eliminatedSeats;
    }

    public bool IsSeatEliminated(Seat seat)
    {
        return seat != null && eliminatedSeats.Contains(seat);
    }

    public void EliminateSeat(Seat seat)
    {
        if (seat == null)
            return;

        if (!eliminatedSeats.Contains(seat))
            eliminatedSeats.Add(seat);

        if (!seat.IsFree())
            seat.Free();
    }

    public Seat GetNextOccupiedSeat(Seat currentSeat)
    {
        List<Seat> occupiedSeats = GetOccupiedSeats();

        if (occupiedSeats.Count == 0)
            return null;

        if (currentSeat == null)
            return occupiedSeats[0];

        int currentIndex = occupiedSeats.IndexOf(currentSeat);

        if (currentIndex == -1)
            return occupiedSeats[0];

        int nextIndex = currentIndex + 1;

        if (nextIndex >= occupiedSeats.Count)
            nextIndex = 0;

        return occupiedSeats[nextIndex];
    }

    private bool IsSeatOccupied(Seat seat)
    {
        return seat != null && !IsSeatEliminated(seat) && !seat.IsFree();
    }

    private void ToggleDebugOccupant(Seat seat)
    {
        if (seat == null)
            return;

        if (IsOccupiedByDebugOccupant(seat))
        {
            FreeDebugOccupant(seat);
            LogDebug($"Debug occupant removed from {seat.name}");
            return;
        }

        if (!seat.IsFree())
        {
            LogDebug($"{seat.name} is already occupied by a non-debug player.");
            return;
        }

        GameObject debugOccupant = new GameObject($"DebugOccupant_{seat.name}");
        debugOccupant.transform.SetParent(transform);

        if (seat.playerSpawn != null)
        {
            debugOccupant.transform.position = seat.playerSpawn.position;
            debugOccupant.transform.rotation = seat.playerSpawn.rotation;
        }

        debugOccupantsBySeat[seat] = debugOccupant;
        seat.Occupy(debugOccupant);

        LogDebug($"Debug occupant added to {seat.name}");
    }

    private bool IsOccupiedByDebugOccupant(Seat seat)
    {
        if (seat == null)
            return false;

        if (!debugOccupantsBySeat.TryGetValue(seat, out GameObject debugOccupant))
            return false;

        if (debugOccupant == null || seat.currentPlayer != debugOccupant)
        {
            debugOccupantsBySeat.Remove(seat);
            return false;
        }

        return true;
    }

    private void FreeDebugOccupant(Seat seat)
    {
        if (seat == null)
            return;

        if (!debugOccupantsBySeat.TryGetValue(seat, out GameObject debugOccupant))
            return;

        debugOccupantsBySeat.Remove(seat);

        if (seat.currentPlayer == debugOccupant)
            seat.Free();

        if (debugOccupant == null)
            return;

        if (Application.isPlaying)
            Destroy(debugOccupant);
        else
            DestroyImmediate(debugOccupant);
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log(message);
    }
}
