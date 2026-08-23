using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the visual state and animated presentation of exactly three authored spell cards.
/// It depends only on Inspector-assigned card views and pose transforms and never executes spells.
/// </summary>
public sealed class SpellHandController : MonoBehaviour
{
    public enum CardVisualState
    {
        Hidden,
        OnTable,
        Raised,
        Selected,
        Consumed
    }

    private const int CardCount = 3;

    [Header("Exactly Three Cards")]
    [SerializeField] private SpellCardView[] cardViews = new SpellCardView[CardCount];

    [Header("Table Poses")]
    [SerializeField] private Transform[] cardSlots = new Transform[CardCount];

    [Header("Raised Poses")]
    [SerializeField] private Transform[] raisedPoses = new Transform[CardCount];
    [SerializeField] private Transform inspectPose;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float animationDuration = 0.35f;
    [SerializeField, Min(0f)] private float cardLiftHeight = 0.05f;
    [SerializeField] private float fanAngle = 6f;
    [SerializeField] private Vector3 selectionOffset = new Vector3(0f, 0f, 0.08f);
    [SerializeField, Min(0.01f)] private float selectionScale = 1.08f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve consumeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField, Min(0.01f)] private float consumeDuration = 0.2f;

    [Header("Initial State")]
    [SerializeField] private bool visibleOnAwake;
    [SerializeField] private bool openOnAwake;

    [Header("Temporary Debug Input")]
    [SerializeField] private bool enableDebugInput = true;
    [SerializeField] private KeyCode visibilityKey = KeyCode.H;
    [SerializeField] private KeyCode openCloseKey = KeyCode.E;
    [SerializeField] private KeyCode consumeKey = KeyCode.Space;

    private readonly CardVisualState[] cardStates = new CardVisualState[CardCount];
    private readonly Coroutine[] cardAnimations = new Coroutine[CardCount];
    private readonly Vector3[] authoredScales = new Vector3[CardCount];
    private readonly Vector3[] tableLocalPositions = new Vector3[CardCount];
    private readonly Quaternion[] tableLocalRotations = new Quaternion[CardCount];
    private readonly bool[] hasCachedTablePose = new bool[CardCount];

    private bool isVisible;
    private bool isOpen;
    private bool interactionAllowed = true;
    private int selectedIndex = -1;

    public bool IsVisible => isVisible;
    public bool IsOpen => isOpen;
    public int SelectedIndex => selectedIndex;
    public float ConsumptionDuration => consumeDuration;

    private void Awake()
    {
        CacheAuthoredScales();
        CacheTablePoses();
        SnapCardsToTable();

        if (visibleOnAwake)
            ShowHand();
        else
            HideHand();

        if (visibleOnAwake && openOnAwake)
            OpenHand();
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!enableDebugInput || !LocalInputContextGate.AllowsGameplayInput)
            return;

        if (Input.GetKeyDown(visibilityKey))
        {
            if (isVisible)
                HideHand();
            else
                ShowHand();
        }

        if (Input.GetKeyDown(openCloseKey))
        {
            if (isOpen)
                CloseHand();
            else
                OpenHand();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
            SelectCard(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            SelectCard(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            SelectCard(2);

        if (Input.GetKeyDown(consumeKey))
            ConsumeSelectedCard();
#endif
    }

    public void SetDebugInputEnabled(bool enabled)
    {
        enableDebugInput = enabled;
    }

    public void SetInteractionAllowed(bool allowed)
    {
        interactionAllowed = allowed;
        if (!interactionAllowed && isOpen)
            CloseHand();
    }

    /// <summary>
    /// Applies private authoritative slot contents without changing any gameplay state.
    /// Empty slots are hidden and occupied slots return to their authored table poses.
    /// </summary>
    public void ApplyAuthoritativeHand(IReadOnlyList<SpellDefinition> slots)
    {
        ApplyAuthoritativeHand(slots, null);
    }

    /// <summary>
    /// Applies private authoritative contents while optionally preserving physical card identity
    /// from each new slot's previous slot. An already raised hand remains raised.
    /// </summary>
    public void ApplyAuthoritativeHand(
        IReadOnlyList<SpellDefinition> slots,
        IReadOnlyList<int> previousSlotForNewSlot)
    {
        bool preserveRaisedPose = isOpen;
        ClearSelectedCardPresentation();
        selectedIndex = -1;
        RemapCardViews(previousSlotForNewSlot);
        isOpen = preserveRaisedPose;
        isVisible = false;

        for (int index = 0; index < CardCount; index++)
        {
            StopCardAnimation(index);
            SpellDefinition definition = slots != null && index < slots.Count
                ? slots[index]
                : null;
            if (!HasCard(index))
                continue;

            cardViews[index].SetDefinition(definition);
            bool occupied = definition != null;
            cardViews[index].SetVisible(occupied);
            if (!occupied)
            {
                cardStates[index] = CardVisualState.Hidden;
                continue;
            }

            if (preserveRaisedPose)
            {
                Pose target = GetRaisedPose(index);
                StartCardAnimation(
                    index,
                    target.position,
                    target.rotation,
                    authoredScales[index],
                    animationDuration,
                    openCurve,
                    CardVisualState.Raised);
            }
            else
            {
                SnapCardToTable(index);
            }

            isVisible |= occupied;
        }
    }

    /// <summary>Enables every non-consumed card and returns hidden cards to the table state.</summary>
    public void ShowHand()
    {
        isVisible = true;
        isOpen = false;
        selectedIndex = -1;

        for (int index = 0; index < CardCount; index++)
        {
            if (!CanPresentCard(index))
                continue;

            StopCardAnimation(index);
            SnapCardToTable(index);
            cardViews[index].SetVisible(true);
            cardStates[index] = CardVisualState.OnTable;
        }
    }

    /// <summary>Disables the visual hand without changing which cards have been consumed.</summary>
    public void HideHand()
    {
        ClearSelectedCardPresentation();
        isVisible = false;
        isOpen = false;
        selectedIndex = -1;

        for (int index = 0; index < CardCount; index++)
        {
            StopCardAnimation(index);

            if (!HasCard(index))
                continue;

            if (cardStates[index] == CardVisualState.Consumed)
                continue;

            SnapCardToTable(index);
            cardViews[index].SetVisible(false);
            cardStates[index] = CardVisualState.Hidden;
        }
    }

    /// <summary>Animates all available cards from their current poses to the raised fan.</summary>
    public void OpenHand()
    {
        if (!interactionAllowed)
            return;

        ClearSelectedCardPresentation();

        if (!isVisible)
            ShowHand();

        isOpen = true;
        selectedIndex = -1;

        for (int index = 0; index < CardCount; index++)
        {
            if (!CanPresentCard(index))
                continue;

            Pose target = GetRaisedPose(index);
            StartCardAnimation(index, target.position, target.rotation, authoredScales[index], animationDuration, openCurve, CardVisualState.Raised);
        }
    }

    /// <summary>Animates all available cards back to their authored table slots.</summary>
    public void CloseHand()
    {
        ClearSelectedCardPresentation();
        isOpen = false;
        selectedIndex = -1;

        if (!isVisible)
            return;

        for (int index = 0; index < CardCount; index++)
        {
            if (!CanPresentCard(index) || !hasCachedTablePose[index])
                continue;

            StartCardAnimation(
                index,
                transform.TransformPoint(tableLocalPositions[index]),
                transform.rotation * tableLocalRotations[index],
                authoredScales[index],
                animationDuration,
                closeCurve,
                CardVisualState.OnTable);
        }
    }

    /// <summary>Selects one visible card by zero-based index and presents it at the inspect pose.</summary>
    public void SelectCard(int index)
    {
        if (!isVisible || !isOpen || !CanPresentCard(index))
            return;

        int previousSelection = selectedIndex;
        if (previousSelection == index)
            return;

        ClearSelectedCardPresentation();
        selectedIndex = index;

        if (previousSelection >= 0 && previousSelection != index && CanPresentCard(previousSelection))
        {
            Pose previousTarget = GetRaisedPose(previousSelection);
            StartCardAnimation(previousSelection, previousTarget.position, previousTarget.rotation, authoredScales[previousSelection], animationDuration, openCurve, CardVisualState.Raised);
        }

        cardViews[index].SetSelected(true);
        Pose selectedPose = GetSelectedPose(index);
        StartCardAnimation(index, selectedPose.position, selectedPose.rotation, authoredScales[index] * selectionScale, animationDuration, openCurve, CardVisualState.Selected);
    }

    public void ClearSelection()
    {
        int previousSelection = selectedIndex;
        ClearSelectedCardPresentation();
        selectedIndex = -1;
        if (isOpen && previousSelection >= 0 && CanPresentCard(previousSelection))
        {
            Pose target = GetRaisedPose(previousSelection);
            StartCardAnimation(
                previousSelection,
                target.position,
                target.rotation,
                authoredScales[previousSelection],
                animationDuration,
                openCurve,
                CardVisualState.Raised);
        }
    }

    public bool TryGetGazeCardIndex(Ray gazeRay, float maximumDistance, out int cardIndex)
    {
        cardIndex = -1;
        if (!isVisible || !isOpen)
            return false;

        float closestDistance = Mathf.Max(0f, maximumDistance);
        for (int index = 0; index < CardCount; index++)
        {
            if (!CanPresentCard(index) || cardViews[index].CardMesh == null ||
                !cardViews[index].CardMesh.enabled)
            {
                continue;
            }

            if (!cardViews[index].CardMesh.bounds.IntersectRay(gazeRay, out float distance) ||
                distance < 0f || distance > closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            cardIndex = index;
        }

        return cardIndex >= 0;
    }

    /// <summary>Plays a placeholder shrink animation on the selected card, then hides it.</summary>
    public void ConsumeSelectedCard()
    {
        if (selectedIndex < 0 || !CanPresentCard(selectedIndex))
            return;

        int consumedIndex = selectedIndex;
        selectedIndex = -1;
        cardViews[consumedIndex].DisableGlowLightImmediately();
        StopCardAnimation(consumedIndex);
        cardStates[consumedIndex] = CardVisualState.Consumed;
        cardAnimations[consumedIndex] = StartCoroutine(ConsumeCardRoutine(consumedIndex));
    }

    public void ConsumeCardAt(int index)
    {
        if (!CanPresentCard(index))
            return;

        ClearSelectedCardPresentation();
        selectedIndex = index;
        ConsumeSelectedCard();
    }

    public CardVisualState GetCardState(int index)
    {
        return index >= 0 && index < CardCount ? cardStates[index] : CardVisualState.Hidden;
    }

    private void CacheAuthoredScales()
    {
        for (int index = 0; index < CardCount; index++)
            authoredScales[index] = HasCard(index) ? cardViews[index].transform.localScale : Vector3.one;
    }

    private void CacheTablePoses()
    {
        for (int index = 0; index < CardCount; index++)
        {
            if (!HasPose(cardSlots, index))
                continue;

            tableLocalPositions[index] = transform.InverseTransformPoint(cardSlots[index].position);
            tableLocalRotations[index] = Quaternion.Inverse(transform.rotation) * cardSlots[index].rotation;
            hasCachedTablePose[index] = true;
        }
    }

    private void SnapCardsToTable()
    {
        for (int index = 0; index < CardCount; index++)
            SnapCardToTable(index);
    }

    private void SnapCardToTable(int index)
    {
        if (!HasCard(index) || !hasCachedTablePose[index])
            return;

        cardViews[index].transform.SetPositionAndRotation(
            transform.TransformPoint(tableLocalPositions[index]),
            transform.rotation * tableLocalRotations[index]);
        cardViews[index].transform.localScale = authoredScales[index];
        cardStates[index] = CardVisualState.OnTable;
    }

    private Pose GetRaisedPose(int index)
    {
        Transform pose = HasPose(raisedPoses, index) ? raisedPoses[index] : null;

        if (pose == null && HasPose(cardSlots, index))
            pose = cardSlots[index];

        if (pose == null)
            pose = cardViews[index].transform;

        Vector3 position = pose.position + pose.up * cardLiftHeight;
        float normalizedIndex = index - ((CardCount - 1) * 0.5f);
        Quaternion rotation = pose.rotation * Quaternion.AngleAxis(-normalizedIndex * fanAngle, Vector3.forward);
        return new Pose(position, rotation);
    }

    private Pose GetSelectedPose(int index)
    {
        if (inspectPose != null)
            return new Pose(inspectPose.TransformPoint(selectionOffset), inspectPose.rotation);

        Pose raisedPose = GetRaisedPose(index);
        return new Pose(raisedPose.position + raisedPose.rotation * selectionOffset, raisedPose.rotation);
    }

    private void StartCardAnimation(int index, Vector3 position, Quaternion rotation, Vector3 scale, float duration, AnimationCurve curve, CardVisualState finalState)
    {
        StopCardAnimation(index);
        cardAnimations[index] = StartCoroutine(AnimateCardRoutine(index, position, rotation, scale, duration, curve, finalState));
    }

    private IEnumerator AnimateCardRoutine(int index, Vector3 targetPosition, Quaternion targetRotation, Vector3 targetScale, float duration, AnimationCurve curve, CardVisualState finalState)
    {
        Transform cardTransform = cardViews[index].transform;
        Vector3 startPosition = cardTransform.position;
        Quaternion startRotation = cardTransform.rotation;
        Vector3 startScale = cardTransform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float curvedProgress = curve != null ? curve.Evaluate(progress) : progress;
            cardTransform.SetPositionAndRotation(
                Vector3.LerpUnclamped(startPosition, targetPosition, curvedProgress),
                Quaternion.SlerpUnclamped(startRotation, targetRotation, curvedProgress));
            cardTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, curvedProgress);
            yield return null;
        }

        cardTransform.SetPositionAndRotation(targetPosition, targetRotation);
        cardTransform.localScale = targetScale;
        cardStates[index] = finalState;
        cardAnimations[index] = null;
    }

    private IEnumerator ConsumeCardRoutine(int index)
    {
        Transform cardTransform = cardViews[index].transform;
        Vector3 startScale = cardTransform.localScale;
        float elapsed = 0f;

        while (elapsed < consumeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / consumeDuration);
            float scaleAmount = consumeCurve != null ? consumeCurve.Evaluate(progress) : 1f - progress;
            cardTransform.localScale = startScale * Mathf.Max(0f, scaleAmount);
            yield return null;
        }

        cardTransform.localScale = Vector3.zero;
        cardViews[index].SetVisible(false);
        cardStates[index] = CardVisualState.Consumed;
        cardAnimations[index] = null;
    }

    private void StopCardAnimation(int index)
    {
        if (index < 0 || index >= CardCount || cardAnimations[index] == null)
            return;

        StopCoroutine(cardAnimations[index]);
        cardAnimations[index] = null;
    }

    private void ClearSelectedCardPresentation()
    {
        for (int index = 0; index < CardCount; index++)
        {
            if (HasCard(index))
                cardViews[index].SetSelected(false);
        }
    }

    private void RemapCardViews(IReadOnlyList<int> previousSlotForNewSlot)
    {
        if (previousSlotForNewSlot == null)
            return;

        for (int index = 0; index < CardCount; index++)
            StopCardAnimation(index);

        SpellCardView[] previousViews = (SpellCardView[])cardViews.Clone();
        Vector3[] previousScales = (Vector3[])authoredScales.Clone();
        bool[] assigned = new bool[CardCount];
        for (int newSlot = 0; newSlot < CardCount; newSlot++)
        {
            int previousSlot = newSlot < previousSlotForNewSlot.Count
                ? previousSlotForNewSlot[newSlot]
                : -1;
            if (previousSlot < 0 || previousSlot >= CardCount || assigned[previousSlot])
                continue;

            cardViews[newSlot] = previousViews[previousSlot];
            authoredScales[newSlot] = previousScales[previousSlot];
            assigned[previousSlot] = true;
        }

        int nextUnused = 0;
        for (int newSlot = 0; newSlot < CardCount; newSlot++)
        {
            int previousSlot = newSlot < previousSlotForNewSlot.Count
                ? previousSlotForNewSlot[newSlot]
                : -1;
            if (previousSlot >= 0 && previousSlot < CardCount &&
                cardViews[newSlot] == previousViews[previousSlot])
            {
                continue;
            }

            while (nextUnused < CardCount && assigned[nextUnused])
                nextUnused++;
            if (nextUnused >= CardCount)
                break;

            cardViews[newSlot] = previousViews[nextUnused];
            authoredScales[newSlot] = previousScales[nextUnused];
            assigned[nextUnused] = true;
        }
    }

    private bool CanPresentCard(int index)
    {
        return HasCard(index) && cardViews[index].CurrentDefinition != null &&
            cardStates[index] != CardVisualState.Consumed;
    }

    private bool HasCard(int index)
    {
        return index >= 0 && index < CardCount && cardViews != null && cardViews.Length == CardCount && cardViews[index] != null;
    }

    private static bool HasPose(Transform[] poses, int index)
    {
        return poses != null && poses.Length == CardCount && index >= 0 && index < CardCount && poses[index] != null;
    }

    private void OnValidate()
    {
        EnsureExactArraySize(ref cardViews);
        EnsureExactArraySize(ref cardSlots);
        EnsureExactArraySize(ref raisedPoses);
        animationDuration = Mathf.Max(0.01f, animationDuration);
        consumeDuration = Mathf.Max(0.01f, consumeDuration);
        selectionScale = Mathf.Max(0.01f, selectionScale);
    }

    private static void EnsureExactArraySize<T>(ref T[] array)
    {
        if (array != null && array.Length == CardCount)
            return;

        T[] resized = new T[CardCount];
        if (array != null)
            System.Array.Copy(array, resized, Mathf.Min(array.Length, CardCount));

        array = resized;
    }
}
