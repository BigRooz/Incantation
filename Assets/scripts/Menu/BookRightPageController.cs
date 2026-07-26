using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Incantation.Networking;

/// <summary>
/// Prepares and presents contextual content on the Living Book's right page.
/// It does not own Book state and delegates animated rewrites to BookTextTransitionController.
/// </summary>
public sealed class BookRightPageController : MonoBehaviour
{
    [Header("Existing Right Page Text")]
    [SerializeField] private TMP_Text rightTitle;
    [SerializeField] private TMP_Text rightLine1;
    [SerializeField] private TMP_Text rightLine2;
    [SerializeField] private TMP_Text rightLine3;
    [SerializeField] private TMP_Text rightLine4;
    [SerializeField] private TMP_Text rightLine5;

    [Header("Independent Right Page Menu Items")]
    [SerializeField] private BookMenuItem rightMenuItem1;
    [SerializeField] private BookMenuItem rightMenuItem2;
    [SerializeField] private BookMenuItem rightMenuItem3;
    [SerializeField] private BookMenuItem rightMenuItem4;
    [SerializeField] private BookMenuItem rightMenuItem5;

    [Header("Menu Actions")]
    [SerializeField] private BookMenuController bookMenuController;

    [Header("Page Presentation")]
    [SerializeField] private BookTextTransitionController textTransitionController;
    [SerializeField] private Color disabledButtonColor = new Color(0.42f, 0.42f, 0.42f, 1f);
    [SerializeField, Range(0.15f, 0.2f)] private float buttonFadeDuration = 0.18f;

    [Header("Voice Page")]
    [SerializeField] private VoiceBookPageController voiceBookPageController;

    private readonly List<TMP_Text> transitionTexts = new List<TMP_Text>();
    private readonly List<string> transitionTargets = new List<string>();
    private Color validateButtonColor;
    private Material validateButtonMaterial;
    private Coroutine buttonFadeCoroutine;
    private BookState preparedState;
    private bool validateButtonWasEnabled;

    private void Awake()
    {
        if (rightLine3 != null)
        {
            validateButtonColor = rightLine3.color;
            validateButtonMaterial = rightLine3.fontSharedMaterial;
        }
    }

    public void RefreshForState(BookState state)
    {
        transitionTexts.Clear();
        transitionTargets.Clear();
        PrepareForState(state, transitionTexts, transitionTargets);
        PlayPreparedTransition();
    }

    public void PrepareForState(
        BookState state,
        List<TMP_Text> texts,
        List<string> targetStrings)
    {
        bool wasJoinState = IsJoinState(preparedState);
        bool willBeJoinState = IsJoinState(state);
        if (wasJoinState && !willBeJoinState)
        {
            RestoreValidateSealVisualState();
        }

        preparedState = state;

        switch (state)
        {
            case BookState.MainMenu:
                PrepareMainPage(texts, targetStrings);
                break;
            case BookState.HostMenu:
                PrepareHostPage(texts, targetStrings);
                break;
            case BookState.JoinMenu:
            case BookState.JoinSealEntry:
                PrepareJoinSealPage(texts, targetStrings);
                break;
            case BookState.CharacterMenu:
                PrepareContextPage(
                    texts, targetStrings, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty,
                    bookMenuController != null ? bookMenuController.ShowCharacter : null);
                break;
            case BookState.VoiceMenu:
                ClearRightPageActions();
                if (voiceBookPageController != null)
                {
                    voiceBookPageController.PreparePage(
                        texts, targetStrings,
                        rightTitle, rightLine1, rightLine2, rightLine3, rightLine4, rightLine5,
                        rightMenuItem1, rightMenuItem2, rightMenuItem3, rightMenuItem4, rightMenuItem5);
                }
                else
                {
                    PrepareClearPage(texts, targetStrings);
                }
                break;
            default:
                PrepareClearPage(texts, targetStrings);
                break;
        }
    }

    public void CompletePreparedTransition()
    {
        if (IsJoinState(preparedState))
        {
            ConfigureJoinSealInteractions();
        }
        else if (preparedState == BookState.HostMenu &&
                 RitualSealService.Instance != null &&
                 RitualSealService.Instance.IsHostingRitual)
        {
            RefreshInteraction(rightMenuItem1, null, false);
            RefreshInteraction(rightMenuItem2, null, false);
            RefreshInteraction(rightMenuItem3, null, false);
            RefreshInteraction(rightMenuItem4, null, false);
            RefreshInteraction(rightMenuItem5, null, false);
            validateButtonWasEnabled = false;
        }
        else
        {
            SetMenuItemInteraction(rightMenuItem1, rightLine1);
            SetMenuItemInteraction(rightMenuItem2, rightLine2);
            SetMenuItemInteraction(rightMenuItem3, rightLine3);
            SetMenuItemInteraction(rightMenuItem4, rightLine4);
            SetMenuItemInteraction(rightMenuItem5, rightLine5);
            validateButtonWasEnabled = false;
        }
    }

