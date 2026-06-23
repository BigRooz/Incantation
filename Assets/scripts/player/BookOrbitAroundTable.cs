using System.Collections;
using UnityEngine;

public class BookOrbitAroundTable : MonoBehaviour
{
    [Header("Références")]
    public Transform tableCenter;
    public Transform[] playerSeats;

    [Header("Mouvement")]
    public float orbitRadius = 1.2f;
    public float moveDuration = 1.5f;
    public float heightOffset = 0.05f;

    [Header("Rotation du livre")]
    public bool faceTableCenter = true;
    public float rotationSmoothness = 10f;

    [Header("Sens")]
    public bool moveLeft = true;

    private bool isMoving = false;
    private int currentSeatIndex = 0;

    private void Start()
    {
        if (tableCenter == null)
        {
            Debug.LogError("BookOrbitAroundTable : Table Center n'est pas assigné.");
            return;
        }

        if (playerSeats == null || playerSeats.Length == 0)
        {
            Debug.LogError("BookOrbitAroundTable : Aucun siège assigné.");
            return;
        }

        SnapToSeat(currentSeatIndex);
    }

    private void Update()
    {
        // TEST TEMPORAIRE
        // Appuie sur Espace pour envoyer le livre au joueur suivant
        if (Input.GetKeyDown(KeyCode.Space))
        {
            MoveToNextSeat();
        }
    }

    public void MoveToNextSeat()
    {
        if (isMoving)
            return;

        if (moveLeft)
            currentSeatIndex++;
        else
            currentSeatIndex--;

        if (currentSeatIndex >= playerSeats.Length)
            currentSeatIndex = 0;

        if (currentSeatIndex < 0)
            currentSeatIndex = playerSeats.Length - 1;

        MoveToSeat(currentSeatIndex);
    }

    public void MoveToSeat(int seatIndex)
    {
        if (isMoving)
            return;

        if (seatIndex < 0 || seatIndex >= playerSeats.Length)
        {
            Debug.LogWarning("BookOrbitAroundTable : index de siège invalide.");
            return;
        }

        currentSeatIndex = seatIndex;
        StartCoroutine(MoveBookToSeat(seatIndex));
    }

    private IEnumerator MoveBookToSeat(int seatIndex)
    {
        isMoving = true;

        Vector3 startPos = transform.position;
        Vector3 targetPos = GetPositionNearSeat(playerSeats[seatIndex]);

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / moveDuration;
            t = SmoothStep(t);

            transform.position = GetCircularPosition(startPos, targetPos, t);

            if (faceTableCenter)
                RotateTowardTableCenter();

            yield return null;
        }

        transform.position = targetPos;

        if (faceTableCenter)
            RotateTowardTableCenter();

        isMoving = false;
    }

    private Vector3 GetPositionNearSeat(Transform seat)
    {
        Vector3 direction = (seat.position - tableCenter.position).normalized;

        Vector3 pos = tableCenter.position + direction * orbitRadius;
        pos.y = tableCenter.position.y + heightOffset;

        return pos;
    }

    private Vector3 GetCircularPosition(Vector3 startPos, Vector3 endPos, float t)
    {
        Vector3 startDir = (startPos - tableCenter.position).normalized;
        Vector3 endDir = (endPos - tableCenter.position).normalized;

        float startAngle = Mathf.Atan2(startDir.z, startDir.x) * Mathf.Rad2Deg;
        float endAngle = Mathf.Atan2(endDir.z, endDir.x) * Mathf.Rad2Deg;

        float angle;

        if (moveLeft)
        {
            if (endAngle < startAngle)
                endAngle += 360f;

            angle = Mathf.Lerp(startAngle, endAngle, t);
        }
        else
        {
            if (endAngle > startAngle)
                endAngle -= 360f;

            angle = Mathf.Lerp(startAngle, endAngle, t);
        }

        float rad = angle * Mathf.Deg2Rad;

        Vector3 pos = tableCenter.position;
        pos.x += Mathf.Cos(rad) * orbitRadius;
        pos.z += Mathf.Sin(rad) * orbitRadius;
        pos.y = tableCenter.position.y + heightOffset;

        return pos;
    }

    private void RotateTowardTableCenter()
    {
        Vector3 direction = tableCenter.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSmoothness
        );
    }

    private void SnapToSeat(int seatIndex)
    {
        transform.position = GetPositionNearSeat(playerSeats[seatIndex]);

        if (faceTableCenter)
            RotateTowardTableCenter();
    }

    private float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
}