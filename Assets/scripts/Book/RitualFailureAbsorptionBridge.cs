using UnityEngine;

/// <summary>
/// Bridges the demon hand grab moment to player absorption without moving
/// absorption animation logic into RitualController or DemonHandController.
/// </summary>
public class RitualFailureAbsorptionBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RitualController ritualController;
    [SerializeField] private PlayerAbsorptionController playerAbsorptionController;
    [SerializeField] private DeathVisionVignetteController deathVisionVignetteController;

    private Transform pendingFailedPlayer;
    private bool hasLoggedMissingRitualController;
    private bool hasLoggedMissingAbsorptionController;
    private bool hasLoggedMissingFailedPlayer;

    private void OnEnable()
    {
        SubscribeToVignette();
    }

    private void OnDisable()
    {
        UnsubscribeFromVignette();
        pendingFailedPlayer = null;
    }

    public void AbsorbCurrentFailedPlayer()
    {
        if (!HasRequiredReferences())
            return;

        Transform failedPlayer = ritualController.CurrentFailedPlayer;

        if (failedPlayer == null)
        {
            LogMissingFailedPlayerWarning();
            return;
        }

        if (deathVisionVignetteController == null)
        {
            playerAbsorptionController.BeginAbsorption(failedPlayer);
            return;
        }

        pendingFailedPlayer = failedPlayer;
        SubscribeToVignette();
        deathVisionVignetteController.Play();
    }

    private void BeginPendingAbsorption()
    {
        if (pendingFailedPlayer == null)
        {
            LogMissingFailedPlayerWarning();
            return;
        }

        playerAbsorptionController.BeginAbsorption(pendingFailedPlayer);
        pendingFailedPlayer = null;
    }

    private void SubscribeToVignette()
    {
        if (deathVisionVignetteController == null)
            return;

        deathVisionVignetteController.AbsorptionMomentReached -= BeginPendingAbsorption;
        deathVisionVignetteController.AbsorptionMomentReached += BeginPendingAbsorption;
    }

    private void UnsubscribeFromVignette()
    {
        if (deathVisionVignetteController == null)
            return;

        deathVisionVignetteController.AbsorptionMomentReached -= BeginPendingAbsorption;
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = true;

        if (ritualController == null)
        {
            LogMissingRitualControllerWarning();
            hasReferences = false;
        }

        if (playerAbsorptionController == null)
        {
            LogMissingAbsorptionControllerWarning();
            hasReferences = false;
        }

        return hasReferences;
    }

    private void LogMissingRitualControllerWarning()
    {
        if (hasLoggedMissingRitualController)
            return;

        Debug.LogWarning($"{nameof(RitualFailureAbsorptionBridge)} on '{gameObject.name}' needs a RitualController reference to resolve the failed player.", this);
        hasLoggedMissingRitualController = true;
    }

    private void LogMissingAbsorptionControllerWarning()
    {
        if (hasLoggedMissingAbsorptionController)
            return;

        Debug.LogWarning($"{nameof(RitualFailureAbsorptionBridge)} on '{gameObject.name}' needs a PlayerAbsorptionController reference before it can absorb a failed player.", this);
        hasLoggedMissingAbsorptionController = true;
    }

    private void LogMissingFailedPlayerWarning()
    {
        if (hasLoggedMissingFailedPlayer)
            return;

        Debug.LogWarning($"{nameof(RitualFailureAbsorptionBridge)} on '{gameObject.name}' could not absorb a failed player because RitualController.CurrentFailedPlayer is null.", this);
        hasLoggedMissingFailedPlayer = true;
    }
}
