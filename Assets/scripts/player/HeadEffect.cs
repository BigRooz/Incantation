using UnityEngine;

public class HeadEffects : MonoBehaviour
{
    [Header("Idle")]
    public float breathingAmount = 0.15f;
    public float breathingSpeed = 1.2f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        float offsetY = Mathf.Sin(Time.time * breathingSpeed) * breathingAmount;

        transform.localPosition = startPosition + new Vector3(0f, offsetY, 0f);
    }
}