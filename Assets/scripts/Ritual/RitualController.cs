using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class RitualController : MonoBehaviour
{
    private const float HourglassDuration = 10f;
    private static RitualController activeRitualController;

    [Header("References")]
    [SerializeField] private SeatManager seatManager;
    [SerializeField] private BookMover bookMover;
    [SerializeField] private HourglassController hourglassController;
    [SerializeField] private IncantationManager incantationManager;
    [SerializeField] private MonoBehaviour voiceRecognizerBehaviour;
    [SerializeField] private VoicePhraseNormalizer voicePhraseNormalizer;
    [SerializeField] private IncantationWordLibrary speechAliasWordLibrary;

    [Header("Prototype")]
    [SerializeField] private bool autoStart = true;

    [Header("Speech Alias Learning")]
    [SerializeField] private bool enableLearningMode = false;
    [SerializeField] private string pendingRecognizedPhrase = string.Empty;
    [SerializeField] private string pendingExpectedWord = string.Empty;
    [SerializeField] private List<LearnedSpeechAlias> learnedAliases = new List<LearnedSpeechAlias>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Coroutine ritualRoutine;
    private Coroutine turnRoutine;
    private IVoiceRecognizer voiceRecognizer;
    private bool hourglassFinished;
    private bool isWaitingForOccupiedSeat;
    private bool isTurnActive;
    private bool playerTurnComplete;
    private bool hasLoggedMissingHourglass;
    private bool hasLoggedMissingIncantationManager;
    private Seat lastCompletedSeat;

    public Seat CurrentActiveSeat { get; private set; }

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
        hasLoggedMissingHourglass = false;
        hasLoggedMissingIncantationManager = false;
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

        LogDebug($"Book arrived at: {CurrentActiveSeat.name}");

        if (!IsSeatOccupied(CurrentActiveSeat))
        {
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
            StopListening();
            Debug.Log("Incantation failed.");
            CompletePlayerTurn();
        }
    }

    private bool MoveBookToCurrentSeat()
    {
        if (bookMover == null || CurrentActiveSeat == null)
            return false;

        LogDebug($"Moving book to: {CurrentActiveSeat.name}");
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
            LogMissingIncantationManager();
            return false;
        }

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

        return hasRequiredReferences;
    }

    private void LogMissingHourglass()
    {
        if (hasLoggedMissingHourglass)
            return;

        Debug.LogWarning("RitualController requires an HourglassController reference.");
        hasLoggedMissingHourglass = true;
    }

    private void LogMissingIncantationManager()
    {
        if (hasLoggedMissingIncantationManager)
            return;

        Debug.LogWarning("RitualController requires an IncantationManager reference.");
        hasLoggedMissingIncantationManager = true;
    }

    private IEnumerator RunPlayerTurn()
    {
        LogDebug("Starting hourglass");
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

        LogDebug("Waiting for occupied seat");
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
        LogDebug("Player turn complete");
    }

    private void StartListening()
    {
        if (!ResolveVoiceRecognizer())
            return;

        if (voiceRecognizer.IsListening)
            return;

        voiceRecognizer.StartListening();
        LogDebug("Listening started");
    }

    private void StopListening()
    {
        if (!ResolveVoiceRecognizer())
            return;

        if (!voiceRecognizer.IsListening)
            return;

        voiceRecognizer.StopListening();
        LogDebug("Listening stopped");
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

        string expectedWord = incantationManager.CurrentWord;
        string normalizedPhrase = voicePhraseNormalizer == null
            ? phrase
            : voicePhraseNormalizer.NormalizePhrase(phrase);
        bool completedCurrentWord = incantationManager.TryCompleteCurrentWord(normalizedPhrase);

        if (!completedCurrentWord)
        {
            Debug.Log($"Incorrect word: {phrase}");
            HandleSpeechAliasSuggestion(phrase, expectedWord);
            return;
        }

        Debug.Log($"Correct word: {phrase}");

        if (!incantationManager.IsCompleted)
            return;

        LogDebug("Incantation complete");

        if (hourglassController != null)
            hourglassController.StopHourglass();

        CompletePlayerTurn();
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
            Debug.LogWarning("RitualController requires an IncantationWordLibrary reference for speech alias learning.");

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