    public void RefreshJoinSealSilently()
    {
        if (!IsJoinState(preparedState))
        {
            return;
        }

        RitualSealService service = RitualSealService.Instance;
        string seal = bookMenuController != null ? bookMenuController.EnteredSeal : string.Empty;
        bool canSubmit = bookMenuController != null && bookMenuController.CanSubmitSeal;

        ApplyTextImmediately(rightTitle, string.Empty);
        ApplyTextImmediately(rightLine1, "Seal");
        ApplyTextImmediately(rightLine2, FormatSealField(seal));
        ApplyTextImmediately(rightLine3, "Validate Seal");
        ApplyTextImmediately(rightLine4, GetJoinStatus(service, seal, canSubmit));
        ApplyTextImmediately(rightLine5, string.Empty);

        ConfigureJoinSealInteractions();
    }

    public void PrepareLobbyPage(
        List<TMP_Text> texts,
        List<string> targets,
        string line1Text,
        string line2Text,
        string line3Text,
        UnityAction line3Action)
    {
        PrepareEntry(texts, targets, rightTitle, null, string.Empty, null);
        PrepareEntry(texts, targets, rightLine1, rightMenuItem1, line1Text, null);
        PrepareEntry(texts, targets, rightLine2, rightMenuItem2, line2Text, null);
        PrepareEntry(texts, targets, rightLine3, rightMenuItem3, line3Text, line3Action);
        PrepareEntry(texts, targets, rightLine4, rightMenuItem4, string.Empty, null);
        PrepareEntry(texts, targets, rightLine5, rightMenuItem5, string.Empty, null);
    }

    public void RefreshLobbyContent(
        string line1Text,
        string line2Text,
        string line3Text,
        UnityAction line3Action)
    {
        ApplyTextImmediately(rightLine1, line1Text);
        ApplyTextImmediately(rightLine2, line2Text);
        ApplyTextImmediately(rightLine3, line3Text);

        RefreshInteraction(rightMenuItem1, null, false);
        RefreshInteraction(rightMenuItem2, null, false);
        RefreshInteraction(rightMenuItem3, line3Action, line3Action != null);
    }

    public void RefreshHostPageSilently()
    {
        preparedState = BookState.HostMenu;
        RitualSealService service = RitualSealService.Instance;
        bool isHostingRitual = service != null && service.IsHostingRitual;

        if (isHostingRitual)
        {
            int playerCount = NetworkPlayer.CircleMemberCount;
            ApplyTextImmediately(rightTitle, string.Empty);
            ApplyTextImmediately(rightLine1, $"Seal: {service.ActiveSeal}");
            ApplyTextImmediately(rightLine2, GetSealReplacementStatus(service));
            ApplyTextImmediately(
                rightLine3,
                $"{playerCount} / {NetworkPlayer.MaximumCircleMembers}");
            ApplyTextImmediately(rightLine4, GetHostWaitingMessage(playerCount));
            ApplyTextImmediately(rightLine5, string.Empty);
        }
        else
        {
            ApplyTextImmediately(rightTitle, string.Empty);
            ApplyTextImmediately(rightLine1, string.Empty);
            ApplyTextImmediately(rightLine2, string.Empty);
            ApplyTextImmediately(rightLine3, string.Empty);
            ApplyTextImmediately(rightLine4, string.Empty);
            ApplyTextImmediately(rightLine5, string.Empty);
        }

        RefreshInteraction(
            rightMenuItem1,
            bookMenuController != null ? bookMenuController.EditHostedSeal : null,
            bookMenuController != null);
        RefreshInteraction(rightMenuItem2, null, false);
        RefreshInteraction(rightMenuItem3, null, false);
        RefreshInteraction(rightMenuItem4, null, false);
        RefreshInteraction(rightMenuItem5, null, false);
    }

