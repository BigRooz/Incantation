using UnityEngine;

/// <summary>
/// Owns the local Ghost presentation that replaces the locally eliminated priest after the
/// existing absorption and Book Prison transition. It owns no ritual or network authority.
/// </summary>
public sealed class GhostLifecycleController : MonoBehaviour
{
    private GhostMovement ghostMovement;
    private GhostCameraController ghostCameraController;
    private GameObject hiddenPriest;
    private bool ghostModeActive;

    public bool GhostModeActive => ghostModeActive;

    private void Awake()
    {
        ResolveComponents();
        ResetGhostPresentation();
    }

    public bool BeginGhostPresentation(
        GameObject localPriest,
        Transform prisonSpawn,
        Camera deathCamera)
    {
        ResolveComponents();
        if (localPriest == null || prisonSpawn == null || deathCamera == null ||
            ghostMovement == null || ghostCameraController == null)
        {
            Debug.LogWarning(
                $"{nameof(GhostLifecycleController)} on '{name}' cannot begin Ghost mode because " +
                "the local priest, prison spawn, Death Camera, or Ghost components are missing.",
                this);
            return false;
        }

        ResetGhostPresentation();
        hiddenPriest = localPriest;
        gameObject.SetActive(true);
        ghostCameraController.BeginFollowing(transform, deathCamera);
        LocalInputContextGate.RestoreGameplay();
        ghostMovement.BeginGhostControl(
            prisonSpawn.position,
            prisonSpawn.rotation,
            ghostCameraController);
        hiddenPriest.SetActive(false);
        ghostModeActive = true;
        return true;
    }

    public void ResetGhostPresentation()
    {
        ghostModeActive = false;

        ResolveComponents();

        ghostMovement?.EndGhostControl();
        ghostCameraController?.StopFollowing();

        if (hiddenPriest != null)
            hiddenPriest.SetActive(true);

        hiddenPriest = null;
        gameObject.SetActive(false);
    }

    private void ResolveComponents()
    {
        if (ghostMovement == null)
            ghostMovement = GetComponent<GhostMovement>();
        if (ghostCameraController == null)
            ghostCameraController = GetComponent<GhostCameraController>();
    }
}
