using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Visual-only player absorption toward the single cursed book.
/// Does not decide failure, elimination, seating, turn order, or ritual state.
/// </summary>
public class PlayerAbsorptionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform absorptionTarget;

    [Header("Absorption")]
    [SerializeField, Min(0f)] private float absorptionDuration = 0.75f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private bool deactivateTargetOnComplete = true;

    [Space]
    [Header("Debug (Development Only)")]
    [SerializeField]
    [Tooltip("Development only. Prevents the absorbed player from being hidden/deactivated after the absorption sequence.")]
    private bool skipPlayerHide = false;

    [SerializeField]
    [Tooltip("Development only. Prevents the absorbed player from being hidden/deactivated after the absorption sequence.")]
    private bool skipPlayerDeactivate = false;

    [Header("Events")]
    [SerializeField] private UnityEvent onAbsorptionStarted;
    [SerializeField] private UnityEvent onAbsorptionFinished;

    private Coroutine absorptionRoutine;
    private Transform activeTarget;
    private AbsorbedTargetState activeTargetState;
    private bool hasLoggedNullTargetWarning;
    private bool hasLoggedMissingAbsorptionTargetWarning;
    private bool isAbsorbing;
    private readonly Dictionary<Transform, AbsorbedTargetState> savedTargetStates = new();
    private readonly List<PreservedCameraPose> preservedCameraPoses = new();

    public void BeginAbsorption(GameObject target)
    {
        BeginAbsorption(target != null ? target.transform : null);
    }

    public void BeginAbsorption(Transform target)
    {
        BeginAbsorption(target, preserveActiveCameraWorldPose: false);
    }

    public void BeginAbsorption(
        Transform target,
        bool preserveActiveCameraWorldPose)
    {
        if (isAbsorbing)
            return;

        if (target == null)
        {
            LogNullTargetWarning();
            return;
        }

        if (absorptionTarget == null)
        {
            LogMissingAbsorptionTargetWarning();
            return;
        }

        StoreOriginalState(target);
        CaptureActiveCameraPoses(preserveActiveCameraWorldPose);
        absorptionRoutine = StartCoroutine(RunAbsorption());
    }

    public void ResetAbsorption()
    {
        if (absorptionRoutine != null)
        {
            StopCoroutine(absorptionRoutine);
            absorptionRoutine = null;
        }

        isAbsorbing = false;

        RestorePreservedCameraPoses();
        preservedCameraPoses.Clear();

        foreach (AbsorbedTargetState savedState in savedTargetStates.Values)
            savedState.Restore();

        savedTargetStates.Clear();
        activeTarget = null;
        activeTargetState = default;
    }

    /// <summary>
    /// Restores only the absorbed target's visual root state for a later presentation.
    /// Position and rotation remain owned by the presentation controller.
    /// </summary>
    public bool TryRestoreAbsorbedTargetForPresentation(Transform target)
    {
        if (target == null || !savedTargetStates.TryGetValue(target, out AbsorbedTargetState savedState))
            return false;

        savedState.RestorePresentationState();
        return savedState.OriginalActiveState;
    }

    private void StoreOriginalState(Transform target)
    {
        activeTarget = target;
        if (!savedTargetStates.TryGetValue(target, out activeTargetState))
        {
            activeTargetState = new AbsorbedTargetState(target);
            savedTargetStates.Add(target, activeTargetState);
        }
    }

    private IEnumerator RunAbsorption()
    {
        isAbsorbing = true;
        onAbsorptionStarted?.Invoke();

        float safeDuration = Mathf.Max(0f, absorptionDuration);
        Vector3 targetPosition = absorptionTarget.position;
        Quaternion targetRotation = GetAbsorptionRotation(targetPosition);
        Debug.Log(
            $"{nameof(PlayerAbsorptionController)} absorption diagnostics start\n" +
            $"Target: {(activeTarget != null ? activeTarget.name : "null")}\n" +
            $"Target start position: {activeTargetState.OriginalPosition}\n" +
            $"Absorption target: {(absorptionTarget != null ? absorptionTarget.name : "null")}\n" +
            $"Absorption target position: {targetPosition}\n" +
            $"Distance: {Vector3.Distance(activeTargetState.OriginalPosition, targetPosition)}\n" +
            $"Target start scale: {activeTargetState.OriginalScale}\n" +
            $"Target final intended scale: {Vector3.zero}\n" +
            $"Absorption duration: {safeDuration}",
            this);

        if (safeDuration <= 0f)
        {
            ApplyAbsorptionFrame(1f, targetPosition, targetRotation);
        }
        else
        {
            float elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / safeDuration);
                ApplyAbsorptionFrame(normalizedTime, targetPosition, targetRotation);
                yield return null;
            }
        }

        ApplyAbsorptionFrame(1f, targetPosition, targetRotation);

        bool shouldHidePlayer = !skipPlayerHide;
        bool shouldDeactivatePlayer = deactivateTargetOnComplete && !skipPlayerDeactivate && !skipPlayerHide;

        if (activeTarget != null)
        {
            if (!shouldHidePlayer)
                activeTarget.localScale = activeTargetState.OriginalScale;

            if (shouldDeactivatePlayer)
                activeTarget.gameObject.SetActive(false);
        }

        Debug.Log(
            $"{nameof(PlayerAbsorptionController)} absorption diagnostics complete\n" +
            $"Final target position: {(activeTarget != null ? activeTarget.position.ToString() : "null")}\n" +
            $"Final target scale: {(activeTarget != null ? activeTarget.localScale.ToString() : "null")}\n" +
            $"Target deactivated: {shouldDeactivatePlayer}",
            this);

        isAbsorbing = false;
        absorptionRoutine = null;
        RestorePreservedCameraPoses();
        onAbsorptionFinished?.Invoke();
        preservedCameraPoses.Clear();
    }

    private void ApplyAbsorptionFrame(float normalizedTime, Vector3 targetPosition, Quaternion targetRotation)
    {
        if (activeTarget == null)
            return;

        float movementAmount = EvaluateCurve(movementCurve, normalizedTime);
        float scaleAmount = EvaluateCurve(scaleCurve, normalizedTime);

        activeTarget.position = Vector3.LerpUnclamped(
            activeTargetState.OriginalPosition,
            targetPosition,
            movementAmount);
        activeTarget.rotation = Quaternion.SlerpUnclamped(
            activeTargetState.OriginalRotation,
            targetRotation,
            movementAmount);
        activeTarget.localScale = activeTargetState.OriginalScale * scaleAmount;
        RestorePreservedCameraPoses();
    }

    private void CaptureActiveCameraPoses(bool preserveActiveCameraWorldPose)
    {
        preservedCameraPoses.Clear();
        if (!preserveActiveCameraWorldPose || activeTarget == null)
            return;

        Camera[] cameras = FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
                continue;

            Transform cameraTransform = camera.transform;
            if (cameraTransform != activeTarget && !cameraTransform.IsChildOf(activeTarget))
                continue;

            preservedCameraPoses.Add(new PreservedCameraPose(cameraTransform));
            Debug.Log(
                "[DeathCamera] Camera Mutation Request\n" +
                $"Camera = {camera.name}\n" +
                $"Hierarchy = {GetHierarchyPath(camera.transform)}\n" +
                "Operation = Move / Rotate by absorbed ancestor\n" +
                "Result = Ignored\n" +
                $"Source = {nameof(PlayerAbsorptionController)}",
                this);
        }
    }

    private void RestorePreservedCameraPoses()
    {
        for (int i = 0; i < preservedCameraPoses.Count; i++)
            preservedCameraPoses[i].Restore();
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
            return "none";

        string path = target.name;
        for (Transform parent = target.parent; parent != null; parent = parent.parent)
            path = $"{parent.name}/{path}";

        return path;
    }

    private readonly struct PreservedCameraPose
    {
        private readonly Transform cameraTransform;
        private readonly Vector3 position;
        private readonly Quaternion rotation;

        public PreservedCameraPose(Transform cameraTransform)
        {
            this.cameraTransform = cameraTransform;
            position = cameraTransform.position;
            rotation = cameraTransform.rotation;
        }

        public void Restore()
        {
            if (cameraTransform != null)
                cameraTransform.SetPositionAndRotation(position, rotation);
        }
    }

    private readonly struct AbsorbedTargetState
    {
        private readonly Transform target;

        public AbsorbedTargetState(Transform target)
        {
            this.target = target;
            OriginalPosition = target.position;
            OriginalRotation = target.rotation;
            OriginalScale = target.localScale;
            OriginalActiveState = target.gameObject.activeSelf;
        }

        public Vector3 OriginalPosition { get; }
        public Quaternion OriginalRotation { get; }
        public Vector3 OriginalScale { get; }
        public bool OriginalActiveState { get; }

        public void Restore()
        {
            if (target == null)
                return;

            target.position = OriginalPosition;
            target.rotation = OriginalRotation;
            RestorePresentationState();
        }

        public void RestorePresentationState()
        {
            if (target == null)
                return;

            target.localScale = OriginalScale;
            target.gameObject.SetActive(OriginalActiveState);
        }
    }

    private Quaternion GetAbsorptionRotation(Vector3 targetPosition)
    {
        Vector3 directionToTarget = targetPosition - activeTargetState.OriginalPosition;

        if (directionToTarget.sqrMagnitude <= 0.0001f)
            return activeTargetState.OriginalRotation;

        return Quaternion.LookRotation(directionToTarget.normalized, Vector3.up);
    }

    private float EvaluateCurve(AnimationCurve curve, float normalizedTime)
    {
        if (curve == null || curve.length == 0)
            return normalizedTime;

        return curve.Evaluate(normalizedTime);
    }

    private void LogNullTargetWarning()
    {
        if (hasLoggedNullTargetWarning)
            return;

        Debug.LogWarning($"{nameof(PlayerAbsorptionController)} on '{gameObject.name}' cannot begin absorption because the target is null.", this);
        hasLoggedNullTargetWarning = true;
    }

    private void LogMissingAbsorptionTargetWarning()
    {
        if (hasLoggedMissingAbsorptionTargetWarning)
            return;

        Debug.LogWarning($"{nameof(PlayerAbsorptionController)} on '{gameObject.name}' requires an absorptionTarget Transform near the book.", this);
        hasLoggedMissingAbsorptionTargetWarning = true;
    }
}
