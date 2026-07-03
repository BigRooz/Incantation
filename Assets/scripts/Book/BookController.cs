using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Provides a focused adapter around BookMover for ritual systems that only need book movement.
/// Depends on BookMover for the actual transform interpolation and Seat for the destination reference.
/// This class does not choose traversal, manage players, validate phrases, or start timers.
/// TODO: Replace duration-based arrival detection when BookMover exposes a real arrival callback.
/// </summary>
public class BookController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private BookMover bookMover;

    public event Action<Seat> OnMoveStarted;
    public event Action<Seat> OnArrived;
    public event Action OnRitualAccepted;

    private Coroutine arrivalRoutine;

    public bool IsMoving { get; private set; }

    private void Awake()
    {
        if (bookMover == null)
            bookMover = GetComponent<BookMover>();
    }

    public bool MoveToSeat(Seat seat)
    {
        if (bookMover == null)
        {
            Debug.LogWarning("BookController requires a BookMover reference.");
            return false;
        }

        if (seat == null)
            return false;

        if (seat.GetBookDestination() == null)
        {
            Debug.LogWarning("BookController could not move because the Seat has no book destination.");
            return false;
        }

        if (arrivalRoutine != null)
            StopCoroutine(arrivalRoutine);

        IsMoving = true;
        OnMoveStarted?.Invoke(seat);

        bookMover.MoveToSeat(seat);

        arrivalRoutine = StartCoroutine(WaitForArrival(seat));
        return true;
    }

    public void NotifyRitualAccepted()
    {
        OnRitualAccepted?.Invoke();
    }

    private IEnumerator WaitForArrival(Seat seat)
    {
        float moveDuration = Mathf.Max(0f, bookMover.moveDuration);

        if (moveDuration > 0f)
            yield return new WaitForSeconds(moveDuration);
        else
            yield return null;

        IsMoving = false;
        arrivalRoutine = null;
        OnArrived?.Invoke(seat);
    }
}
