using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class RitualController : MonoBehaviour
{
    private const float VoiceRecognitionProcessingTimeoutSeconds = 5f;
    private static RitualController activeRitualController;

    [Header("References")]
    [SerializeField] private SeatManager seatManager;
    [SerializeField] private BookMover bookMover;
    [SerializeField] private BookController bookController;
    [SerializeField] private HourglassController hourglassController;
    [SerializeField] private IncantationManager incantationManager;
    [SerializeField] private IncantationTextDisplay incantationTextDisplay;
    [SerializeField] private CoreRitualLoopBridge coreRitualLoopBridge;

    [Header("Voice Recognizer Selection")]
    [Tooltip("Stable Play Mode default: assign WindowsKeywordVoiceRecognizer for immediate per-syllable validation. Assign WhisperVoiceRecognizer only when testing experimental full-phrase recognition.")]
    [SerializeField] private MonoBehaviour voiceRecognizerBehaviour;

    [Header("Speech Normalization")]
    [SerializeField] private VoicePhraseNormalizer voicePhraseNormalizer;
    [SerializeField] private IncantationWordLibrary speechAliasWordLibrary;

    [Header("Prototype")]
    [SerializeField] private bool autoStart = true;

    [Header("Turn Timing")]
    [Tooltip("Development turn duration in seconds. Keep this long enough for Play Mode voice recognition retries.")]
    [SerializeField, Min(5f)] private float hourglassDuration = 30f;
    [Tooltip("Delay after the successful phrase judgment replay before the book accepts the ritual and leaves.")]
    [SerializeField, Min(0f)] private float ritualAcceptancePauseSeconds = 0.75f;

    [Header("Speech Alias Learning")]
    [SerializeField] private bool enableLearningMode = false;
    [SerializeField] private string pendingRecognizedPhrase = string.Empty;
    [SerializeField] private string pendingExpectedWord = string.Empty;
    [SerializeField] private List<LearnedSpeechAlias> learnedAliases = new List<LearnedSpeechAlias>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Coroutine ritualRoutine;
    private Coroutine turnRoutine;
    private Coroutine retryListeningRoutine;
    private Coroutine successfulTurnRoutine;
    private Seat preMovedBookSeat;
    private bool currentTurnBookMoveSkipped;
    private IVoiceRecognizer voiceRecognizer;
    private IVoiceRecognizerProcessingStatus voiceRecognizerProcessingStatus;
    private IVoiceRecognizer loggedVoiceRecognizer;
    private IVoiceRecognizer subscribedVoiceRecognizer;
    private MonoBehaviour resolvedVoiceRecognizerBehaviour;
    private bool hourglassFinished;
    private bool isWaitingForOccupiedSeat;
    private bool isTurnActive;
    private bool playerTurnComplete;
    private bool ritualFailed;
    private bool hasLoggedMissingHourglass;
    private bool hasLoggedMissingIncantationManager;
    private bool hasLoggedMissingVoicePhraseNormalizer;
    private bool hasLoggedMissingSeatManager;
    private bool hasLoggedMissingBookMover;
    private bool hasLoggedMissingVoiceRecognizerBehaviour;
    private bool hasLoggedMissingSpeechAliasWordLibrary;
    private bool hasLoggedLegacyPhraseFallback;
    private bool hasLoggedRuntimeCoreRitualLoopBridgeSetup;
    private bool hasInitializedCoreRitualLoop;
    private bool isUsingCoreRitualLoopPhraseAuthority;
    private int configuredCoreRitualPlayerCount;
    private Seat lastCompletedSeat;
    private string lastProcessedWhisperPhrase = string.Empty;

    public Seat CurrentActiveSeat { get; private set; }
    public bool RitualFailed => ritualFailed;

    private void OnEnable()
    {
        SubscribeToHourglass();
        SubscribeToVoiceRecognizer();
    }

    private void OnDisable()
    {
        StopRitual();
        UnsubscribeFromVoiceRecognizer();
        UnsubscribeFromHourglass();
    }

    private void Start()
    {
        ValidateRequiredReferences();

        if (autoStart)
            StartRitual();
    }

    private void Update()
    {
        if (!Application.isPlaying || !enableLearningMode || !HasPendingAlias())
            return;

        if (Input.GetKeyDown(KeyCode.A))
            AcceptPendingAlias();
        else if (Input.GetKeyDown(KeyCode.D))
            IgnorePendingAlias();
    }

    public void StartRitual()
    {
        if (activeRitualController != null && activeRitualController != this)
        {
            Debug.LogWarning("StartRitual ignored because another RitualController is already running");
            return;
        }

        if (ritualRoutine != null)
        {
            LogDebug("StartRitual ignored because ritual is already running");
            return;
        }

        Debug.Log("Ritual started");
        ritualFailed = false;
        activeRitualController = this;
        ritualRoutine = StartCoroutine(RitualLoop());
    }

    public void StopRitual()
    {
        if (turnRoutine != null)
        {
            StopCoroutine(turnRoutine);
            turnRoutine = null;
        }

        StopRetryListeningRoutine();
        StopSuccessfulTurnRoutine();

        if (ritualRoutine != null)
        {
            StopCoroutine(ritualRoutine);
            ritualRoutine = null;
        }

        if (activeRitualController == this)
            activeRitualController = null;

        hourglassFinished = false;
        isWaitingForOccupiedSeat = false;
        isTurnActive = false;
        playerTurnComplete = false;
        ritualFailed = false;
        hasLoggedMissingHourglass = false;
        hasLoggedMissingIncantationManager = false;
        hasLoggedMissingVoicePhraseNormalizer = false;
        hasLoggedMissingSeatManager = false;
        hasLoggedMissingBookMover = false;
        hasLoggedMissingVoiceRecognizerBehaviour = false;
        hasLoggedMissingSpeechAliasWordLibrary = false;
        hasLoggedLegacyPhraseFallback = false;
        hasLoggedRuntimeCoreRitualLoopBridgeSetup = false;
        hasInitializedCoreRitualLoop = false;
        isUsingCoreRitualLoopPhraseAuthority = false;
        configuredCoreRitualPlayerCount = 0;
        lastCompletedSeat = null;
        preMovedBookSeat = null;
        currentTurnBookMoveSkipped = false;
        lastProcessedWhisperPhrase = string.Empty;
        StopListening();

        if (hourglassController != null)
            hourglassController.StopHourglass();
    }

    private IEnumerator RitualLoop()
    {
        CurrentActiveSeat = null;

        while (true)
        {
            if (ritualFailed)
            {
                LogDebug("RitualLoop stopped because the ritual is in a failed state.");
                yield break;
            }

            if (turnRoutine != null)
            {
                yield return null;
                continue;
            }

            if (!TrySelectNextTurnSeat())
            {
                yield return null;
                continue;
            }

            turnRoutine = StartCoroutine(RunTurn(CurrentActiveSeat));
            yield return turnRoutine;
            turnRoutine = null;
            yield return null;
        }
    }

    private IEnumerator RunTurn(Seat turnSeat)
    {
        CurrentActiveSeat = turnSeat;

        if (!IsSeatOccupied(CurrentActiveSeat))
        {
            ClearPreMovedBookSeatIfMatches(CurrentActiveSeat);
            CurrentActiveSeat = null;
            LogWaitingForOccupiedSeat();
            yield break;
        }

        if (!HasRequiredTurnReferences())
            yield break;

        LogDebug($"Occupied seat found: {CurrentActiveSeat.name}");

        if (!MoveBookToCurrentSeat())
            yield break;

        yield return WaitForBookMoveDuration();

        if (!currentTurnBookMoveSkipped)
            Debug.Log($"Book movement completed to active seat: {CurrentActiveSeat.name}.");

        LogDebug($"Book arrived at: {CurrentActiveSeat.name}");

        if (!IsSeatOccupied(CurrentActiveSeat))
        {
            ClearPreMovedBookSeatIfMatches(CurrentActiveSeat);
            CurrentActiveSeat = null;
            LogWaitingForOccupiedSeat();
            yield break;
        }

        BeginPlayerTurn();

        if (!GenerateIncantation())
        {
            CompletePlayerTurn();
            yield break;
        }

        StartListening();

        if (hourglassController == null)
        {
            Debug.LogWarning("RitualController requires an HourglassController reference.");
            StopListening();
            CompletePlayerTurn();
            yield break;
        }

        if (playerTurnComplete)
            yield break;

        yield return RunPlayerTurn();

        if (hourglassFinished && isTurnActive)
        {
            bool isUsingWhisperRecognizer = IsUsingWhisperRecognizer();

            StopListening();

            if (isUsingWhisperRecognizer)
                yield return WaitForVoiceRecognitionProcessing();

            if (!isTurnActive || playerTurnComplete)
                yield break;

            string timeoutReason = isUsingWhisperRecognizer
                ? "Timeout: no valid full phrase recognized before the hourglass finished."
                : "Timeout: no valid keyword recognized before the hourglass finished.";

            Debug.Log("Ritual phrase timeout ended turn.");
            Debug.Log(timeoutReason);
            FailRitual(timeoutReason);
        }
    }

    private bool MoveBookToCurrentSeat()
    {
        if (bookMover == null)
        {
            LogMissingBookMover();
            return false;
        }

        if (CurrentActiveSeat == null)
            return false;

        if (preMovedBookSeat == CurrentActiveSeat && IsBookAlreadyAtSeat(CurrentActiveSeat))
        {
            preMovedBookSeat = null;
            currentTurnBookMoveSkipped = true;
            Debug.Log($"Book movement skipped with reason: Book already moved to next active seat {CurrentActiveSeat.name} after ritual acceptance.");
            return true;
        }

        if (preMovedBookSeat != null)
            preMovedBookSeat = null;

        currentTurnBookMoveSkipped = false;
        LogDebug($"Moving book to: {CurrentActiveSeat.name}");

        return MoveBookToSeat(CurrentActiveSeat);
    }

    private bool MoveBookToSeat(Seat seat)
    {
        if (bookMover == null)
        {
            LogMissingBookMover();
            return false;
        }

        if (seat == null)
            return false;

        if (seat.GetBookDestination() == null)
        {
            Debug.LogWarning($"Book movement skipped with reason: target seat {seat.name} has no book destination.", this);
            return false;
        }

        BookController resolvedBookController = ResolveBookController();
        bool didStartMovement = false;

        if (resolvedBookController != null)
            didStartMovement = resolvedBookController.MoveToSeat(seat);

        if (!didStartMovement)
        {
            if (resolvedBookController != null)
                Debug.LogWarning($"BookController could not start movement to {seat.name}; falling back to BookMover.", this);

            bookMover.MoveToSeat(seat);
            didStartMovement = true;
        }

        if (seatManager != null)
            seatManager.SetCurrentBookSeat(seat);

        return didStartMovement;
    }

    private bool IsBookAlreadyAtSeat(Seat seat)
    {
        return seatManager != null && seatManager.currentBookSeat == seat;
    }

    private void ClearPreMovedBookSeatIfMatches(Seat seat)
    {
        if (preMovedBookSeat == seat)
            preMovedBookSeat = null;
    }

    private BookController ResolveBookController()
    {
        if (bookController != null)
            return bookController;

        if (bookMover != null)
            bookController = bookMover.GetComponent<BookController>();

        return bookController;
    }

    private IEnumerator WaitForBookMoveDuration()
    {
        if (currentTurnBookMoveSkipped)
            yield break;

        if (bookMover == null)
            yield break;

        yield return new WaitForSeconds(bookMover.moveDuration);
    }

    private bool GenerateIncantation()
    {
        if (incantationManager == null)
        {
            LogMissingIncantationManager();
            return false;
        }

        if (TryGenerateCoreRitualLoopIncantation())
        {
            Debug.Log($"Generated incantation: {GetCurrentIncantationText()}");
            return true;
        }

        LogLegacyPhraseFallback();
        isUsingCoreRitualLoopPhraseAuthority = false;
        incantationManager.GenerateIncantation();
        Debug.Log($"Generated incantation: {GetCurrentIncantationText()}");
        return true;
    }

    private bool HasRequiredTurnReferences()
    {
        bool hasRequiredReferences = true;

        if (hourglassController == null)
        {
            LogMissingHourglass();
            hasRequiredReferences = false;
        }

        if (incantationManager == null)
        {
            LogMissingIncantationManager();
            hasRequiredReferences = false;
        }

        if (voiceRecognizerBehaviour == null)
        {
            LogMissingVoiceRecognizerBehaviour();
            hasRequiredReferences = false;
        }

        if (voicePhraseNormalizer == null)
            LogMissingVoicePhraseNormalizer();

        if (speechAliasWordLibrary == null)
            LogMissingSpeechAliasWordLibrary();

        return hasRequiredReferences;
    }

    private void LogMissingHourglass()
    {
        if (hasLoggedMissingHourglass)
            return;

        LogMissingRequiredReference(nameof(hourglassController));
        hasLoggedMissingHourglass = true;
    }

    private void LogMissingIncantationManager()
    {
        if (hasLoggedMissingIncantationManager)
            return;

        LogMissingRequiredReference(nameof(incantationManager));
        hasLoggedMissingIncantationManager = true;
    }

    private void LogMissingSeatManager()
    {
        if (hasLoggedMissingSeatManager)
            return;

        LogMissingRequiredReference(nameof(seatManager));
        hasLoggedMissingSeatManager = true;
    }

    private void LogMissingBookMover()
    {
        if (hasLoggedMissingBookMover)
            return;

        LogMissingRequiredReference(nameof(bookMover));
        hasLoggedMissingBookMover = true;
    }

    private void LogMissingVoiceRecognizerBehaviour()
    {
        if (hasLoggedMissingVoiceRecognizerBehaviour)
            return;

        LogMissingRequiredReference(nameof(voiceRecognizerBehaviour));
        hasLoggedMissingVoiceRecognizerBehaviour = true;
    }

    private void LogMissingVoicePhraseNormalizer()
    {
        if (hasLoggedMissingVoicePhraseNormalizer)
            return;

        LogMissingRequiredReference(nameof(voicePhraseNormalizer));
        hasLoggedMissingVoicePhraseNormalizer = true;
    }

    private void LogMissingSpeechAliasWordLibrary()
    {
        if (hasLoggedMissingSpeechAliasWordLibrary)
            return;

        LogMissingRequiredReference(nameof(speechAliasWordLibrary));
        hasLoggedMissingSpeechAliasWordLibrary = true;
    }

    private void LogMissingRequiredReference(string fieldName)
    {
        Debug.LogWarning($"{nameof(RitualController)} on '{gameObject.name}' is missing required reference '{fieldName}'. Assign it in the Inspector.", this);
    }

    private IEnumerator RunPlayerTurn()
    {
        LogDebug("Starting hourglass");
        hourglassController.StartHourglass(hourglassDuration);

        while (!hourglassFinished && !playerTurnComplete)
            yield return null;
    }

    private bool TrySelectNextTurnSeat()
    {
        if (seatManager == null)
        {
            LogMissingSeatManager();
            return false;
        }

        List<Seat> occupiedSeats = seatManager.GetOccupiedSeats();

        if (occupiedSeats.Count == 0)
        {
            CurrentActiveSeat = null;
            LogWaitingForOccupiedSeat();
            return false;
        }

        if (lastCompletedSeat == null)
            Debug.Log($"Occupied seats before ritual:\n{FormatSeatList(occupiedSeats)}");

        CurrentActiveSeat = lastCompletedSeat == null
            ? occupiedSeats[0]
            : GetNextOccupiedSeatFromList(occupiedSeats, lastCompletedSeat);

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

    private Seat GetNextOccupiedSeatFromList(List<Seat> occupiedSeats, Seat currentSeat)
    {
        if (occupiedSeats == null || occupiedSeats.Count == 0)
            return null;

        if (currentSeat == null)
            return occupiedSeats[0];

        int currentIndex = occupiedSeats.IndexOf(currentSeat);

        if (currentIndex < 0)
            return occupiedSeats[0];

        int nextIndex = currentIndex + 1;

        if (nextIndex >= occupiedSeats.Count)
            nextIndex = 0;

        return occupiedSeats[nextIndex];
    }

    private bool IsSeatOccupied(Seat seat)
    {
        return seat != null && !seat.IsFree();
    }

    private void LogWaitingForOccupiedSeat()
    {
        if (isWaitingForOccupiedSeat)
            return;

        LogDebug("Waiting for occupied seat");
        isWaitingForOccupiedSeat = true;
    }

    private void BeginPlayerTurn()
    {
        hourglassFinished = false;
        playerTurnComplete = false;
        isTurnActive = true;
        lastProcessedWhisperPhrase = string.Empty;
    }

    private void CompletePlayerTurn()
    {
        if (!isTurnActive || ritualFailed)
            return;

        StopRetryListeningRoutine();
        StopListening();
        isTurnActive = false;
        playerTurnComplete = true;
        lastCompletedSeat = CurrentActiveSeat;
        CurrentActiveSeat = null;
        LogDebug("Player turn complete");
    }

    private void StartListening()
    {
        if (!ResolveVoiceRecognizer())
            return;

        EnsureVoiceRecognizerSubscription();

        if (voiceRecognizer.IsListening)
            return;

        ConfigureWhisperListeningContext();
        lastProcessedWhisperPhrase = string.Empty;
        Debug.Log($"Ritual phrase attempt started with {GetActiveVoiceRecognizerTypeName()}.");
        voiceRecognizer.StartListening();
        Debug.Log($"Ritual listening started with {GetActiveVoiceRecognizerTypeName()}. Local active player microphone only.");
    }

    private void StopListening()
    {
        if (!ResolveVoiceRecognizer())
            return;

        if (!voiceRecognizer.IsListening)
            return;

        LogDebug($"RitualController StopListening started for {GetActiveVoiceRecognizerTypeName()}.");
        voiceRecognizer.StopListening();
        LogDebug("Listening stopped");
    }

    private IEnumerator WaitForVoiceRecognitionProcessing()
    {
        if (!ResolveVoiceRecognizer() || voiceRecognizerProcessingStatus == null)
            yield break;

        if (!voiceRecognizerProcessingStatus.IsProcessingRecognition)
            yield break;

        float elapsedSeconds = 0f;
        LogDebug($"RitualController transcription pending for {GetActiveVoiceRecognizerTypeName()}; waiting for recognition result.");

        while (isTurnActive &&
            !playerTurnComplete &&
            !ritualFailed &&
            voiceRecognizerProcessingStatus.IsProcessingRecognition &&
            elapsedSeconds < VoiceRecognitionProcessingTimeoutSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            yield return null;
        }

        if (voiceRecognizerProcessingStatus.IsProcessingRecognition)
            LogDebug($"RitualController voice recognition wait timed out after {VoiceRecognitionProcessingTimeoutSeconds:0.0}s.");
        else
            LogDebug($"RitualController voice recognition processing finished after {elapsedSeconds:0.00}s.");
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

        EnsureVoiceRecognizerSubscription();
    }

    private void UnsubscribeFromVoiceRecognizer()
    {
        ClearVoiceRecognizerSubscription();
    }

    private void EnsureVoiceRecognizerSubscription()
    {
        if (voiceRecognizer == null || ReferenceEquals(subscribedVoiceRecognizer, voiceRecognizer))
            return;

        ClearVoiceRecognizerSubscription();
        voiceRecognizer.OnPhraseRecognized += HandlePhraseRecognized;
        subscribedVoiceRecognizer = voiceRecognizer;
    }

    private void ClearVoiceRecognizerSubscription()
    {
        if (subscribedVoiceRecognizer == null)
            return;

        subscribedVoiceRecognizer.OnPhraseRecognized -= HandlePhraseRecognized;
        subscribedVoiceRecognizer = null;
    }

    private bool ResolveVoiceRecognizer()
    {
        if (voiceRecognizerBehaviour == null)
        {
            voiceRecognizer = null;
            voiceRecognizerProcessingStatus = null;
            loggedVoiceRecognizer = null;
            resolvedVoiceRecognizerBehaviour = null;
            ClearVoiceRecognizerSubscription();
            LogMissingVoiceRecognizerBehaviour();
            return false;
        }

        MonoBehaviour referencedBehaviour = voiceRecognizerBehaviour;
        bool referencedImplementsVoiceRecognizer = referencedBehaviour is IVoiceRecognizer;

        voiceRecognizer = referencedBehaviour as IVoiceRecognizer;
        resolvedVoiceRecognizerBehaviour = referencedImplementsVoiceRecognizer
            ? referencedBehaviour
            : ResolveVoiceRecognizerFromGameObject(referencedBehaviour);
        voiceRecognizerProcessingStatus = voiceRecognizer as IVoiceRecognizerProcessingStatus;

        if (voiceRecognizer == null)
        {
            LogDebug(
                $"RitualController voice recognizer reference: referencedType={referencedBehaviour.GetType().Name}, " +
                $"implementsIVoiceRecognizer={referencedImplementsVoiceRecognizer}, finalResolvedType=None.");
            Debug.LogWarning($"{nameof(RitualController)} on '{gameObject.name}' has voiceRecognizerBehaviour assigned to '{referencedBehaviour.gameObject.name}', but no component on that GameObject implements IVoiceRecognizer.", this);
            voiceRecognizerProcessingStatus = null;
            loggedVoiceRecognizer = null;
            ClearVoiceRecognizerSubscription();
        }
        else if (!ReferenceEquals(loggedVoiceRecognizer, voiceRecognizer))
        {
            string resolvedTypeName = resolvedVoiceRecognizerBehaviour != null
                ? resolvedVoiceRecognizerBehaviour.GetType().Name
                : voiceRecognizer.GetType().Name;

            LogDebug(
                $"RitualController voice recognizer reference: referencedType={referencedBehaviour.GetType().Name}, " +
                $"implementsIVoiceRecognizer={referencedImplementsVoiceRecognizer}, finalResolvedType={resolvedTypeName}.");
            Debug.Log($"RitualController active voice recognizer: {resolvedTypeName} on {referencedBehaviour.gameObject.name}.", resolvedVoiceRecognizerBehaviour != null ? resolvedVoiceRecognizerBehaviour : referencedBehaviour);
            loggedVoiceRecognizer = voiceRecognizer;
        }

        return voiceRecognizer != null;
    }

    private string GetActiveVoiceRecognizerTypeName()
    {
        if (resolvedVoiceRecognizerBehaviour != null)
            return resolvedVoiceRecognizerBehaviour.GetType().Name;

        return voiceRecognizer != null ? voiceRecognizer.GetType().Name : "None";
    }

    private MonoBehaviour ResolveVoiceRecognizerFromGameObject(MonoBehaviour referencedBehaviour)
    {
        if (referencedBehaviour == null)
            return null;

        MonoBehaviour[] behaviours = referencedBehaviour.GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IVoiceRecognizer resolvedRecognizer)
            {
                voiceRecognizer = resolvedRecognizer;
                return behaviour;
            }
        }

        return null;
    }

    private void HandleHourglassFinished()
    {
        hourglassFinished = true;
    }

    private void ValidateRequiredReferences()
    {
        if (seatManager == null)
            LogMissingSeatManager();

        if (bookMover == null)
            LogMissingBookMover();

        if (hourglassController == null)
            LogMissingHourglass();

        if (incantationManager == null)
            LogMissingIncantationManager();

        if (voiceRecognizerBehaviour == null)
            LogMissingVoiceRecognizerBehaviour();

        if (voicePhraseNormalizer == null)
            LogMissingVoicePhraseNormalizer();

        if (speechAliasWordLibrary == null)
            LogMissingSpeechAliasWordLibrary();
    }

    private void HandlePhraseRecognized(string recognizedPhrase)
    {
        if (ritualFailed || playerTurnComplete || !isTurnActive)
            return;

        if (IsEmptySpeechUpdate(recognizedPhrase))
            return;

        if (!ResolveVoiceRecognizer() || incantationManager == null)
            return;

        bool isUsingWhisperRecognizer = IsUsingWhisperRecognizer();

        if (isUsingWhisperRecognizer && IsDuplicateWhisperUpdate(recognizedPhrase))
            return;

        ResolveVoicePhraseNormalizer();
        string normalizedPhrase = voicePhraseNormalizer != null
            ? voicePhraseNormalizer.Normalize(recognizedPhrase)
            : recognizedPhrase;

        if (isUsingWhisperRecognizer)
        {
            ProcessWhisperPhraseRecognition(recognizedPhrase, normalizedPhrase);
            return;
        }

        ProcessSequentialWordRecognition(recognizedPhrase, normalizedPhrase);
    }

    private void ProcessWhisperPhraseRecognition(string recognizedPhrase, string normalizedPhrase)
    {
        Debug.Log($"Ritual phrase transcript received: {recognizedPhrase}");
        Debug.Log($"Final phrase recognized: {recognizedPhrase}");
        Debug.Log($"Normalized final phrase: {normalizedPhrase}");

        if (!incantationManager.TryCompleteCurrentPhrase(recognizedPhrase, voicePhraseNormalizer))
        {
            incantationManager.ResetCurrentPhraseProgress();
            Debug.Log($"Phrase incomplete after transcript: {normalizedPhrase}");
            RestartListeningAfterFailedAttempt();
            return;
        }

        Debug.Log($"Ritual phrase attempt succeeded: {normalizedPhrase}");
        Debug.Log($"Sequential phrase success: {normalizedPhrase}");
        LogDebug("Incantation complete");

        UnsubscribeFromVoiceRecognizer();
        StopListening();

        if (hourglassController != null)
            hourglassController.StopHourglass();

        CompleteSuccessfulPlayerTurn();
    }

    private void ProcessSequentialWordRecognition(string recognizedPhrase, string normalizedPhrase)
    {
        Debug.Log($"Recognized phrase: {recognizedPhrase}");
        Debug.Log($"Normalized phrase: {normalizedPhrase}");

        string expectedWord = incantationManager.CurrentWord;
        bool completedCurrentWord = incantationManager.TryValidateCurrentWordRealtime(recognizedPhrase, voicePhraseNormalizer);

        if (!completedCurrentWord)
        {
            Debug.Log($"Incorrect word: {normalizedPhrase}");
            HandleSpeechAliasSuggestion(recognizedPhrase, expectedWord);
            return;
        }

        Debug.Log($"Correct word: {normalizedPhrase}");

        if (!incantationManager.IsCompleted)
            return;

        LogDebug("Incantation complete");

        if (hourglassController != null)
            hourglassController.StopHourglass();

        CompleteSuccessfulPlayerTurn();
    }

    private void CompleteSuccessfulPlayerTurn()
    {
        if (!isTurnActive || playerTurnComplete || ritualFailed || successfulTurnRoutine != null)
            return;

        successfulTurnRoutine = StartCoroutine(CompleteSuccessfulPlayerTurnSequence());
    }

    private IEnumerator CompleteSuccessfulPlayerTurnSequence()
    {
        yield return WaitForPhraseReplayToFinish();

        if (!isTurnActive || playerTurnComplete || ritualFailed)
        {
            successfulTurnRoutine = null;
            yield break;
        }

        if (ritualAcceptancePauseSeconds > 0f)
        {
            Debug.Log($"Book acceptance pause started for {ritualAcceptancePauseSeconds:0.00}s.");
            yield return new WaitForSeconds(ritualAcceptancePauseSeconds);
        }

        if (!isTurnActive || playerTurnComplete || ritualFailed)
        {
            successfulTurnRoutine = null;
            yield break;
        }

        Seat completedSeat = CurrentActiveSeat;
        BookController resolvedBookController = ResolveBookController();

        Debug.Log("Book accepted ritual.");

        if (resolvedBookController != null)
            resolvedBookController.NotifyRitualAccepted();

        TryAdvanceCoreRitualLoopAfterSuccessfulTurn();

        yield return MoveBookToNextActiveSeatAfterSuccess(completedSeat);

        CompletePlayerTurn();
        successfulTurnRoutine = null;
    }

    private IEnumerator MoveBookToNextActiveSeatAfterSuccess(Seat completedSeat)
    {
        if (completedSeat == null)
        {
            Debug.Log("Book movement skipped with reason: completed seat was missing after ritual acceptance.");
            yield break;
        }

        if (seatManager == null)
        {
            LogMissingSeatManager();
            Debug.Log("Book movement skipped with reason: SeatManager reference is missing.");
            yield break;
        }

        List<Seat> occupiedSeats = seatManager.GetOccupiedSeats();
        Debug.Log($"Occupied seats after successful ritual:\n{FormatSeatList(occupiedSeats)}");

        Seat nextSeat = GetNextOccupiedSeatFromList(occupiedSeats, completedSeat);

        if (nextSeat == null)
        {
            Debug.Log("Book movement skipped with reason: no next occupied active seat was found.");
            yield break;
        }

        Debug.Log($"Selected next seat: {nextSeat.name}");

        if (nextSeat == completedSeat)
        {
            Debug.Log($"Book movement skipped with reason: only one occupied active seat is available ({completedSeat.name}).");
            yield break;
        }

        if (nextSeat.GetBookDestination() == null)
        {
            Debug.LogWarning($"Book movement skipped with reason: next active seat {nextSeat.name} has no book destination.");
            yield break;
        }

        Debug.Log($"Book moving to next seat: {nextSeat.name}.");

        if (!MoveBookToSeat(nextSeat))
        {
            Debug.LogWarning($"Book movement skipped with reason: movement command failed for next active seat {nextSeat.name}.");
            yield break;
        }

        preMovedBookSeat = nextSeat;

        if (bookMover != null && bookMover.moveDuration > 0f)
            yield return new WaitForSeconds(bookMover.moveDuration);
        else
            yield return null;

        Debug.Log($"Book movement completed to next seat: {nextSeat.name}.");
    }

    private string FormatSeatList(List<Seat> seatsToFormat)
    {
        if (seatsToFormat == null || seatsToFormat.Count == 0)
            return "(none)";

        StringBuilder builder = new StringBuilder();

        for (int seatIndex = 0; seatIndex < seatsToFormat.Count; seatIndex++)
        {
            Seat seat = seatsToFormat[seatIndex];

            if (seatIndex > 0)
                builder.AppendLine();

            builder.Append(seatIndex);
            builder.Append(": ");
            builder.Append(seat != null ? seat.name : "(missing seat)");
        }

        return builder.ToString();
    }

    private IEnumerator WaitForPhraseReplayToFinish()
    {
        if (incantationManager == null)
            yield break;

        while (incantationManager.HasActivePhraseReplay)
            yield return null;

        IncantationTextDisplay resolvedIncantationTextDisplay = ResolveIncantationTextDisplay();

        while (resolvedIncantationTextDisplay != null && resolvedIncantationTextDisplay.IsReplayingJudgment)
            yield return null;
    }

    private IncantationTextDisplay ResolveIncantationTextDisplay()
    {
        if (incantationTextDisplay != null)
            return incantationTextDisplay;

        incantationTextDisplay = FindFirstObjectByType<IncantationTextDisplay>();
        return incantationTextDisplay;
    }

    private bool IsUsingWhisperRecognizer()
    {
        return voiceRecognizer is WhisperVoiceRecognizer ||
            resolvedVoiceRecognizerBehaviour is WhisperVoiceRecognizer;
    }

    private void ConfigureWhisperListeningContext()
    {
        WhisperVoiceRecognizer whisperVoiceRecognizer = ResolveWhisperVoiceRecognizer();

        if (whisperVoiceRecognizer == null)
            return;

        whisperVoiceRecognizer.SetExpectedPhraseWordCount(GetCurrentIncantationWordCount());
    }

    private WhisperVoiceRecognizer ResolveWhisperVoiceRecognizer()
    {
        if (voiceRecognizer is WhisperVoiceRecognizer directWhisperVoiceRecognizer)
            return directWhisperVoiceRecognizer;

        return resolvedVoiceRecognizerBehaviour as WhisperVoiceRecognizer;
    }

    private int GetCurrentIncantationWordCount()
    {
        if (incantationManager == null)
            return 1;

        int wordCount = incantationManager.CurrentIncantation.Count;
        return Mathf.Max(1, wordCount);
    }

    private bool IsEmptySpeechUpdate(string recognizedPhrase)
    {
        if (string.IsNullOrWhiteSpace(recognizedPhrase))
            return true;

        return !ContainsSpeechCharacter(recognizedPhrase);
    }

    private bool IsDuplicateWhisperUpdate(string recognizedPhrase)
    {
        if (!string.Equals(recognizedPhrase, lastProcessedWhisperPhrase, StringComparison.Ordinal))
        {
            lastProcessedWhisperPhrase = recognizedPhrase;
            return false;
        }

        return true;
    }

    private bool ContainsSpeechCharacter(string phrase)
    {
        foreach (char character in phrase)
        {
            if (char.IsLetterOrDigit(character))
                return true;
        }

        return false;
    }

    private bool IsRecognizedPhraseValid(string normalizedPhrase)
    {
        if (isUsingCoreRitualLoopPhraseAuthority && TryResolveReadyCoreRitualLoopBridge())
            return coreRitualLoopBridge.ValidatePhrase(normalizedPhrase);

        return CoreRitualLoop.ValidatePhraseCandidate(GetNormalizedCurrentIncantationText(), normalizedPhrase);
    }

    private void FailRitual(string reason)
    {
        if (ritualFailed)
            return;

        ritualFailed = true;
        Debug.Log($"Ritual failed. {reason}");

        StopRetryListeningRoutine();
        UnsubscribeFromVoiceRecognizer();
        StopListening();

        if (hourglassController != null)
            hourglassController.StopHourglass();

        isTurnActive = false;
        playerTurnComplete = true;
        LogDebug("Ritual is now in failed state. No next seat, incantation, book move, or hourglass restart will start automatically.");
    }

    private void RestartListeningAfterFailedAttempt()
    {
        if (!isTurnActive || playerTurnComplete || ritualFailed)
            return;

        if (hourglassFinished)
        {
            Debug.Log("Ritual phrase attempt failed after timeout; no retry will start.");
            return;
        }

        if (retryListeningRoutine != null)
            StopCoroutine(retryListeningRoutine);

        Debug.Log("Ritual phrase attempt failed, retrying while hourglass is still running.");
        retryListeningRoutine = StartCoroutine(RestartListeningWhenRecognizerReady());
    }

    private IEnumerator RestartListeningWhenRecognizerReady()
    {
        while (isTurnActive &&
            !playerTurnComplete &&
            !ritualFailed &&
            !hourglassFinished &&
            IsVoiceRecognizerBusy())
        {
            yield return null;
        }

        retryListeningRoutine = null;

        if (!isTurnActive || playerTurnComplete || ritualFailed || hourglassFinished)
            yield break;

        if (incantationManager != null)
            incantationManager.ResetPhraseReplayFeedback();

        StartListening();
    }

    private bool IsVoiceRecognizerBusy()
    {
        if (!ResolveVoiceRecognizer())
            return false;

        if (voiceRecognizer.IsListening)
            return true;

        return voiceRecognizerProcessingStatus != null && voiceRecognizerProcessingStatus.IsProcessingRecognition;
    }

    private void StopRetryListeningRoutine()
    {
        if (retryListeningRoutine == null)
            return;

        StopCoroutine(retryListeningRoutine);
        retryListeningRoutine = null;
    }

    private void StopSuccessfulTurnRoutine()
    {
        if (successfulTurnRoutine == null)
            return;

        StopCoroutine(successfulTurnRoutine);
        successfulTurnRoutine = null;
    }

    private void HandleSpeechAliasSuggestion(string recognizedPhrase, string expectedWord)
    {
        if (!enableLearningMode)
            return;

        string normalizedRecognizedPhrase = NormalizeSpeechText(recognizedPhrase);
        string normalizedExpectedWord = NormalizeSpeechText(expectedWord);

        if (string.IsNullOrEmpty(normalizedRecognizedPhrase) || string.IsNullOrEmpty(normalizedExpectedWord))
            return;

        if (IsSpeechAliasForExpectedWord(recognizedPhrase, expectedWord))
            return;

        pendingRecognizedPhrase = recognizedPhrase.Trim();
        pendingExpectedWord = expectedWord.Trim();
        LogSpeechLearningBlock(pendingRecognizedPhrase, pendingExpectedWord);
    }

    private VoicePhraseNormalizer ResolveVoicePhraseNormalizer()
    {
        if (voicePhraseNormalizer == null)
            voicePhraseNormalizer = GetComponent<VoicePhraseNormalizer>();

        if (voicePhraseNormalizer == null)
            voicePhraseNormalizer = FindFirstObjectByType<VoicePhraseNormalizer>();

        if (voicePhraseNormalizer == null && !hasLoggedMissingVoicePhraseNormalizer)
            LogMissingVoicePhraseNormalizer();

        return voicePhraseNormalizer;
    }

    private bool IsSpeechAliasForExpectedWord(string recognizedPhrase, string expectedWord)
    {
        IncantationWord currentWord = GetCurrentExpectedIncantationWord();

        if (currentWord != null && currentWord.HasSpeechAlias(recognizedPhrase))
            return true;

        IncantationWordLibrary wordLibrary = ResolveSpeechAliasWordLibrary(false);

        return wordLibrary != null && wordLibrary.IsSpeechAliasForWord(recognizedPhrase, expectedWord);
    }

    private IncantationWord GetCurrentExpectedIncantationWord()
    {
        if (incantationManager == null || incantationManager.IsCompleted)
            return null;

        int currentWordIndex = incantationManager.CurrentWordIndex;

        if (currentWordIndex < 0 || currentWordIndex >= incantationManager.CurrentIncantation.Count)
            return null;

        return incantationManager.CurrentIncantation[currentWordIndex];
    }

    private IncantationWordLibrary ResolveSpeechAliasWordLibrary(bool logMissingWarning)
    {
        if (speechAliasWordLibrary == null)
            speechAliasWordLibrary = GetComponent<IncantationWordLibrary>();

        if (speechAliasWordLibrary == null)
            speechAliasWordLibrary = FindFirstObjectByType<IncantationWordLibrary>();

        if (speechAliasWordLibrary == null && logMissingWarning)
            LogMissingSpeechAliasWordLibrary();

        return speechAliasWordLibrary;
    }

    private void AcceptPendingAlias()
    {
        if (!HasPendingAlias())
            return;

        string recognizedPhrase = pendingRecognizedPhrase;
        string expectedWord = pendingExpectedWord;

        IncantationWordLibrary wordLibrary = ResolveSpeechAliasWordLibrary(true);

        if (wordLibrary != null && !wordLibrary.IsSpeechAliasForWord(recognizedPhrase, expectedWord))
            wordLibrary.TryAddSpeechAlias(expectedWord, recognizedPhrase);

        AddLearnedAlias(expectedWord, recognizedPhrase);
        Debug.Log($"Accepted alias: {recognizedPhrase} -> {expectedWord}");
        ClearPendingAlias();
    }

    private void IgnorePendingAlias()
    {
        if (!HasPendingAlias())
            return;

        Debug.Log($"Ignored alias: {pendingRecognizedPhrase} -> {pendingExpectedWord}");
        ClearPendingAlias();
    }

    private void AddLearnedAlias(string expectedWord, string recognizedPhrase)
    {
        if (learnedAliases == null)
            learnedAliases = new List<LearnedSpeechAlias>();

        string normalizedExpectedWord = NormalizeSpeechText(expectedWord);
        string normalizedRecognizedPhrase = NormalizeSpeechText(recognizedPhrase);

        foreach (LearnedSpeechAlias learnedAlias in learnedAliases)
        {
            if (learnedAlias == null)
                continue;

            if (NormalizeSpeechText(learnedAlias.expectedWord) == normalizedExpectedWord &&
                NormalizeSpeechText(learnedAlias.recognizedPhrase) == normalizedRecognizedPhrase)
                return;
        }

        learnedAliases.Add(new LearnedSpeechAlias(expectedWord, recognizedPhrase));
    }

    [ContextMenu("Export Learned Aliases To Json")]
    public void ExportLearnedAliasesToJson()
    {
        if (learnedAliases == null)
            learnedAliases = new List<LearnedSpeechAlias>();

        LearnedSpeechAliasExport export = new LearnedSpeechAliasExport(learnedAliases);
        string json = JsonUtility.ToJson(export, true);
        string path = Path.Combine(Application.persistentDataPath, "learned-speech-aliases.json");

        File.WriteAllText(path, json);
        Debug.Log($"Exported learned aliases to JSON: {path}");
    }

    private bool HasPendingAlias()
    {
        return !string.IsNullOrWhiteSpace(pendingRecognizedPhrase) &&
            !string.IsNullOrWhiteSpace(pendingExpectedWord);
    }

    private void ClearPendingAlias()
    {
        pendingRecognizedPhrase = string.Empty;
        pendingExpectedWord = string.Empty;
    }

    private void LogSpeechLearningBlock(string recognizedPhrase, string expectedWord)
    {
        Debug.Log(
            "---------------------------\n" +
            "Speech Learning\n" +
            "---------------------------\n" +
            "Expected:\n" +
            $"{expectedWord}\n\n" +
            "Windows heard:\n" +
            $"{recognizedPhrase}\n\n" +
            "Suggested alias:\n" +
            $"{recognizedPhrase} -> {expectedWord}\n\n" +
            "Press A to accept alias\n" +
            "Press D to ignore\n" +
            "---------------------------");
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

    private string GetNormalizedCurrentIncantationText()
    {
        string currentIncantationText = GetCurrentIncantationText();

        if (voicePhraseNormalizer != null)
            return voicePhraseNormalizer.NormalizePhrase(currentIncantationText);

        return NormalizeSpeechText(currentIncantationText);
    }

    private bool TryGenerateCoreRitualLoopIncantation()
    {
        if (!EnsureCoreRitualLoopInitialized())
            return false;

        if (!coreRitualLoopBridge.TryPrepareLegacyIncantation(incantationManager))
        {
            isUsingCoreRitualLoopPhraseAuthority = false;
            return false;
        }

        isUsingCoreRitualLoopPhraseAuthority = true;
        return true;
    }

    private bool EnsureCoreRitualLoopInitialized()
    {
        if (!TryResolveReadyCoreRitualLoopBridge())
            return false;

        int occupiedSeatCount = GetOccupiedSeatCount();

        if (occupiedSeatCount <= 0)
        {
            LogWaitingForOccupiedSeat();
            return false;
        }

        if (hasInitializedCoreRitualLoop && configuredCoreRitualPlayerCount == occupiedSeatCount)
            return true;

        coreRitualLoopBridge.ConfigurePlayerCount(occupiedSeatCount);
        coreRitualLoopBridge.ResetGame();
        configuredCoreRitualPlayerCount = occupiedSeatCount;
        hasInitializedCoreRitualLoop = true;
        return true;
    }

    private bool TryAdvanceCoreRitualLoopAfterSuccessfulTurn()
    {
        if (!isUsingCoreRitualLoopPhraseAuthority || !TryResolveReadyCoreRitualLoopBridge())
            return false;

        return coreRitualLoopBridge.TryAdvanceSuccessfulTurn();
    }

    private int GetOccupiedSeatCount()
    {
        if (seatManager == null)
        {
            LogMissingSeatManager();
            return 0;
        }

        return seatManager.GetOccupiedSeats().Count;
    }

    private bool TryResolveReadyCoreRitualLoopBridge()
    {
        if (coreRitualLoopBridge == null)
            coreRitualLoopBridge = GetComponent<CoreRitualLoopBridge>();

        if (coreRitualLoopBridge == null)
            coreRitualLoopBridge = FindFirstObjectByType<CoreRitualLoopBridge>();

        if (coreRitualLoopBridge == null)
            coreRitualLoopBridge = CreateRuntimeBridgeOnCoreRitualLoop();

        if (coreRitualLoopBridge == null)
            return false;

        return coreRitualLoopBridge.IsReady();
    }

    private CoreRitualLoopBridge CreateRuntimeBridgeOnCoreRitualLoop()
    {
        CoreRitualLoop coreRitualLoop = FindFirstObjectByType<CoreRitualLoop>();

        if (coreRitualLoop == null)
            return null;

        CoreRitualLoopBridge resolvedBridge = coreRitualLoop.GetComponent<CoreRitualLoopBridge>();

        if (resolvedBridge != null)
            return resolvedBridge;

        resolvedBridge = coreRitualLoop.gameObject.AddComponent<CoreRitualLoopBridge>();
        LogRuntimeCoreRitualLoopBridgeSetup(coreRitualLoop);
        return resolvedBridge;
    }

    private void LogRuntimeCoreRitualLoopBridgeSetup(CoreRitualLoop coreRitualLoop)
    {
        if (hasLoggedRuntimeCoreRitualLoopBridgeSetup)
            return;

        string coreRitualLoopName = coreRitualLoop != null ? coreRitualLoop.gameObject.name : "CoreRitualLoop";

        Debug.LogWarning(
            $"{nameof(RitualController)} found {nameof(CoreRitualLoop)} on '{coreRitualLoopName}' but no {nameof(CoreRitualLoopBridge)} component was present. " +
            $"A runtime bridge was added so this Play Mode session can use the growing incantation system. " +
            $"For persistent scene setup, add {nameof(CoreRitualLoopBridge)} to the '{coreRitualLoopName}' GameObject and assign that component to {nameof(RitualController)}.{nameof(coreRitualLoopBridge)}.",
            this);

        hasLoggedRuntimeCoreRitualLoopBridgeSetup = true;
    }

    private void LogLegacyPhraseFallback()
    {
        if (hasLoggedLegacyPhraseFallback)
            return;

        Debug.Log($"{nameof(RitualController)} is using legacy {nameof(IncantationManager)} phrase generation because {nameof(CoreRitualLoopBridge)} is not assigned and ready.");
        hasLoggedLegacyPhraseFallback = true;
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log(message);
    }

    private string NormalizeSpeechText(string speechText)
    {
        if (string.IsNullOrWhiteSpace(speechText))
            return string.Empty;

        return speechText.Trim().ToLowerInvariant();
    }

    [System.Serializable]
    private class LearnedSpeechAlias
    {
        public string expectedWord;
        public string recognizedPhrase;

        public LearnedSpeechAlias(string expectedWord, string recognizedPhrase)
        {
            this.expectedWord = expectedWord;
            this.recognizedPhrase = recognizedPhrase;
        }
    }

    [System.Serializable]
    private class LearnedSpeechAliasExport
    {
        public List<LearnedSpeechAlias> learnedAliases;

        public LearnedSpeechAliasExport(List<LearnedSpeechAlias> learnedAliases)
        {
            this.learnedAliases = new List<LearnedSpeechAlias>(learnedAliases);
        }
    }
}
