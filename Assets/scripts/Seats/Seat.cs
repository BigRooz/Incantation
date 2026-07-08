using UnityEngine;

public class Seat : MonoBehaviour
{
    [Header("Références du siège")]
    public Transform playerSpawn;
    public Transform bookTarget;
    public Transform bookGhost;
    public Transform lookTarget;
    public Transform leftHand;
    public Transform rightHand;
    public Collider chairClickZone;
    [Tooltip("Runtime-bound visible player root/model to absorb when this seat's active player fails. Seat.Occupy assigns this automatically for real player objects.")]
    public Transform realPlayerTransform;

    [Header("État")]
    public bool isOccupied;
    public GameObject currentPlayer;

    public bool IsFree()
    {
        return !isOccupied && currentPlayer == null;
    }

    public void Occupy(GameObject player)
    {
        isOccupied = true;
        currentPlayer = player;
        realPlayerTransform = ResolveRealPlayerTransform(player);
    }

    public void Free()
    {
        isOccupied = false;
        currentPlayer = null;
        realPlayerTransform = null;
    }

    private void OnValidate()
    {
        if (playerSpawn == null)
            playerSpawn = transform.Find("PlayerSpawn");

        if (bookTarget == null)
            bookTarget = transform.Find("BookTarget");

        if (bookGhost == null && bookTarget != null)
            bookGhost = bookTarget.Find("BookGhost");

        if (lookTarget == null)
            lookTarget = transform.Find("LookTarget");

        if (leftHand == null)
            leftHand = transform.Find("LeftHand");

        if (rightHand == null)
            rightHand = transform.Find("RightHand");

        if (chairClickZone == null)
        {
            Transform clickZone = transform.Find("ChairClickZone");

            if (clickZone != null)
                chairClickZone = clickZone.GetComponent<Collider>();
        }
    }

    public Transform GetBookDestination()
    {
        if (bookGhost != null)
            return bookGhost;

        return bookTarget;
    }

    public Transform GetRealPlayerTransform()
    {
        if (realPlayerTransform != null && IsAbsorbablePlayerTransform(realPlayerTransform) && HasVisibleRenderer(realPlayerTransform))
            return realPlayerTransform;

        if (currentPlayer == null || IsDebugOnlyOccupant(currentPlayer))
            return null;

        Transform currentPlayerTransform = currentPlayer.transform;

        if (!IsAbsorbablePlayerTransform(currentPlayerTransform))
            return null;

        return currentPlayerTransform;
    }

    private bool IsDebugOnlyOccupant(GameObject player)
    {
        if (player == null)
            return false;

        return player.name.StartsWith("DebugOccupant_") || player.name.StartsWith("SimulatedPlayer_");
    }

    private Transform ResolveRealPlayerTransform(GameObject player)
    {
        if (player == null || IsDebugOnlyOccupant(player))
            return null;

        Transform playerTransform = player.transform;

        if (!IsAbsorbablePlayerTransform(playerTransform) || !HasVisibleRenderer(playerTransform))
            return null;

        return playerTransform;
    }

    private bool IsAbsorbablePlayerTransform(Transform playerTransform)
    {
        return playerTransform != null &&
            playerTransform != bookGhost &&
            playerTransform != bookTarget;
    }

    private bool HasVisibleRenderer(Transform playerTransform)
    {
        Renderer[] renderers = playerTransform.GetComponentsInChildren<Renderer>(true);
        return renderers.Length > 0;
    }
}
