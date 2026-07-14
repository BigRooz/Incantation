using System.Collections;
using UnityEngine;

public class CameraTransitionManager : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float transitionDuration = 1f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("References")]
    [SerializeField] private Camera activeCamera;

    private Coroutine transitionRoutine;

    public bool IsTransitioning => transitionRoutine != null;

    private void OnDisable()
    {
        CancelCurrentTransition();
    }

    public void MoveTo(Transform target)
    {
        if (!CanMoveToTarget(target))
            return;

        CancelCurrentTransition();

        transitionRoutine = StartCoroutine(RunTransition(target));
    }

    public void MoveToImmediate(Transform target)
    {
        if (!CanMoveToTarget(target))
            return;

        CancelCurrentTransition();
        activeCamera.transform.SetPositionAndRotation(target.position, target.rotation);
    }

    private IEnumerator RunTransition(Transform target)
    {
        Transform cameraTransform = activeCamera.transform;
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        Vector3 targetPosition = target.position;
        Quaternion targetRotation = target.rotation;
        float safeDuration = Mathf.Max(0f, transitionDuration);

        if (safeDuration <= 0f)
        {
            cameraTransform.SetPositionAndRotation(targetPosition, targetRotation);
            transitionRoutine = null;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < safeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float normalizedTime = Mathf.Clamp01(elapsedTime / safeDuration);
            float curvedTime = EvaluateTransitionCurve(normalizedTime);

            cameraTransform.SetPositionAndRotation(
                Vector3.LerpUnclamped(startPosition, targetPosition, curvedTime),
                Quaternion.SlerpUnclamped(startRotation, targetRotation, curvedTime)
            );

            yield return null;
        }

        cameraTransform.SetPositionAndRotation(targetPosition, targetRotation);
        transitionRoutine = null;
    }

    private bool CanMoveToTarget(Transform target)
    {
        if (activeCamera == null)
        {
            Debug.LogWarning($"{nameof(CameraTransitionManager)} cannot move because activeCamera is not assigned.", this);
            return false;
        }

        if (target == null)
        {
            Debug.LogWarning($"{nameof(CameraTransitionManager)} cannot move because target is not assigned.", this);
            return false;
        }

        return true;
    }

    private void CancelCurrentTransition()
    {
        if (transitionRoutine == null)
            return;

        StopCoroutine(transitionRoutine);
        transitionRoutine = null;
    }

    private float EvaluateTransitionCurve(float normalizedTime)
    {
        if (transitionCurve == null)
            return normalizedTime;

        return transitionCurve.Evaluate(normalizedTime);
    }
}
