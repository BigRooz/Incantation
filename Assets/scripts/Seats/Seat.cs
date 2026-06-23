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
    }

    public void Free()
    {
        isOccupied = false;
        currentPlayer = null;
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
}