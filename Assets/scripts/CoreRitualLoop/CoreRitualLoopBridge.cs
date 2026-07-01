using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Provides the narrow adapter surface between legacy ritual gameplay and CoreRitualLoop.
/// Depends on CoreRitualLoop for phrase authority and can mirror that phrase into the legacy
/// IncantationManager display model while migration is in progress.
/// Does not own gameplay, validation, phrase growth, timer, networking, or voice-recognition rules.
/// TODO: Remove the legacy IncantationManager mirror after UI reads directly from CoreRitualLoop.
/// </summary>
public class CoreRitualLoopBridge : MonoBehaviour
{
    private static readonly FieldInfo CurrentIncantationField = typeof(IncantationManager).GetField(
        "currentIncantation",
        BindingFlags.Instance | BindingFlags.NonPublic);

    [Tooltip("Core ritual engine that owns ritual loop coordination.")]
    [SerializeField] private CoreRitualLoop coreRitualLoop;

    /// <summary>
    /// Reports whether this bridge has the required CoreRitualLoop reference for migrated callers.
    /// This check is intentionally quiet so legacy fallback can probe for bridge readiness without
    /// producing warnings or errors during normal migration.
    /// </summary>
    /// <returns>True when the bridge can forward calls to CoreRitualLoop.</returns>
    public bool IsReady()
    {
        return coreRitualLoop != null;
    }

    /// <summary>
    /// Forwards active player count configuration to CoreRitualLoop.
    /// </summary>
    /// <param name="playerCount">The number of active ritual participants.</param>
    public void ConfigurePlayerCount(int playerCount)
    {
        if (!HasCoreRitualLoop())
        {
            return;
        }

        coreRitualLoop.ConfigurePlayerCount(playerCount);
    }

    /// <summary>
    /// Forwards ritual state reset to CoreRitualLoop.
    /// </summary>
    public void ResetGame()
    {
        if (!HasCoreRitualLoop())
        {
            return;
        }

        coreRitualLoop.ResetGame();
    }

    /// <summary>
    /// Forwards current player index lookup to CoreRitualLoop.
    /// </summary>
    /// <returns>The current active player index reported by CoreRitualLoop.</returns>
    public int GetCurrentPlayerIndex()
    {
        if (!HasCoreRitualLoop())
        {
            return 0;
        }

        return coreRitualLoop.GetCurrentPlayerIndex();
    }

    /// <summary>
    /// Forwards current phrase lookup to CoreRitualLoop.
    /// </summary>
    /// <returns>The current shared ritual phrase reported by CoreRitualLoop.</returns>
    public string GetCurrentPhrase()
    {
        if (!HasCoreRitualLoop())
        {
            return string.Empty;
        }

        return coreRitualLoop.GetCurrentPhrase();
    }

    /// <summary>
    /// Mirrors the CoreRitualLoop phrase into the legacy IncantationManager so existing display
    /// and completion events continue to work during migration.
    /// Phrase authority remains CoreRitualLoop through GrowingIncantationManager.
    /// </summary>
    /// <param name="incantationManager">The legacy incantation model used by current UI and recognizer flow.</param>
    /// <returns>True when a non-empty core phrase was mirrored successfully.</returns>
    public bool TryPrepareLegacyIncantation(IncantationManager incantationManager)
    {
        if (incantationManager == null)
        {
            Debug.LogError($"{nameof(CoreRitualLoopBridge)} requires an {nameof(IncantationManager)} reference to mirror the current ritual phrase.", this);
            return false;
        }

        string currentPhrase = GetCurrentPhrase();

        if (string.IsNullOrWhiteSpace(currentPhrase))
        {
            Debug.LogError($"{nameof(CoreRitualLoopBridge)} cannot mirror an empty ritual phrase from {nameof(CoreRitualLoop)}.", this);
            return false;
        }

        List<IncantationWord> currentIncantation = GetLegacyCurrentIncantation(incantationManager);

        if (currentIncantation == null)
        {
            Debug.LogError($"{nameof(CoreRitualLoopBridge)} could not access the legacy incantation list for phrase mirroring.", incantationManager);
            return false;
        }

        incantationManager.ResetIncantation();

        foreach (string phraseWord in SplitPhraseIntoWords(currentPhrase))
        {
            currentIncantation.Add(new IncantationWord(phraseWord));
        }

        incantationManager.OnIncantationGenerated.Invoke();
        return currentIncantation.Count > 0;
    }

    /// <summary>
    /// Forwards recognized phrase validation to CoreRitualLoop.
    /// </summary>
    /// <param name="recognizedPhrase">The recognized phrase candidate.</param>
    /// <returns>True when CoreRitualLoop accepts the phrase candidate.</returns>
    public bool ValidatePhrase(string recognizedPhrase)
    {
        if (!HasCoreRitualLoop())
        {
            return false;
        }

        return coreRitualLoop.ValidateCurrentPhrase(recognizedPhrase);
    }

    /// <summary>
    /// Forwards successful turn advancement to CoreRitualLoop.
    /// </summary>
    public void AdvanceSuccessfulTurn()
    {
        if (!HasCoreRitualLoop())
        {
            return;
        }

        coreRitualLoop.AdvanceSuccessfulTurn();
    }

    /// <summary>
    /// Forwards failed turn advancement to CoreRitualLoop.
    /// </summary>
    public void AdvanceFailedTurn()
    {
        if (!HasCoreRitualLoop())
        {
            return;
        }

        coreRitualLoop.AdvanceFailedTurn();
    }

    private List<IncantationWord> GetLegacyCurrentIncantation(IncantationManager incantationManager)
    {
        if (CurrentIncantationField == null)
        {
            return null;
        }

        return CurrentIncantationField.GetValue(incantationManager) as List<IncantationWord>;
    }

    private List<string> SplitPhraseIntoWords(string phrase)
    {
        List<string> phraseWords = new List<string>();
        string[] rawWords = phrase.Split(
            new[] { ' ', '\t', '\r', '\n' },
            System.StringSplitOptions.RemoveEmptyEntries);

        foreach (string rawWord in rawWords)
        {
            string trimmedWord = rawWord.Trim();

            if (!string.IsNullOrWhiteSpace(trimmedWord))
            {
                phraseWords.Add(trimmedWord);
            }
        }

        return phraseWords;
    }

    private bool HasCoreRitualLoop()
    {
        if (coreRitualLoop != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(CoreRitualLoopBridge)} requires a {nameof(CoreRitualLoop)} reference.", this);
        return false;
    }
}