    public void RefreshRitualCreationStatusSilently(string status)
    {
        ApplyTextImmediately(rightTitle, string.Empty);
        ApplyTextImmediately(rightLine1, status);
        ApplyTextImmediately(rightLine2, string.Empty);
        ApplyTextImmediately(rightLine3, string.Empty);
        ApplyTextImmediately(rightLine4, string.Empty);
        ApplyTextImmediately(rightLine5, string.Empty);

        RefreshInteraction(rightMenuItem1, null, false);
        RefreshInteraction(rightMenuItem2, null, false);
        RefreshInteraction(rightMenuItem3, null, false);
        RefreshInteraction(rightMenuItem4, null, false);
        RefreshInteraction(rightMenuItem5, null, false);
    }

    public void ShowPriestNameEditor(
        string value,
        string status,
        UnityAction confirmAction,
        UnityAction cancelAction)
    {
        ApplyTextImmediately(rightTitle, "PRIEST NAME");
        ApplyTextImmediately(rightLine1, value);
        ApplyTextImmediately(rightLine2, status);
        ApplyTextImmediately(rightLine3, "Confirm");
        ApplyTextImmediately(rightLine4, "Cancel");
        ApplyTextImmediately(rightLine5, string.Empty);
        RefreshInteraction(rightMenuItem1, null, false);
        RefreshInteraction(rightMenuItem2, null, false);
        RefreshInteraction(rightMenuItem3, confirmAction, confirmAction != null);
        RefreshInteraction(rightMenuItem4, cancelAction, cancelAction != null);
        RefreshInteraction(rightMenuItem5, null, false);
    }

    public void ShowHostedSealEditor(
        string value,
        string status,
        UnityAction confirmAction,
        UnityAction cancelAction)
    {
        ApplyTextImmediately(rightTitle, "RITUAL SEAL");
        ApplyTextImmediately(rightLine1, $"Seal: {value}");
        ApplyTextImmediately(rightLine2, status);
        ApplyTextImmediately(rightLine3, "Confirm");
        ApplyTextImmediately(rightLine4, "Cancel");
        ApplyTextImmediately(rightLine5, string.Empty);
        RefreshInteraction(rightMenuItem1, null, false);
        RefreshInteraction(rightMenuItem2, null, false);
        RefreshInteraction(rightMenuItem3, confirmAction, confirmAction != null);
        RefreshInteraction(rightMenuItem4, cancelAction, cancelAction != null);
        RefreshInteraction(rightMenuItem5, null, false);
    }

    public void PrepareRitualCreationStatus(
        BookState state,
        string status,
        List<TMP_Text> texts,
        List<string> targetStrings)
    {
        preparedState = state;
        PrepareEntry(texts, targetStrings, rightTitle, null, string.Empty, null);
        PrepareEntry(texts, targetStrings, rightLine1, rightMenuItem1, status, null);
        PrepareEntry(texts, targetStrings, rightLine2, rightMenuItem2, string.Empty, null);
        PrepareEntry(texts, targetStrings, rightLine3, rightMenuItem3, string.Empty, null);
        PrepareEntry(texts, targetStrings, rightLine4, rightMenuItem4, string.Empty, null);
        PrepareEntry(texts, targetStrings, rightLine5, rightMenuItem5, string.Empty, null);
    }

    public void ClearRightPage()
    {
        transitionTexts.Clear();
        transitionTargets.Clear();
        PrepareClearPage(transitionTexts, transitionTargets);
        PlayPreparedTransition();
    }

    public void ShowContextPage(
        string titleText,
        string line1Text,
        string line2Text,
        string line3Text,
        string line4Text,
        UnityAction showCharacterAction)
    {
        transitionTexts.Clear();
        transitionTargets.Clear();
        PrepareContextPage(
            transitionTexts, transitionTargets, titleText, line1Text, line2Text,
            line3Text, line4Text, showCharacterAction);
        PlayPreparedTransition();
    }

    public void ShowSelectionPage(
        string titleText,
        IReadOnlyList<string> optionNames,
        IReadOnlyList<UnityAction> optionActions)
    {
        transitionTexts.Clear();
        transitionTargets.Clear();
        PrepareEntry(transitionTexts, transitionTargets, rightTitle, null, titleText, null);
        PrepareSelectionEntry(0, rightLine1, rightMenuItem1, optionNames, optionActions);
        PrepareSelectionEntry(1, rightLine2, rightMenuItem2, optionNames, optionActions);
        PrepareSelectionEntry(2, rightLine3, rightMenuItem3, optionNames, optionActions);
        PrepareSelectionEntry(3, rightLine4, rightMenuItem4, optionNames, optionActions);
        PrepareSelectionEntry(4, rightLine5, rightMenuItem5, optionNames, optionActions);
        PlayPreparedTransition();
    }

