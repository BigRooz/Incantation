using Incantation.Networking;
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
            BeginAbsorption(failedPlayer);
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

        BeginAbsorption(pendingFailedPlayer);
        pendingFailedPlayer = null;
    }

    private void BeginAbsorption(Transform failedPlayer)
    {
        bool preserveLocalCamera = IsRemoteNetworkElimination(out string ownershipReason);
        Debug.Log(
            "[DeathCamera] Camera Mutation Request\n" +
            $"EliminatedPlayerId = {ritualController.CurrentFailedPlayerId}\n" +
            $"LocalPlayerId = {(NetworkPlayer.LocalPlayer != null ? NetworkPlayer.LocalPlayer.PlayerId : "none")}\n" +
            $"LocalOwner = {!preserveLocalCamera}\n" +
            "Operation = Move / Rotate absorbed character root\n" +
            $"Result = {(preserveLocalCamera ? "Active local cameras preserved" : "Accepted")}\n" +
            $"Source = {nameof(RitualFailureAbsorptionBridge)}\n" +
            $"Reason = {ownershipReason}",
            this);
        playerAbsorptionController.BeginAbsorption(failedPlayer, preserveLocalCamera);
    }

    private bool IsRemoteNetworkElimination(out string reason)
    {
        NetworkRitualAuthority authority = NetworkRitualAuthority.Instance;
        if (authority == null || !authority.IsNetworkSessionActive)
        {
            reason = "Offline ritual preserves existing camera behavior.";
            return false;
        }

        NetworkPlayer localPlayer = NetworkPlayer.LocalPlayer;
        if (localPlayer == null)
        {
            reason = "No locally owned NetworkPlayer is available; camera mutation is rejected safely.";
            return true;
        }

        bool isLocalOwner = string.Equals(
            ritualController.CurrentFailedPlayerId,
            localPlayer.PlayerId,
            System.StringComparison.Ordinal);
        reason = isLocalOwner
            ? "The authoritative eliminated PlayerId is locally owned."
            : "The authoritative eliminated PlayerId is remotely owned.";
        return !isLocalOwner;
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
