using UnityEngine;

/// <summary>
/// Local-only first-person behavior for an existing Book Prison camera.
/// It never creates a Camera or changes AudioListener state.
/// </summary>
public sealed class GhostCameraController : MonoBehaviour
{
    [Header("First Person")]
    [SerializeField] private Vector3 firstPersonOffset = new Vector3(0f, 0.805f, 0.1f);

    [Header("Look")]
    [SerializeField, Min(0f)] private float lookSensitivity = 2f;
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 55f;

    private Transform followTarget;
    private Camera activeCamera;
    private float pitch;
    private bool ghostModeActive;

    public bool IsFollowingActiveCamera => ghostModeActive &&
        followTarget != null &&
        activeCamera != null &&
        activeCamera.enabled &&
        activeCamera.gameObject.activeInHierarchy;

    private void Update()
    {
        if (!IsFollowingActiveCamera || !LocalInputContextGate.AllowsGameplayInput)
            return;

        pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (!IsFollowingActiveCamera)
            return;

        Quaternion yawRotation = Quaternion.Euler(0f, followTarget.eulerAngles.y, 0f);
        Vector3 firstPersonPosition = followTarget.position + yawRotation * firstPersonOffset;
        Quaternion firstPersonRotation = Quaternion.Euler(
            pitch,
            followTarget.eulerAngles.y,
            0f);
        activeCamera.transform.SetPositionAndRotation(firstPersonPosition, firstPersonRotation);
    }

    public void BeginFollowing(Transform target, Camera camera)
    {
        followTarget = target;
        activeCamera = camera;
        pitch = 0f;
        ghostModeActive = followTarget != null && activeCamera != null;
        enabled = ghostModeActive;
    }

    public void StopFollowing()
    {
        ghostModeActive = false;
        followTarget = null;
        activeCamera = null;
        pitch = 0f;
        enabled = false;
    }
}