    public void SetSealText(string seal)
    {
        PlaySingleEntryTransition(rightLine2, rightMenuItem2, $"Seal: {seal ?? string.Empty}");
    }

    public void SetPlayerCount(int currentPlayers, int maxPlayers)
    {
        PlaySingleEntryTransition(rightLine3, rightMenuItem3, $"Players: {currentPlayers} / {maxPlayers}");
    }

    public void SetPlayerNames(IReadOnlyList<string> playerNames)
    {
        string names = playerNames == null || playerNames.Count == 0
            ? string.Empty
            : string.Join("\n", playerNames);
        PlaySingleEntryTransition(rightLine4, rightMenuItem4, names);
    }

    private void PrepareMainPage(List<TMP_Text> texts, List<string> targets)
    {
        PrepareEntry(texts, targets, rightTitle, null, string.Empty, null);
        PrepareEntry(texts, targets, rightLine1, rightMenuItem1, "Leaderboard",
            bookMenuController != null ? bookMenuController.OpenLeaderboard : null);
        PrepareEntry(texts, targets, rightLine2, rightMenuItem2, "Discord",
            bookMenuController != null ? bookMenuController.OpenDiscord : null);
        PrepareEntry(texts, targets, rightLine3, rightMenuItem3, string.Empty, null);
        PrepareEntry(texts, targets, rightLine4, rightMenuItem4, string.Empty, null);
        PrepareEntry(texts, targets, rightLine5, rightMenuItem5, string.Empty, null);
    }

    private void PrepareHostPage(List<TMP_Text> texts, List<string> targets)
    {
        RitualSealService service = RitualSealService.Instance;
        if (service != null && service.IsHostingRitual)
        {
            int playerCount = NetworkPlayer.CircleMemberCount;
            PrepareEntry(texts, targets, rightTitle, null, string.Empty, null);
            PrepareEntry(texts, targets, rightLine1, rightMenuItem1, $"Seal: {service.ActiveSeal}",
                bookMenuController != null ? bookMenuController.EditHostedSeal : null);
            PrepareEntry(texts, targets, rightLine2, rightMenuItem2, GetSealReplacementStatus(service), null);
            PrepareEntry(
                texts,
                targets,
                rightLine3,
                rightMenuItem3,
                $"{playerCount} / {NetworkPlayer.MaximumCircleMembers}",
                null);
            PrepareEntry(texts, targets, rightLine4, rightMenuItem4, GetHostWaitingMessage(playerCount), null);
            PrepareEntry(texts, targets, rightLine5, rightMenuItem5, string.Empty, null);
            return;
        }

        PrepareClearPage(texts, targets);
    }

    private static string GetHostWaitingMessage(int playerCount)
    {
        if (playerCount >= NetworkPlayer.MaximumCircleMembers)
        {
            return "The circle is complete.";
        }

        if (playerCount == 7)
        {
            return "One priest remains...";
        }

        if (playerCount >= 4)
        {
            return "More priests are gathering...";
        }

        if (playerCount >= 2)
        {
            return "The circle begins to form...";
        }

        return "Waiting for other priests...";
    }

    private static string GetSealReplacementStatus(RitualSealService service)
    {
        return service != null &&
               (service.IsReplacingSeal || service.StatusMessage == "Seal already used")
            ? service.StatusMessage
            : string.Empty;
    }

    private void PrepareJoinSealPage(List<TMP_Text> texts, List<string> targets)
    {
        RitualSealService service = RitualSealService.Instance;
        string seal = bookMenuController != null ? bookMenuController.EnteredSeal : string.Empty;
        bool canSubmit = bookMenuController != null && bookMenuController.CanSubmitSeal;
        string status = GetJoinStatus(service, seal, canSubmit);

        PrepareEntry(texts, targets, rightTitle, null, string.Empty, null);
        PrepareEntry(texts, targets, rightLine1, rightMenuItem1, "Seal", null);
        PrepareEntry(texts, targets, rightLine2, rightMenuItem2, FormatSealField(seal), null);
        PrepareEntry(
            texts,
            targets,
            rightLine3,
            rightMenuItem3,
            "Validate Seal",
            bookMenuController != null ? bookMenuController.SubmitSeal : null);
        PrepareEntry(texts, targets, rightLine4, rightMenuItem4, status, null);
        PrepareEntry(texts, targets, rightLine5, rightMenuItem5, string.Empty, null);
    }

