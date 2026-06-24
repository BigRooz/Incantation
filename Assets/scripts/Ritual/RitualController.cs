using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RitualController : MonoBehaviour
{
    private const float HourglassDuration = 10f;

    [Header("References")]
    [SerializeField] private SeatManager seatManager;
    [SerializeField] private BookMover bookMover;
    [SerializeField] private HourglassController hourglassController;

    [Header("Prototype")]
    [SerializeField] private bool autoStart = true;

    private Coroutine ritualRoutine;
    private bool hourglassFinished;
    private bool isWaitingForOccupiedSeat;

    public Seat CurrentActiveSeat { get; private set; }

    private void OnEnable()
    {
        SubscribeToHourglass();
    }

    private void OnDisable()
    {
        UnsubscribeFromHourglass();
    }

    private void Start()
    {
        if (autoStart)
            StartRitual();
    }

    public void StartRitual()
    {
        if (ritualRoutine != null)
        {
            Debug.Log("StartRitual ignored because ritual is already running");
            return;
        }

        Debug.Log("Ritual started");
        ritualRoutine = StartCoroutine(RitualLoop());
    }

    public void StopRitual()
    {
        if (ritualRoutine != null)
        {
            StopCoroutine(ritualRoutine);
            ritualRoutine = null;
        }

        hourglassFinished = false;
        isWaitingForOccupiedSeat = false;
    }

    private IEnumerator RitualLoop()
    {
        CurrentActiveSeat = null;

        while (true)
        {
            if (CurrentActiveSeat == null || CurrentActiveSeat.IsFree())
                CurrentActiveSeat = GetFirstOccupiedSeat();

            if (CurrentActiveSeat == null)
            {
                if (!isWaitingForOccupiedSeat)
                {
                    Debug.Log("Waiting for occupied seat");
                    isWaitingForOccupiedSeat = true;
                }

                yield return null;
                continue;
            }

            if (isWaitingForOccupiedSeat)
                isWaitingForOccupiedSeat = false;

            Debug.Log($"Occupied seat found: {CurrentActiveSeat.name}");

            if (!MoveBookToCurrentSeat())
            {
                yield return null;
                continue;
            }

            yield return WaitForBookMoveDuration();

            Debug.Log($"Book arrived at: {CurrentActiveSeat.name}");

            yield return RunHourglass();

            Debug.Log("Hourglass finished");
            MoveToNextSeat();
        }
    }

    private bool MoveBookToCurrentSeat()
    {
        if (bookMover == null || CurrentActiveSeat == null)
            return false;

        Debug.Log($"Moving book to: {CurrentActiveSeat.name}");
        bookMover.MoveToSeat(CurrentActiveSeat);
        return true;
    }

    private IEnumerator WaitForBookMoveDuration()
    {
        if (bookMover == null)
            yield break;

        yield return new WaitForSeconds(bookMover.moveDuration);
    }

    private IEnumerator RunHourglass()
    {
        if (hourglassController == null)
            yield break;

        hourglassFinished = false;
        Debug.Log("Starting hourglass");
        hourglassController.StartHourglass(HourglassDuration);

        while (!hourglassFinished)
            yield return null;
    }

    private Seat GetFirstOccupiedSeat()
    {
        if (seatManager == null)
            return null;

        List<Seat> occupiedSeats = seatManager.GetOccupiedSeats();

        if (occupiedSeats.Count == 0)
            return null;

        return occupiedSeats[0];
    }

    private Seat GetNextOccupiedSeat(Seat currentSeat)
    {
        if (seatManager == null)
            return null;

        return seatManager.GetNextOccupiedSeat(currentSeat);
    }

    private void MoveToNextSeat()
    {
        Debug.Log("Moving to next seat");
        CurrentActiveSeat = GetNextOccupiedSeat(CurrentActiveSeat);
    }

    private void SubscribeToHourglass()
    {
        if (hourglassController == null)
            return;

        hourglassController.OnFinished.AddListener(HandleHourglassFinished);
    }

    private void UnsubscribeFromHourglass()
    {
        if (hourglassController == null)
            return;

        hourglassController.OnFinished.RemoveListener(HandleHourglassFinished);
    }

    private void HandleHourglassFinished()
    {
        hourglassFinished = true;
    }
}
