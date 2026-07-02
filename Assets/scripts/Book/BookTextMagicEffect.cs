using TMPro;
using UnityEngine;

/// <summary>
/// Adds a lightweight magical pulse and subtle local motion to TextMeshPro book text.
/// </summary>
public class BookTextMagicEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text targetText;

    [Header("Glow")]
    [SerializeField] private bool enableGlow = true;
    [SerializeField] private Color glowColor = new Color(0.85f, 0.35f, 1f);
    [Min(0f)]
    [SerializeField] private float glowIntensity = 0.35f;
    [Min(0f)]
    [SerializeField] private float glowPulseSpeed = 1.5f;

    [Header("Wobble")]
    [SerializeField] private bool enableWobble = true;
    [Min(0f)]
    [SerializeField] private float wobbleAmount = 0.01f;
    [Min(0f)]
    [SerializeField] private float wobbleSpeed = 2f;

    [Header("Floating")]
    [SerializeField] private bool enableFloating = true;
    [Min(0f)]
    [SerializeField] private float floatAmount = 0.015f;
    [Min(0f)]
    [SerializeField] private float floatSpeed = 1f;

    private Color baseTextColor;
    private Vector3 baseLocalPosition;
    private bool hasBaseState;

    private void Reset()
    {
        targetText = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        FindTargetTextIfNeeded();
    }

    private void OnEnable()
    {
        FindTargetTextIfNeeded();
        CaptureBaseState();
    }

    private void OnDisable()
    {
        RestoreBaseState();
    }

    private void OnValidate()
    {
        FindTargetTextIfNeeded();
    }

    private void Update()
    {
        if (targetText == null)
            return;

        ApplyGlow();
        ApplyMotion();
    }

    private void FindTargetTextIfNeeded()
    {
        if (targetText != null)
            return;

        targetText = GetComponent<TMP_Text>();
    }

    private void CaptureBaseState()
    {
        baseLocalPosition = transform.localPosition;

        if (targetText != null)
            baseTextColor = targetText.color;

        hasBaseState = true;
    }

    private void RestoreBaseState()
    {
        if (!hasBaseState)
            return;

        transform.localPosition = baseLocalPosition;

        if (targetText != null)
            targetText.color = baseTextColor;
    }

    private void ApplyGlow()
    {
        if (!hasBaseState)
            CaptureBaseState();

        if (!enableGlow || glowIntensity <= 0f || glowPulseSpeed <= 0f)
        {
            targetText.color = baseTextColor;
            return;
        }

        float pulse = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
        float glowBlend = pulse * glowIntensity;

        targetText.color = Color.LerpUnclamped(baseTextColor, glowColor, glowBlend);
    }

    private void ApplyMotion()
    {
        if (!hasBaseState)
            CaptureBaseState();

        Vector3 offset = Vector3.zero;

        if (enableWobble && wobbleAmount > 0f && wobbleSpeed > 0f)
        {
            offset.x += Mathf.Sin(Time.time * wobbleSpeed) * wobbleAmount;
            offset.z += Mathf.Cos(Time.time * wobbleSpeed * 0.7f) * wobbleAmount * 0.25f;
        }

        if (enableFloating && floatAmount > 0f && floatSpeed > 0f)
            offset.y += Mathf.Sin(Time.time * floatSpeed) * floatAmount;

        transform.localPosition = baseLocalPosition + offset;
    }
}
