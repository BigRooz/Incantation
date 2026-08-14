using UnityEngine;

/// <summary>
/// Local-only horizontal floating movement for the dead-player Ghost presentation.
/// It owns no ritual or network state.
/// </summary>
public sealed class GhostMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeed = 2.5f;
    [SerializeField, Min(0f)] private float lookSensitivity = 2f;
    [SerializeField] private float hoverHeight;
    [SerializeField] private float spawnYawOffset = 180f;

    [Header("Confinement")]
    [SerializeField] private BoxCollider movementBounds;

    private GhostCameraController ghostCameraController;
    private bool ghostModeActive;
    private float fixedWorldHeight;

    public bool GhostModeActive => ghostModeActive;

    private void Update()
    {
        if (!CanProcessInput())
            return;

        float yawInput = Input.GetAxis("Mouse X") * lookSensitivity;
        transform.Rotate(0f, yawInput, 0f, Space.World);

        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 movement = (forward * input.y + right * input.x) *
            movementSpeed * Time.deltaTime;

        Vector3 targetPosition = transform.position + movement;
        targetPosition.y = fixedWorldHeight;
        transform.position = ClampToBounds(targetPosition);
    }

    public void BeginGhostControl(
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        GhostCameraController cameraController)
    {
        ghostCameraController = cameraController;
        fixedWorldHeight = spawnPosition.y + hoverHeight;
        transform.SetPositionAndRotation(
            ClampToBounds(new Vector3(
                spawnPosition.x,
                fixedWorldHeight,
                spawnPosition.z)),
            spawnRotation * Quaternion.Euler(0f, spawnYawOffset, 0f));
        ghostModeActive = true;
        enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void EndGhostControl()
    {
        ghostModeActive = false;
        ghostCameraController = null;
        enabled = false;
    }

    private bool CanProcessInput()
    {
        return ghostModeActive &&
            ghostCameraController != null &&
            ghostCameraController.IsFollowingActiveCamera &&
            LocalInputContextGate.AllowsGameplayInput;
    }

    private Vector3 ClampToBounds(Vector3 position)
    {
        if (movementBounds == null)
            return position;

        Bounds bounds = movementBounds.bounds;
        position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
        position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
        position.y = fixedWorldHeight;
        return position;
    }
}
