using UnityEngine;

/// <summary>
/// Owns the Living Book's smooth local rotation between its front and back views.
/// Menu systems may request a view, but this component remains the sole authority
/// over the Book rotation and its Inspector-configured angles.
/// </summary>
public sealed class BookRotationController : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField, Min(0f)] private float rotationSpeed = 180f;
    [SerializeField] private float frontRotation = 90f;
    [SerializeField] private float backRotation = 270f;

    private float targetRotation;

    private void Awake()
    {
        targetRotation = frontRotation;
        SetLocalYRotation(frontRotation);
    }

    private void Update()
    {
        float currentRotation = transform.localEulerAngles.y;
        float nextRotation = Mathf.MoveTowardsAngle(
            currentRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);

        SetLocalYRotation(nextRotation);
    }

    public void RotateToFront()
    {
        targetRotation = frontRotation;
    }

    public void RotateToBack()
    {
        targetRotation = backRotation;
    }

    private void SetLocalYRotation(float yRotation)
    {
        Vector3 localEulerAngles = transform.localEulerAngles;
        localEulerAngles.y = yRotation;
        transform.localRotation = Quaternion.Euler(localEulerAngles);
    }
}
