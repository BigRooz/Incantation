using System;
using System.Collections;
using System.Collections.Generic;
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
    private uint movementGeneration;
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
        MoveToSeatAuthoritatively(seat, null);
    }

    /// <summary>
    /// Executes the existing direct movement and reports completion only if this movement was
    /// not replaced or cancelled.
    /// </summary>
    public void MoveToSeatAuthoritatively(Seat seat, Action onCompleted)
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

        BeginMovement(
            new[] { destination.position },
            new[] { destination.rotation },
            Mathf.Max(0f, moveDuration),
            onCompleted);
    }

    /// <summary>
    /// Moves continuously through an ordered physical Seat route. Intermediate Seats provide
    /// geometry only; completion is reported once, after the final destination is reached.
    /// </summary>
    public bool MoveAlongSeatRouteAuthoritatively(
        IReadOnlyList<Seat> route,
        IReadOnlyList<Seat> completePhysicalSeatOrder,
        Action onCompleted)
    {
        if (!TryBuildRoute(route, out Vector3[] positions, out Quaternion[] rotations))
            return false;

        float totalDistance = CalculateRouteDistance(transform.position, positions);
        float referenceAdjacentDistance = CalculateAverageAdjacentDistance(
            completePhysicalSeatOrder);
        float duration = CalculateRouteDuration(totalDistance, referenceAdjacentDistance);
        BeginMovement(positions, rotations, duration, onCompleted);
        return true;
    }

    public void StopAuthoritativeMovement()
    {
        CancelCurrentMovement();
    }

    private void BeginMovement(
        Vector3[] targetPositions,
        Quaternion[] targetRotations,
        float duration,
        Action onCompleted)
    {
        CancelCurrentMovement();
        uint generation = movementGeneration;
        moveRoutine = StartCoroutine(MoveAlongRoute(
            targetPositions,
            targetRotations,
            duration,
            generation,
            onCompleted));
    }

    private void CancelCurrentMovement()
    {
        movementGeneration++;
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }
    }

    private IEnumerator MoveAlongRoute(
        IReadOnlyList<Vector3> targetPositions,
        IReadOnlyList<Quaternion> targetRotations,
        float duration,
        uint generation,
        Action onCompleted)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        int pointCount = targetPositions.Count + 1;
        Vector3[] positions = new Vector3[pointCount];
        Quaternion[] rotations = new Quaternion[pointCount];
        float[] cumulativeDistances = new float[pointCount];
        positions[0] = startPosition;
        rotations[0] = startRotation;

        for (int index = 1; index < pointCount; index++)
        {
            positions[index] = targetPositions[index - 1];
            rotations[index] = targetRotations[index - 1];
            cumulativeDistances[index] = cumulativeDistances[index - 1] +
                Vector3.Distance(positions[index - 1], positions[index]);
        }

        float totalDistance = cumulativeDistances[pointCount - 1];
        LogDebug(
            $"BookMover.MoveAlongRoute | Book: {name} | Segments: {pointCount - 1} | Distance: {totalDistance} | Duration: {duration}");

        if (duration <= 0f || totalDistance <= Mathf.Epsilon)
        {
            transform.SetPositionAndRotation(
                positions[pointCount - 1],
                rotations[pointCount - 1]);
            yield return null;
            CompleteMovement(generation, onCompleted);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            float travelledDistance = totalDistance * t;
            int segmentIndex = FindSegment(cumulativeDistances, travelledDistance);
            float segmentStartDistance = cumulativeDistances[segmentIndex];
            float segmentLength = cumulativeDistances[segmentIndex + 1] - segmentStartDistance;
            float segmentT = segmentLength > Mathf.Epsilon
                ? (travelledDistance - segmentStartDistance) / segmentLength
                : 1f;

            transform.position = Vector3.Lerp(
                positions[segmentIndex],
                positions[segmentIndex + 1],
                segmentT);
            transform.rotation = Quaternion.Slerp(
                rotations[segmentIndex],
                rotations[segmentIndex + 1],
                segmentT);

            yield return null;
        }

        transform.SetPositionAndRotation(
            positions[pointCount - 1],
            rotations[pointCount - 1]);
        CompleteMovement(generation, onCompleted);
    }

    private void CompleteMovement(uint generation, Action onCompleted)
    {
        if (generation != movementGeneration)
            return;

        moveRoutine = null;
        onCompleted?.Invoke();
    }

    private static int FindSegment(IReadOnlyList<float> cumulativeDistances, float distance)
    {
        for (int index = 0; index < cumulativeDistances.Count - 1; index++)
        {
            if (distance <= cumulativeDistances[index + 1])
                return index;
        }

        return cumulativeDistances.Count - 2;
    }

    private static bool TryBuildRoute(
        IReadOnlyList<Seat> route,
        out Vector3[] positions,
        out Quaternion[] rotations)
    {
        if (route == null || route.Count == 0)
        {
            positions = null;
            rotations = null;
            return false;
        }

        positions = new Vector3[route.Count];
        rotations = new Quaternion[route.Count];
        for (int index = 0; index < route.Count; index++)
        {
            Transform destination = route[index] != null
                ? route[index].GetBookDestination()
                : null;
            if (destination == null)
            {
                positions = null;
                rotations = null;
                return false;
            }

            positions[index] = destination.position;
            rotations[index] = destination.rotation;
        }

        return true;
    }

    private static float CalculateRouteDistance(
        Vector3 startPosition,
        IReadOnlyList<Vector3> targetPositions)
    {
        float distance = 0f;
        Vector3 previousPosition = startPosition;
        foreach (Vector3 targetPosition in targetPositions)
        {
            distance += Vector3.Distance(previousPosition, targetPosition);
            previousPosition = targetPosition;
        }

        return distance;
    }

    private static float CalculateAverageAdjacentDistance(
        IReadOnlyList<Seat> physicalSeatOrder)
    {
        if (physicalSeatOrder == null || physicalSeatOrder.Count < 2)
            return 0f;

        float totalDistance = 0f;
        int segmentCount = 0;
        for (int index = 0; index < physicalSeatOrder.Count; index++)
        {
            Seat currentSeat = physicalSeatOrder[index];
            Seat nextSeat = physicalSeatOrder[(index + 1) % physicalSeatOrder.Count];
            Transform currentDestination = currentSeat != null
                ? currentSeat.GetBookDestination()
                : null;
            Transform nextDestination = nextSeat != null
                ? nextSeat.GetBookDestination()
                : null;
            if (currentDestination == null || nextDestination == null)
                continue;

            totalDistance += Vector3.Distance(
                currentDestination.position,
                nextDestination.position);
            segmentCount++;
        }

        return segmentCount > 0 ? totalDistance / segmentCount : 0f;
    }

    private float CalculateRouteDuration(float totalDistance, float referenceAdjacentDistance)
    {
        if (moveDuration <= 0f || totalDistance <= Mathf.Epsilon)
            return 0f;

        if (referenceAdjacentDistance <= Mathf.Epsilon)
            return moveDuration;

        return moveDuration * totalDistance / referenceAdjacentDistance;
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
