using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

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

    [Header("Voice Page")]
    [SerializeField] private VoiceBookPageController voiceBookPageController;

    private readonly List<TMP_Text> transitionTexts = new List<TMP_Text>();
    private readonly List<string> transitionTargets = new List<string>();

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
        switch (state)
        {
            case BookState.MainMenu:
                PrepareMainPage(texts, targetStrings);
                break;
            case BookState.HostMenu:
                PrepareHostPage(texts, targetStrings);
                break;
            case BookState.JoinMenu:
                PrepareJoinPage(texts, targetStrings);
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
        SetMenuItemInteraction(rightMenuItem1, rightLine1);
        SetMenuItemInteraction(rightMenuItem2, rightLine2);
        SetMenuItemInteraction(rightMenuItem3, rightLine3);
        SetMenuItemInteraction(rightMenuItem4, rightLine4);
        SetMenuItemInteraction(rightMenuItem5, rightLine5);
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
        PrepareEntry(texts, targets, rightTitle, null, "THE CIRCLE", null);
        PrepareEntry(texts, targets, rightLine1, rightMenuItem1, "Invite a Mage", LogInvitePlaceholder);
        PrepareEntry(texts, targets, rightLine2, rightMenuItem2, "Seal: ----", null);
        PrepareEntry(texts, targets, rightLine3, rightMenuItem3, "Players: 1 / 8", null);
        PrepareEntry(texts, targets, rightLine4, rightMenuItem4, "Host Name", null);
        PrepareEntry(texts, targets, rightLine5, rightMenuItem5, string.Empty, null);
    }

    private void PrepareJoinPage(List<TMP_Text> texts, List<string> targets)
    {
        PrepareEntry(texts, targets, rightTitle, null, "JOIN THE CIRCLE", null);
        PrepareEntry(texts, targets, rightLine1, rightMenuItem1, string.Empty, null);
        PrepareEntry(texts, targets, rightLine2, rightMenuItem2, "Seal: ----", null);
        PrepareEntry(texts, targets, rightLine3, rightMenuItem3, "Players: -- / 8", null);
        PrepareEntry(texts, targets, rightLine4, rightMenuItem4, "Waiting...", null);
        PrepareEntry(texts, targets, rightLine5, rightMenuItem5, string.Empty, null);
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

    private void LogInvitePlaceholder()
    {
        Debug.Log("Invite a Mage is not implemented yet.", this);
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
