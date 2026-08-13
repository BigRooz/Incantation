using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Bones")]
    public Transform head;
    public Transform neck;
    public Transform spine02;
    public Transform spine01;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;
    public float maxLookUpDown = 60f;
    public float maxLookLeftRight = 45f;

    [Header("Body Weights")]
    public float headWeight = 0.70f;
    public float neckWeight = 0.20f;
    public float spine02Weight = 0.07f;
    public float spine01Weight = 0.03f;

    [Header("Smooth")]
    public float smoothSpeed = 18f;

    private float targetX = 0f;
    private float targetY = 0f;

    private Quaternion headStart;
    private Quaternion neckStart;
    private Quaternion spine02Start;
    private Quaternion spine01Start;
    private bool lookPoseInitialized;

    public float CurrentLookPitch => targetX;
    public float CurrentLookYaw => targetY;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        EnsureLookPoseInitialized();
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        targetY += mouseX;
        targetY = Mathf.Clamp(targetY, -maxLookLeftRight, maxLookLeftRight);

        targetX += mouseY;
        targetX = Mathf.Clamp(targetX, -maxLookUpDown, maxLookUpDown);
    }

    void LateUpdate()
    {
        ApplyLookPose(targetX, targetY, Time.deltaTime);
    }

    public void EnsureLookPoseInitialized()
    {
        if (lookPoseInitialized)
            return;

        if (head != null) headStart = head.localRotation;
        if (neck != null) neckStart = neck.localRotation;
        if (spine02 != null) spine02Start = spine02.localRotation;
        if (spine01 != null) spine01Start = spine01.localRotation;
        lookPoseInitialized = true;
    }

    public void ApplyLookPose(float pitch, float yaw, float deltaTime)
    {
        if (!IsFinite(pitch) || !IsFinite(yaw) || !IsFinite(deltaTime))
            return;

        EnsureLookPoseInitialized();
        float safePitch = Mathf.Clamp(pitch, -maxLookUpDown, maxLookUpDown);
        float safeYaw = Mathf.Clamp(yaw, -maxLookLeftRight, maxLookLeftRight);

        ApplyBone(head, headStart, safePitch * headWeight, safeYaw * headWeight, deltaTime);
        ApplyBone(neck, neckStart, safePitch * neckWeight, safeYaw * neckWeight, deltaTime);
        ApplyBone(spine02, spine02Start, safePitch * spine02Weight, safeYaw * spine02Weight, deltaTime);
        ApplyBone(spine01, spine01Start, safePitch * spine01Weight, safeYaw * spine01Weight, deltaTime);
    }

    public void ResetLookPose(bool immediate)
    {
        EnsureLookPoseInitialized();
        targetX = 0f;
        targetY = 0f;

        if (!immediate)
        {
            ApplyLookPose(0f, 0f, Time.deltaTime);
            return;
        }

        RestoreBoneRotation(head, headStart);
        RestoreBoneRotation(neck, neckStart);
        RestoreBoneRotation(spine02, spine02Start);
        RestoreBoneRotation(spine01, spine01Start);
    }

    private void ApplyBone(
        Transform bone,
        Quaternion startRotation,
        float x,
        float y,
        float deltaTime)
    {
        if (bone == null) return;

        Quaternion targetRotation = startRotation * Quaternion.Euler(x, y, 0f);

        bone.localRotation = Quaternion.Slerp(
            bone.localRotation,
            targetRotation,
            smoothSpeed * deltaTime
        );
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void RestoreBoneRotation(Transform bone, Quaternion baseRotation)
    {
        if (bone != null)
            bone.localRotation = baseRotation;
    }
}
