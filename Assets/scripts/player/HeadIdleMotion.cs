using UnityEngine;

public class HeadIdleMotion : MonoBehaviour
{
    public float idleAmount = 1.5f;
    public float idleSpeed = 1.2f;

    private Quaternion startRotation;

    void Start()
    {
        startRotation = transform.localRotation;
    }

    void LateUpdate()
    {
        float idleX = Mathf.Sin(Time.time * idleSpeed) * idleAmount;
        float idleZ = Mathf.Cos(Time.time * idleSpeed * 0.7f) * idleAmount;

        transform.localRotation = startRotation * Quaternion.Euler(idleX, 0f, idleZ);
    }
}