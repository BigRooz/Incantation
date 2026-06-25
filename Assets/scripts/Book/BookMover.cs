using System.Collections;
using UnityEngine;

public class BookMover : MonoBehaviour
{
    [Header("Mouvement")]
    public float moveDuration = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Coroutine moveRoutine;

    public void MoveToSeat(Seat seat)
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
}
