using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls only the demon hand presentation attached to the single BookModel.
/// Depends on Inspector-assigned handRoot and Animator references.
/// Does not decide ritual failure, move players, eliminate players, or own absorption gameplay.
/// TODO: Future elimination systems can call PlaySequence and subscribe to timing events.
/// </summary>
public class DemonHandController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject handRoot;
    [SerializeField] private Animator handAnimator;

    [Header("Animator Triggers")]
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string resetTriggerName = "Reset";

    [Header("Visibility")]
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private bool deactivateWhenIdle = true;

    [Header("Fallback Timing")]
    [Min(0f)]
    [SerializeField] private float fallbackSequenceDuration = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool playOnStartForDebug = false;

    [Header("Events")]
    [SerializeField] private UnityEvent onSequenceStarted;
    [SerializeField] private UnityEvent onGrabMoment;
    [SerializeField] private UnityEvent onSequenceFinished;

    private Coroutine fallbackRoutine;
    private bool isPlaying;
    private bool hasLoggedMissingReferences;
    private bool hasGrabMomentFired;
    private bool hasValidatedAnimatorParameters;
    private bool hasAttackTrigger;
    private bool hasResetTrigger;
    private bool hasLoggedMissingAttackTrigger;
    private bool hasLoggedMissingResetTrigger;

    private void Awake()
    {
        Debug.Log($"{nameof(DemonHandController)} Awake");
        Debug.Log($"{nameof(DemonHandController)} handRoot name: {(handRoot != null ? handRoot.name : "null")}");
        Debug.Log($"{nameof(DemonHandController)} handAnimator name: {(handAnimator != null ? handAnimator.name : "null")}");
        Debug.Log($"{nameof(DemonHandController)} controller name: {GetAnimatorControllerName()}");

        if (hideOnAwake)
            SetHandActive(false);
    }

    private void Start()
    {
        if (playOnStartForDebug)
            PlaySequence();
    }

    private void OnDisable()
    {
        if (!isPlaying)
            return;

        StopFallbackRoutine();
        isPlaying = false;
        hasGrabMomentFired = false;
    }

    [ContextMenu("Play Sequence")]
    public void PlaySequence()
    {
        Debug.Log($"{nameof(DemonHandController)} PlaySequence() called");
        Debug.Log($"{nameof(DemonHandController)} Already playing? {isPlaying}");

        if (isPlaying)
            return;

        Debug.Log($"{nameof(DemonHandController)} handRoot active? {(handRoot != null ? handRoot.activeSelf.ToString() : "null")}");

        if (!HasRequiredReferences())
            return;

        Debug.Log($"{nameof(DemonHandController)} Activating handRoot");
        Debug.Log($"{nameof(DemonHandController)} Animator reference valid? {handAnimator != null}");
        Debug.Log($"{nameof(DemonHandController)} Animator Controller name: {GetAnimatorControllerName()}");
        Debug.Log($"{nameof(DemonHandController)} Current Animator State: {GetCurrentAnimatorStateDescription()}");

        ValidateAnimatorParameters();

        Debug.Log($"{nameof(DemonHandController)} Attack trigger exists? {hasAttackTrigger}");
        Debug.Log($"{nameof(DemonHandController)} Reset trigger exists? {hasResetTrigger}");

        if (!hasAttackTrigger)
            return;

        isPlaying = true;
        hasGrabMomentFired = false;

        StopFallbackRoutine();
        SetHandActive(true);

        Debug.Log($"{nameof(DemonHandController)} Calling ResetTrigger()");
        ReturnAnimatorToIdle();
        Debug.Log($"{nameof(DemonHandController)} Calling SetTrigger(Attack)");
        SetAnimatorTriggerIfAvailable(attackTriggerName, hasAttackTrigger);
        StartCoroutine(LogAnimatorStateAfterAttackTrigger());

        onSequenceStarted?.Invoke();
        Debug.Log($"{nameof(DemonHandController)} PlaySequence() completed");
        fallbackRoutine = StartCoroutine(FinishAfterFallbackDuration());
    }

    [ContextMenu("Reset Sequence")]
    public void ResetSequence()
    {
        StopFallbackRoutine();
        isPlaying = false;
        hasGrabMomentFired = false;

        if (handAnimator != null)
        {
            ValidateAnimatorParameters();
            ReturnAnimatorToIdle();
            SetAnimatorTriggerIfAvailable(resetTriggerName, hasResetTrigger);
        }

        if (deactivateWhenIdle)
            SetHandActive(false);
    }

    public void AnimationEvent_GrabMoment()
    {
        Debug.Log($"{nameof(DemonHandController)} Grab animation event fired.");

        if (!isPlaying || hasGrabMomentFired)
            return;

        hasGrabMomentFired = true;
        onGrabMoment?.Invoke();
    }

    public void AnimationEvent_SequenceFinished()
    {
        Debug.Log($"{nameof(DemonHandController)} Sequence finished animation event fired.");

        if (!isPlaying)
            return;

        StopFallbackRoutine();
        isPlaying = false;
        hasGrabMomentFired = false;

        onSequenceFinished?.Invoke();
        ReturnAnimatorToIdle();

        if (deactivateWhenIdle)
            SetHandActive(false);
    }

    private IEnumerator FinishAfterFallbackDuration()
    {
        Debug.Log($"{nameof(DemonHandController)} Fallback timer started.");

        float safeDuration = Mathf.Max(0f, fallbackSequenceDuration);

        if (safeDuration > 0f)
            yield return new WaitForSeconds(safeDuration);
        else
            yield return null;

        Debug.Log($"{nameof(DemonHandController)} Fallback timer finished.");
        fallbackRoutine = null;
        AnimationEvent_SequenceFinished();
    }

    private IEnumerator LogAnimatorStateAfterAttackTrigger()
    {
        yield return null;

        if (handAnimator == null)
        {
            Debug.Log($"{nameof(DemonHandController)} Animator state after Attack trigger: Animator null");
            yield break;
        }

        if (!handAnimator.isActiveAndEnabled)
        {
            Debug.Log($"{nameof(DemonHandController)} Animator state after Attack trigger: Animator inactive or disabled");
            yield break;
        }

        AnimatorStateInfo currentStateInfo = handAnimator.GetCurrentAnimatorStateInfo(0);
        AnimatorClipInfo[] currentClipInfo = handAnimator.GetCurrentAnimatorClipInfo(0);
        string currentClipName = currentClipInfo.Length > 0 && currentClipInfo[0].clip != null ? currentClipInfo[0].clip.name : "none";
        bool isInTransition = handAnimator.IsInTransition(0);

        string report = $"{nameof(DemonHandController)} Animator state after Attack trigger:\n"
            + $"Current state: {GetKnownAnimatorStateName(currentStateInfo)}\n"
            + $"Current state fullPathHash: {currentStateInfo.fullPathHash}\n"
            + $"Current state shortNameHash: {currentStateInfo.shortNameHash}\n"
            + $"Current clip: {currentClipName}\n"
            + $"Normalized time: {currentStateInfo.normalizedTime}\n"
            + $"IsInTransition(0): {isInTransition}";

        if (isInTransition)
        {
            AnimatorStateInfo nextStateInfo = handAnimator.GetNextAnimatorStateInfo(0);
            AnimatorClipInfo[] nextClipInfo = handAnimator.GetNextAnimatorClipInfo(0);
            string nextClipName = nextClipInfo.Length > 0 && nextClipInfo[0].clip != null ? nextClipInfo[0].clip.name : "none";

            report += "\n"
                + $"Next state: {GetKnownAnimatorStateName(nextStateInfo)}\n"
                + $"Next state fullPathHash: {nextStateInfo.fullPathHash}\n"
                + $"Next state shortNameHash: {nextStateInfo.shortNameHash}\n"
                + $"Next state normalizedTime: {nextStateInfo.normalizedTime}\n"
                + $"Next clip: {nextClipName}";
        }

        Debug.Log(report);
    }

    private bool HasRequiredReferences()
    {
        if (handRoot != null && handAnimator != null)
            return true;

        if (!hasLoggedMissingReferences)
        {
            Debug.LogWarning($"{nameof(DemonHandController)} on {name} requires both a handRoot GameObject and a handAnimator reference before it can play.");
            hasLoggedMissingReferences = true;
        }

        return false;
    }

    private void ValidateAnimatorParameters()
    {
        if (hasValidatedAnimatorParameters || handAnimator == null)
            return;

        AnimatorControllerParameter[] parameters = handAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type != AnimatorControllerParameterType.Trigger)
                continue;

            if (parameter.name == attackTriggerName)
                hasAttackTrigger = true;

            if (parameter.name == resetTriggerName)
                hasResetTrigger = true;
        }

        hasValidatedAnimatorParameters = true;
        LogMissingTriggerWarnings();
    }

    private void LogMissingTriggerWarnings()
    {
        if (!hasAttackTrigger && !hasLoggedMissingAttackTrigger)
        {
            Debug.LogWarning($"{nameof(DemonHandController)} on {name} could not find Animator trigger '{attackTriggerName}'. The demon hand attack sequence will not play until the Animator Controller defines this trigger.");
            LogAnimatorParameters();
            hasLoggedMissingAttackTrigger = true;
        }

        if (!hasResetTrigger && !hasLoggedMissingResetTrigger)
        {
            Debug.LogWarning($"{nameof(DemonHandController)} on {name} could not find Animator trigger '{resetTriggerName}'. Manual reset trigger playback will be skipped until the Animator Controller defines this trigger.");
            hasLoggedMissingResetTrigger = true;
        }
    }

    private void SetHandActive(bool active)
    {
        if (handRoot == null)
            return;

        handRoot.SetActive(active);
    }

    private void SetAnimatorTriggerIfAvailable(string triggerName, bool triggerExists)
    {
        if (handAnimator == null || !triggerExists || string.IsNullOrWhiteSpace(triggerName))
            return;

        handAnimator.SetTrigger(triggerName);
    }

    private void ResetAnimatorTriggerIfAvailable(string triggerName, bool triggerExists)
    {
        if (handAnimator == null || !triggerExists || string.IsNullOrWhiteSpace(triggerName))
            return;

        handAnimator.ResetTrigger(triggerName);
    }

    private void ReturnAnimatorToIdle()
    {
        if (handAnimator == null)
            return;

        ResetAnimatorTriggerIfAvailable(attackTriggerName, hasAttackTrigger);
        ResetAnimatorTriggerIfAvailable(resetTriggerName, hasResetTrigger);
        handAnimator.Rebind();
        handAnimator.Update(0f);
    }

    private void StopFallbackRoutine()
    {
        if (fallbackRoutine == null)
            return;

        StopCoroutine(fallbackRoutine);
        fallbackRoutine = null;
    }

    private string GetAnimatorControllerName()
    {
        if (handAnimator == null || handAnimator.runtimeAnimatorController == null)
            return "null";

        return handAnimator.runtimeAnimatorController.name;
    }

    private string GetCurrentAnimatorStateDescription()
    {
        if (handAnimator == null)
            return "Animator null";

        if (!handAnimator.isActiveAndEnabled)
            return "Animator inactive or disabled";

        AnimatorStateInfo stateInfo = handAnimator.GetCurrentAnimatorStateInfo(0);
        AnimatorClipInfo[] clipInfo = handAnimator.GetCurrentAnimatorClipInfo(0);
        string clipName = clipInfo.Length > 0 && clipInfo[0].clip != null ? clipInfo[0].clip.name : "none";

        return $"Layer 0, fullPathHash {stateInfo.fullPathHash}, shortNameHash {stateInfo.shortNameHash}, normalizedTime {stateInfo.normalizedTime}, clip {clipName}";
    }

    private string GetKnownAnimatorStateName(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.IsName("Idle"))
            return "Idle";

        if (stateInfo.IsName("Attack"))
            return "Attack";

        if (stateInfo.IsName($"Base Layer.Idle"))
            return "Base Layer.Idle";

        if (stateInfo.IsName($"Base Layer.Attack"))
            return "Base Layer.Attack";

        return "unknown";
    }

    private void LogAnimatorParameters()
    {
        if (handAnimator == null)
            return;

        AnimatorControllerParameter[] parameters = handAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            Debug.Log($"{nameof(DemonHandController)} Animator parameter:\n{parameter.name}\n{parameter.type}");
        }
    }
}
