using UnityEngine;

public class BodyMotion : MonoBehaviour
{
    [Header("Bones")]
    public Transform head;
    public Transform neck;
    public Transform spine02;
    public Transform spine01;

    [Header("Weights")]
    public float neckWeight = 0.35f;
    public float spine02Weight = 0.18f;
    public float spine01Weight = 0.08f;

    [Header("Smooth")]
    public float smoothSpeed = 10f;

    private Quaternion headStart;
    private Quaternion neckStart;
    private Quaternion spine02Start;
    private Quaternion spine01Start;

    void Start()
    {
        if (head != null) headStart = head.localRotation;
        if (neck != null) neckStart = neck.localRotation;
        if (spine02 != null) spine02Start = spine02.localRotation;
        if (spine01 != null) spine01Start = spine01.localRotation;
    }

    void LateUpdate()
    {
        if (head == null) return;

        Vector3 headEuler = head.localEulerAngles;

        float x = NormalizeAngle(headEuler.x);
        float y = NormalizeAngle(headEuler.y);

        ApplyBone(neck, neckStart, x * neckWeight, y * neckWeight);
        ApplyBone(spine02, spine02Start, x * spine02Weight, y * spine02Weight);
        ApplyBone(spine01, spine01Start, x * spine01Weight, y * spine01Weight);
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

    float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }
}