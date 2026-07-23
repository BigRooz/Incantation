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

    [Header("Lobby Seat Selection")]
    [SerializeField] private bool lobbySeatSelectionEnabled = true;
    [SerializeField] private GameObject localLobbyPlayer;
    [SerializeField] private bool showLobbyOccupiedMarkers = true;
    [SerializeField] private Color lobbyOccupiedMarkerColor = new Color(0.55f, 0.05f, 0.03f, 1f);
    [SerializeField, Min(0.05f)] private float lobbyOccupiedMarkerSize = 0.18f;
    [SerializeField] private Vector3 lobbyOccupiedMarkerOffset = new Vector3(0f, 0.08f, 0f);

    [Header("Debug")]
    [SerializeField] private bool allowMultipleDebugOccupants = false;
    [SerializeField] private bool enableDebugLogs = false;

    private readonly Dictionary<Seat, GameObject> debugOccupantsBySeat = new Dictionary<Seat, GameObject>();
    private readonly Dictionary<Seat, GameObject> lobbyOccupiedMarkersBySeat = new Dictionary<Seat, GameObject>();
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

    public void SetLocalLobbyPlayer(GameObject player)
    {
        localLobbyPlayer = player;
    }

    public void SetLobbySeatSelectionEnabled(bool isEnabled)
    {
        lobbySeatSelectionEnabled = isEnabled;

        if (lobbySeatSelectionEnabled)
            RefreshLobbySeatVisuals();
        else
            ClearLobbySeatVisuals();
    }

    public bool TryLobbySit(Seat seat)
    {
        return TryLobbySit(seat, localLobbyPlayer);
    }

    public bool TryLobbySit(Seat seat, GameObject player)
    {
        if (!lobbySeatSelectionEnabled)
            return false;

        if (seat == null || player == null)
            return false;

        Seat currentSeat = GetLobbySeatForPlayer(player);

        if (currentSeat == seat)
        {
            MovePlayerToSeat(player, seat);
            RefreshLobbySeatVisuals();
            return true;
        }

        if (!IsLobbySeatAvailable(seat))
        {
            LogDebug($"{seat.name} is already occupied and cannot be selected in lobby.");
            return false;
        }

        if (seat.playerSpawn == null)
        {
            Debug.LogWarning($"{seat.name} cannot seat the lobby player because it has no PlayerSpawn assigned.", seat);
            return false;
        }

        LeaveLobbySeat(player);
        MovePlayerToSeat(player, seat);
        seat.Occupy(player);
        RefreshLobbySeatVisuals();

        LogDebug($"Lobby player seated at {seat.name}");
        return true;
    }

    public bool LeaveLobbySeat(GameObject player)
    {
        if (player == null)
            return false;

        bool leftSeat = false;

        foreach (Seat seat in seats)
        {
            if (seat == null || seat.currentPlayer != player)
                continue;

            seat.Free();
            leftSeat = true;
        }

        if (leftSeat)
            RefreshLobbySeatVisuals();

        return leftSeat;
    }

    public bool LeaveLobbySeat(GameObject player, Transform waitingPosition)
    {
        if (!LeaveLobbySeat(player))
            return false;

        MovePlayerToPosition(player, waitingPosition);
        return true;
    }

    public bool IsLobbySeatAvailable(Seat seat)
    {
        return lobbySeatSelectionEnabled &&
            seat != null &&
            !IsSeatEliminated(seat) &&
            seat.IsFree();
    }

    public Seat GetLobbySeatForPlayer(GameObject player)
    {
        if (player == null)
            return null;

        foreach (Seat seat in seats)
        {
            if (seat != null && seat.currentPlayer == player)
                return seat;
        }

        return null;
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

    private static void MovePlayerToSeat(GameObject player, Seat seat)
    {
        if (player == null || seat == null || seat.playerSpawn == null)
            return;

        MovePlayerToPosition(player, seat.playerSpawn);
    }

    private static void MovePlayerToPosition(GameObject player, Transform destination)
    {
        if (player == null || destination == null)
            return;

        player.SetActive(true);
        player.transform.SetPositionAndRotation(destination.position, destination.rotation);
    }

    private void RefreshLobbySeatVisuals()
    {
        if (!showLobbyOccupiedMarkers)
        {
            ClearLobbySeatVisuals();
            return;
        }

        foreach (Seat seat in seats)
        {
            if (seat == null)
                continue;

            bool shouldShowMarker = lobbySeatSelectionEnabled && !seat.IsFree();
            SetLobbyOccupiedMarkerVisible(seat, shouldShowMarker);
        }
    }

    private void SetLobbyOccupiedMarkerVisible(Seat seat, bool isVisible)
    {
        if (seat == null)
            return;

        if (!isVisible)
        {
            if (lobbyOccupiedMarkersBySeat.TryGetValue(seat, out GameObject existingMarker) && existingMarker != null)
                existingMarker.SetActive(false);

            return;
        }

        GameObject marker = GetOrCreateLobbyOccupiedMarker(seat);

        if (marker != null)
            marker.SetActive(true);
    }

    private GameObject GetOrCreateLobbyOccupiedMarker(Seat seat)
    {
        if (seat == null)
            return null;

        if (lobbyOccupiedMarkersBySeat.TryGetValue(seat, out GameObject existingMarker) && existingMarker != null)
            return existingMarker;

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = $"LobbyOccupiedMarker_{seat.name}";
        marker.transform.SetParent(seat.transform, false);
        marker.transform.position = GetLobbyMarkerWorldPosition(seat);
        marker.transform.localScale = Vector3.one * lobbyOccupiedMarkerSize;

        Collider markerCollider = marker.GetComponent<Collider>();

        if (markerCollider != null)
            Destroy(markerCollider);

        Renderer markerRenderer = marker.GetComponent<Renderer>();

        if (markerRenderer != null)
            markerRenderer.material.color = lobbyOccupiedMarkerColor;

        lobbyOccupiedMarkersBySeat[seat] = marker;
        return marker;
    }

    private Vector3 GetLobbyMarkerWorldPosition(Seat seat)
    {
        if (seat != null && seat.playerSpawn != null)
            return seat.playerSpawn.position + lobbyOccupiedMarkerOffset;

        return seat != null
            ? seat.transform.position + lobbyOccupiedMarkerOffset
            : lobbyOccupiedMarkerOffset;
    }

    private void ClearLobbySeatVisuals()
    {
        foreach (KeyValuePair<Seat, GameObject> markerEntry in lobbyOccupiedMarkersBySeat)
        {
            if (markerEntry.Value != null)
                markerEntry.Value.SetActive(false);
        }
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