    private void ConfigureJoinSealInteractions()
    {
        bool canSubmit = bookMenuController != null && bookMenuController.CanSubmitSeal;

        if (rightMenuItem1 != null)
        {
            rightMenuItem1.SetOnClickAction(null);
            rightMenuItem1.SetInteractionEnabled(false);
        }

        if (rightMenuItem2 != null)
        {
            rightMenuItem2.SetOnClickAction(null);
            rightMenuItem2.SetInteractionEnabled(true);
        }

        if (rightMenuItem3 != null)
        {
            rightMenuItem3.SetOnClickAction(
                bookMenuController != null ? bookMenuController.SubmitSeal : null);
            rightMenuItem3.SetInteractionEnabled(canSubmit);
        }

        if (rightMenuItem4 != null)
        {
            rightMenuItem4.SetOnClickAction(null);
            rightMenuItem4.SetInteractionEnabled(false);
        }

        if (rightMenuItem5 != null)
        {
            rightMenuItem5.SetOnClickAction(null);
            rightMenuItem5.SetInteractionEnabled(false);
        }

        UpdateValidateButtonColor(canSubmit);
    }

    private static void ApplyTextImmediately(TMP_Text textEntry, string value)
    {
        if (textEntry == null)
        {
            return;
        }

        textEntry.text = value ?? string.Empty;
        textEntry.maxVisibleCharacters = int.MaxValue;
    }

    private static string FormatSealField(string seal)
    {
        string normalizedSeal = RitualSealService.NormalizeSeal(seal);
        return $"[ {normalizedSeal.PadRight(4, '•')} ]";
    }

    private static string GetJoinStatus(RitualSealService service, string seal, bool canSubmit)
    {
        if (service != null && service.JoinStatus == RitualJoinStatus.Joining)
        {
            return "Joining ritual...";
        }

        if (service != null && service.JoinStatus == RitualJoinStatus.Joined)
        {
            return "Ritual joined.";
        }

        if (service != null && service.JoinStatus == RitualJoinStatus.CantJoin)
        {
            return AddPeriod(service.JoinFailureReason);
        }

        return canSubmit && RitualSealService.NormalizeSeal(seal).Length == 4
            ? "Ready to join."
            : "Enter a ritual seal.";
    }

    private static string AddPeriod(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Connection rejected.";
        }

