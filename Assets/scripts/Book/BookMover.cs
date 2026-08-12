using System.Collections;
using Incantation.Networking;
using UnityEngine;

/// <summary>
/// Moves the single physical Book toward Seat destinations.
/// In a FishNet session it forwards legacy requests to NetworkRitualAuthority; offline Play
/// Mode preserves the existing local path.
/// </summary>
[DisallowMultipleComponent]
public class BookMover : MonoBehaviour
{
    [Header("Mouvement")]
    public float moveDuration = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Coroutine moveRoutine;
    private NetworkBookAuthority networkAuthority;
    private NetworkRitualAuthority ritualAuthority;
    private SeatManager seatManager;

    private void Awake()
    {
        ResolveNetworkAuthority();
    }

    public void MoveToSeat(Seat seat)
    {
        if (seat == null)
            return;

        NetworkBookAuthority resolvedNetworkAuthority = ResolveNetworkAuthority();
        if (resolvedNetworkAuthority != null && resolvedNetworkAuthority.IsNetworkSessionActive)
        {
            NetworkRitualAuthority resolvedRitualAuthority = ResolveRitualAuthority();
            SeatManager resolvedSeatManager = ResolveSeatManager();
            int seatId = resolvedSeatManager != null
                ? resolvedSeatManager.GetSeatId(seat)
                : NetworkPlayer.UnassignedSeatId;

            if (resolvedRitualAuthority == null ||
                seatId == NetworkPlayer.UnassignedSeatId)
            {
                Debug.LogWarning(
                    "[RitualAuthority]\n" +
                    "Book Movement Rejected\n" +
                    "Reason = Legacy BookMover could not resolve ritual authority or a stable target Seat ID.",
                    this);
                return;
            }

            resolvedRitualAuthority.TryForwardLegacyBookMoveRequest(seatId);
            return;
        }

        MoveToSeatAuthoritatively(seat);
    }

    /// <summary>
    /// Executes the existing movement interpolation after authority has been resolved.
    /// NetworkBookAuthority is the only network-session caller; offline flow calls it directly.
    /// </summary>
    public void MoveToSeatAuthoritatively(Seat seat)
    {
        if (seat == null)
            return;

        Transform destination = seat.GetBookDestination();

        if (destination == null)
        {
            Debug.LogWarning("BookMover : aucune destination de livre trouvée.");
            return;
        }

        LogDebug($"BookMover.MoveToSeat | Book: {name} | Target Object: {destination.name} | Target Position: {destination.position}");

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MoveTo(destination));
    }

    public void StopAuthoritativeMovement()
    {
        if (moveRoutine == null)
            return;

        StopCoroutine(moveRoutine);
        moveRoutine = null;
    }

    private IEnumerator MoveTo(Transform target)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        Vector3 targetPosition = target.position;
        Quaternion targetRotation = target.rotation;
        float distance = Vector3.Distance(startPosition, targetPosition);

        LogDebug($"BookMover.MoveTo | Book: {name} | Start Position: {startPosition} | Target Position: {targetPosition} | Target Object: {target.name} | Distance: {distance}");

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / moveDuration;
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

            yield return null;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log(message);
    }

    private NetworkBookAuthority ResolveNetworkAuthority()
    {
        if (networkAuthority == null)
        {
            networkAuthority = NetworkBookAuthority.Instance;
        }

        if (networkAuthority == null)
        {
            networkAuthority = FindFirstObjectByType<NetworkBookAuthority>(
                FindObjectsInactive.Include);
        }

        return networkAuthority;
    }

    private NetworkRitualAuthority ResolveRitualAuthority()
    {
        if (ritualAuthority == null)
            ritualAuthority = NetworkRitualAuthority.Instance;

        if (ritualAuthority == null)
        {
            ritualAuthority = FindFirstObjectByType<NetworkRitualAuthority>(
                FindObjectsInactive.Include);
        }

        return ritualAuthority;
    }

    private SeatManager ResolveSeatManager()
    {
        if (seatManager == null)
            seatManager = FindFirstObjectByType<SeatManager>(FindObjectsInactive.Include);

        return seatManager;
    }
}
