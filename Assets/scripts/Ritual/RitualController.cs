using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class RitualController : MonoBehaviour
{
    private const float HourglassDuration = 10f;

    [Header("References")]
    [SerializeField] private SeatManager seatManager;
    [SerializeField] private BookMover bookMover;
    [SerializeField] private HourglassController hourglassController;
    [SerializeField] private IncantationManager incantationManager;
    [SerializeField] private MonoBehaviour voiceRecognizerBehaviour;

    [Header("Prototype")]
    [SerializeField] private bool autoStart = true;

    private Coroutine ritualRoutine;
    private IVoiceRecognizer voiceRecognizer;
    private bool hourglassFinished;
    private bool isWaitingForOccupiedSeat;
    private bool isTurnActive;
    private bool playerTurnComplete;
    private Seat lastCompletedSeat;

    public Seat CurrentActiveSeat { get; private set; }

    private void OnEnable()
    {
        SubscribeToHourglass();
        SubscribeToVoiceRecognizer();
    }

    private void OnDisable()
    {
        StopListening();
        UnsubscribeFromVoiceRecognizer();
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
        isTurnActive = false;
        playerTurnComplete = false;
        lastCompletedSeat = null;
        StopListening();

        if (hourglassController != null)
            hourglassController.StopHourglass();
    }

    private IEnumerator RitualLoop()
    {
        CurrentActiveSeat = null;

        while (true)
        {
            if (isTurnActive)
            {
                yield return null;
                continue;
            }

            if (!TrySelectNextTurnSeat())
            {
                yield return null;
                continue;
            }

            Debug.Log($"Occupied seat found: {CurrentActiveSeat.name}");

            if (!MoveBookToCurrentSeat())
            {
                yield return null;
                continue;
            }

            yield return WaitForBookMoveDuration();

            Debug.Log($"Book arrived at: {CurrentActiveSeat.name}");

            if (!IsSeatOccupied(CurrentActiveSeat))
            {
                CurrentActiveSeat = null;
                LogWaitingForOccupiedSeat();
                yield return null;
                continue;
            }

            BeginPlayerTurn();

            if (!GenerateIncantation())
            {
                CompletePlayerTurn();
                yield return null;
                continue;
            }

            StartListening();

            if (hourglassController == null)
            {
                Debug.LogWarning("RitualController requires an HourglassController reference.");
                StopListening();
                CompletePlayerTurn();
                yield return null;
                continue;
            }

            if (playerTurnComplete)
            {
                yield return null;
                continue;
            }

            yield return RunPlayerTurn();

            if (hourglassFinished && isTurnActive)
            {
                StopListening();
                Debug.Log("Incantation failed.");
                CompletePlayerTurn();
            }

            yield return null;
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

    private bool GenerateIncantation()
    {
        if (incantationManager == null)
        {
            Debug.LogWarning("RitualController requires an IncantationManager reference.");
            return false;
        }

        incantationManager.GenerateIncantation();
        Debug.Log($"Generated incantation: {GetCurrentIncantationText()}");
        return true;
    }

    private IEnumerator RunPlayerTurn()
    {
        Debug.Log("Starting hourglass");
        hourglassController.StartHourglass(HourglassDuration);

        while (!hourglassFinished && !playerTurnComplete)
            yield return null;
    }

    private bool TrySelectNextTurnSeat()
    {
        if (seatManager == null)
            return false;

        List<Seat> occupiedSeats = seatManager.GetOccupiedSeats();

        if (occupiedSeats.Count == 0)
        {
            CurrentActiveSeat = null;
            LogWaitingForOccupiedSeat();
            return false;
        }

        CurrentActiveSeat = lastCompletedSeat == null
            ? occupiedSeats[0]
            : seatManager.GetNextOccupiedSeat(lastCompletedSeat);

        if (CurrentActiveSeat == null)
        {
            LogWaitingForOccupiedSeat();
            return false;
        }

        if (!IsSeatOccupied(CurrentActiveSeat))
        {
            CurrentActiveSeat = null;
            LogWaitingForOccupiedSeat();
            return false;
        }

        if (isWaitingForOccupiedSeat)
            isWaitingForOccupiedSeat = false;

        return true;
    }

    private bool IsSeatOccupied(Seat seat)
    {
        return seat != null && !seat.IsFree();
    }

    private void LogWaitingForOccupiedSeat()
    {
        if (isWaitingForOccupiedSeat)
            return;

        Debug.Log("Waiting for occupied seat");
        isWaitingForOccupiedSeat = true;
    }

    private void BeginPlayerTurn()
    {
        hourglassFinished = false;
        playerTurnComplete = false;
        isTurnActive = true;
    }

    private void CompletePlayerTurn()
    {
        if (!isTurnActive)
            return;

        StopListening();
        isTurnActive = false;
        playerTurnComplete = true;
        lastCompletedSeat = CurrentActiveSeat;
        CurrentActiveSeat = null;
        Debug.Log("Player turn complete");
    }

    private void StartListening()
    {
        if (!ResolveVoiceRecognizer())
            return;

        if (voiceRecognizer.IsListening)
            return;

        voiceRecognizer.StartListening();
        Debug.Log("Listening started");
    }

    private void StopListening()
    {
        if (!ResolveVoiceRecognizer())
            return;

        if (!voiceRecognizer.IsListening)
            return;

        voiceRecognizer.StopListening();
        Debug.Log("Listening stopped");
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

    private void SubscribeToVoiceRecognizer()
    {
        if (!ResolveVoiceRecognizer())
            return;

        voiceRecognizer.OnPhraseRecognized += HandlePhraseRecognized;
    }

    private void UnsubscribeFromVoiceRecognizer()
    {
        if (!ResolveVoiceRecognizer())
            return;

        voiceRecognizer.OnPhraseRecognized -= HandlePhraseRecognized;
    }

    private bool ResolveVoiceRecognizer()
    {
        if (voiceRecognizerBehaviour == null)
        {
            voiceRecognizer = null;
            return false;
        }

        voiceRecognizer = voiceRecognizerBehaviour as IVoiceRecognizer;

        if (voiceRecognizer == null)
            Debug.LogWarning("RitualController voice recognizer reference must implement IVoiceRecognizer.");

        return voiceRecognizer != null;
    }

    private void HandleHourglassFinished()
    {
        hourglassFinished = true;
    }

    private void HandlePhraseRecognized(string phrase)
    {
        if (!ResolveVoiceRecognizer() || incantationManager == null || !isTurnActive || playerTurnComplete)
            return;

        bool completedCurrentWord = incantationManager.TryCompleteCurrentWord(phrase);

        if (!completedCurrentWord)
        {
            Debug.Log($"Incorrect word: {phrase}");
            return;
        }

        Debug.Log($"Correct word: {phrase}");

        if (!incantationManager.IsCompleted)
            return;

        Debug.Log("Incantation complete");

        if (hourglassController != null)
            hourglassController.StopHourglass();

        CompletePlayerTurn();
    }

    private string GetCurrentIncantationText()
    {
        if (incantationManager == null || incantationManager.CurrentIncantation.Count == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < incantationManager.CurrentIncantation.Count; i++)
        {
            if (i > 0)
                builder.Append(' ');

            builder.Append(incantationManager.CurrentIncantation[i].Text);
        }

        return builder.ToString();
    }
}
