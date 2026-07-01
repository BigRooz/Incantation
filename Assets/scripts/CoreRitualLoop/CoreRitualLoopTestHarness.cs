using UnityEngine;

/// <summary>
/// Development-only console harness for validating the logic-only CoreRitualLoop integration.
/// Depends only on CoreRitualLoop, which owns coordination with TurnManager, GrowingIncantationManager,
/// and PhraseValidator. This harness has no gameplay, UI, voice, networking, book movement, or timer behavior.
/// TODO: Remove or move to a dedicated test scene once the core ritual loop is validated in Unity.
/// </summary>
public class CoreRitualLoopTestHarness : MonoBehaviour
{
    [Header("Test Target")]
    [Tooltip("Logic-only ritual loop to exercise through console output.")]
    [SerializeField] private CoreRitualLoop coreRitualLoop;

    [Header("Test Settings")]
    [Tooltip("Number of players used by the rotation growth proof.")]
    [SerializeField, Min(1)] private int testPlayerCount = 4;

    [Tooltip("Number of successful turns to advance when running the full context menu test.")]
    [SerializeField, Min(1)] private int successfulTurnsToRun = 4;

    /// <summary>
    /// Resets the core ritual loop and logs the current player, current phrase,
    /// and validator result for the visible phrase.
    /// </summary>
    [ContextMenu("Core Ritual Loop Test/Reset Game")]
    public void ResetGame()
    {
        if (!HasCoreRitualLoop())
        {
            return;
        }

        coreRitualLoop.ConfigurePlayerCount(testPlayerCount);
        coreRitualLoop.ResetGame();
        LogCurrentState("ResetGame");
    }

    /// <summary>
    /// Advances one successful turn through CoreRitualLoop and logs the resulting player,
    /// phrase, and validator result for the visible phrase.
    /// </summary>
    [ContextMenu("Core Ritual Loop Test/Advance Successful Turn")]
    public void AdvanceSuccessfulTurn()
    {
        if (!HasCoreRitualLoop())
        {
            return;
        }

        coreRitualLoop.AdvanceSuccessfulTurn();
        LogCurrentState("AdvanceSuccessfulTurn");
    }

    /// <summary>
    /// Runs a console-only proof that player rotation advances and the shared phrase grows
    /// by exactly one word only after a full table rotation.
    /// </summary>
    [ContextMenu("Core Ritual Loop Test/Run Rotation Growth Test")]
    public void RunRotationGrowthTest()
    {
        if (!HasCoreRitualLoop())
        {
            return;
        }

        Debug.Log($"{nameof(CoreRitualLoopTestHarness)} starting rotation growth test with {testPlayerCount} players.", this);

        ResetGame();

        for (int turnIndex = 0; turnIndex < successfulTurnsToRun; turnIndex++)
        {
            AdvanceSuccessfulTurn();
        }

        Debug.Log($"{nameof(CoreRitualLoopTestHarness)} finished rotation growth test.", this);
    }

    private bool HasCoreRitualLoop()
    {
        if (coreRitualLoop != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(CoreRitualLoopTestHarness)} requires a {nameof(CoreRitualLoop)} reference.", this);
        return false;
    }

    private void LogCurrentState(string actionName)
    {
        int currentPlayerIndex = coreRitualLoop.GetCurrentPlayerIndex();
        string currentPhrase = coreRitualLoop.GetCurrentPhrase();
        bool currentPhraseValidated = coreRitualLoop.ValidateCurrentPhrase(currentPhrase);

        Debug.Log(
            $"{nameof(CoreRitualLoopTestHarness)}::{actionName}\n" +
            $"Current player: Player {currentPlayerIndex}\n" +
            $"Current phrase: {currentPhrase}\n" +
            $"PhraseValidator accepts visible phrase: {currentPhraseValidated}\n" +
            "--------------------------------",
            this);
    }

    private void OnValidate()
    {
        testPlayerCount = Mathf.Max(1, testPlayerCount);
        successfulTurnsToRun = Mathf.Max(1, successfulTurnsToRun);
    }
}
