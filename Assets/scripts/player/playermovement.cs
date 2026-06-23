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

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (head != null) headStart = head.localRotation;
        if (neck != null) neckStart = neck.localRotation;
        if (spine02 != null) spine02Start = spine02.localRotation;
        if (spine01 != null) spine01Start = spine01.localRotation;
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
        ApplyBone(head, headStart, targetX * headWeight, targetY * headWeight);
        ApplyBone(neck, neckStart, targetX * neckWeight, targetY * neckWeight);
        ApplyBone(spine02, spine02Start, targetX * spine02Weight, targetY * spine02Weight);
        ApplyBone(spine01, spine01Start, targetX * spine01Weight, targetY * spine01Weight);
    }

    void ApplyBone(Transform bone, Quaternion startRotation, float x, float y)
    {
        if (bone == null) return;

        Quaternion targetRotation = startRotation * Quaternion.Euler(x, y, 0f);

        bone.localRotation = Quaternion.Slerp(
            bone.localRotation,
            targetRotation,
            smoothSpeed * Time.deltaTime
        );
    }
}