        return message.EndsWith(".") ? message : $"{message}.";
    }

    private void UpdateValidateButtonColor(bool enabled)
    {
        if (rightLine3 == null)
        {
            return;
        }

        Color targetColor = enabled ? validateButtonColor : disabledButtonColor;
        if (!enabled)
        {
            if (buttonFadeCoroutine != null)
            {
                StopCoroutine(buttonFadeCoroutine);
                buttonFadeCoroutine = null;
            }

            rightLine3.color = targetColor;
        }
        else if (!validateButtonWasEnabled)
        {
            if (buttonFadeCoroutine != null)
            {
                StopCoroutine(buttonFadeCoroutine);
            }

            rightLine3.color = disabledButtonColor;
            buttonFadeCoroutine = StartCoroutine(
                FadeValidateButton(disabledButtonColor, targetColor));
        }
        else
        {
            rightLine3.color = targetColor;
        }

        validateButtonWasEnabled = enabled;
    }

    private IEnumerator FadeValidateButton(Color from, Color to)
    {
        float elapsed = 0f;
        while (elapsed < buttonFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            rightLine3.color = Color.Lerp(from, to, Mathf.Clamp01(elapsed / buttonFadeDuration));
            yield return null;
        }

        rightLine3.color = to;
        buttonFadeCoroutine = null;
    }

    private void RestoreValidateSealVisualState()
    {
        if (buttonFadeCoroutine != null)
        {
            StopCoroutine(buttonFadeCoroutine);
            buttonFadeCoroutine = null;
        }

        if (rightLine3 != null)
        {
            rightLine3.color = validateButtonColor;
            if (validateButtonMaterial != null)
            {
                rightLine3.fontSharedMaterial = validateButtonMaterial;
            }
        }

        validateButtonWasEnabled = false;
    }

    private static bool IsJoinState(BookState state)
    {
        return state == BookState.JoinMenu || state == BookState.JoinSealEntry;
    }

    private void PrepareClearPage(List<TMP_Text> texts, List<string> targets)
    {
        PrepareEntry(texts, targets, rightTitle, null, string.Empty, null);
        PrepareEntry(texts, targets, rightLine1, rightMenuItem1, string.Empty, null);
        PrepareEntry(texts, targets, rightLine2, rightMenuItem2, string.Empty, null);
        PrepareEntry(texts, targets, rightLine3, rightMenuItem3, string.Empty, null);
        PrepareEntry(texts, targets, rightLine4, rightMenuItem4, string.Empty, null);
        PrepareEntry(texts, targets, rightLine5, rightMenuItem5, string.Empty, null);
    }

    private void PrepareContextPage(
        List<TMP_Text> texts,
        List<string> targets,
        string titleText,
        string line1Text,
        string line2Text,
        string line3Text,
        string line4Text,
        UnityAction showCharacterAction)
    {
        PrepareEntry(texts, targets, rightTitle, null, titleText, null);
        PrepareEntry(texts, targets, rightLine1, rightMenuItem1, line1Text, null);
        PrepareEntry(texts, targets, rightLine2, rightMenuItem2, line2Text, null);
        PrepareEntry(texts, targets, rightLine3, rightMenuItem3, line3Text, null);
        PrepareEntry(texts, targets, rightLine4, rightMenuItem4, line4Text, null);
        PrepareEntry(texts, targets, rightLine5, rightMenuItem5, "Show Character", showCharacterAction);
    }

    private static void PrepareEntry(
        List<TMP_Text> texts,
        List<string> targets,
        TMP_Text textEntry,
        BookMenuItem menuItem,
        string value,
        UnityAction action)
    {
        texts.Add(textEntry);
        targets.Add(value ?? string.Empty);

        if (menuItem != null)
        {
            menuItem.SetOnClickAction(action);
            menuItem.SetInteractionEnabled(false);
        }
    }

    private void PlayPreparedTransition()
    {
        if (textTransitionController != null)
        {
            textTransitionController.PlayTransition(transitionTexts, transitionTargets, CompletePreparedTransition);
            return;
        }

        ApplyTargetsImmediately(transitionTexts, transitionTargets);
        CompletePreparedTransition();
    }

    private void PlaySingleEntryTransition(TMP_Text textEntry, BookMenuItem menuItem, string value)
    {
        transitionTexts.Clear();
        transitionTargets.Clear();
        PrepareEntry(transitionTexts, transitionTargets, textEntry, menuItem, value, null);
        PlayPreparedTransition();
    }

    private void PrepareSelectionEntry(
        int index,
        TMP_Text textEntry,
        BookMenuItem menuItem,
        IReadOnlyList<string> optionNames,
        IReadOnlyList<UnityAction> optionActions)
    {
        string optionName = optionNames != null && index < optionNames.Count
            ? optionNames[index]
            : string.Empty;
        UnityAction optionAction = optionActions != null && index < optionActions.Count
            ? optionActions[index]
            : null;

        PrepareEntry(
            transitionTexts,
            transitionTargets,
            textEntry,
            menuItem,
            optionName,
            optionAction);
    }

    private static void ApplyTargetsImmediately(List<TMP_Text> texts, List<string> targets)
    {
        for (int i = 0; i < texts.Count; i++)
        {
            if (texts[i] != null)
            {
                texts[i].text = targets[i];
                texts[i].maxVisibleCharacters = int.MaxValue;
            }
        }
    }

    private static void SetMenuItemInteraction(BookMenuItem menuItem, TMP_Text textEntry)
    {
        if (menuItem == null)
        {
            return;
        }

        menuItem.RefreshVisualBaseline();
        menuItem.SetInteractionEnabled(textEntry != null && !string.IsNullOrEmpty(textEntry.text));
    }

    private static void RefreshInteraction(
        BookMenuItem menuItem,
        UnityAction action,
        bool enabled)
    {
        if (menuItem == null)
        {
            return;
        }

        menuItem.SetOnClickAction(action);
        if (menuItem.InteractionEnabled != enabled)
        {
            menuItem.SetInteractionEnabled(enabled);
        }
    }

    private void ClearRightPageActions()
    {
        if (rightMenuItem1 != null) rightMenuItem1.SetOnClickAction(null);
        if (rightMenuItem2 != null) rightMenuItem2.SetOnClickAction(null);
        if (rightMenuItem3 != null) rightMenuItem3.SetOnClickAction(null);
        if (rightMenuItem4 != null) rightMenuItem4.SetOnClickAction(null);
        if (rightMenuItem5 != null) rightMenuItem5.SetOnClickAction(null);
    }
}
