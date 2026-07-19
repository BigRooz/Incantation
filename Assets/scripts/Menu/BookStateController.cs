using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Owns the current Living Book menu state and prepares its left-page content and actions.
/// It delegates all visual rewriting to BookTextTransitionController and coordinates the right page in one transition.
/// </summary>
public sealed class BookStateController : MonoBehaviour
{
    [Header("Existing Book Text")]
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text line1;
    [SerializeField] private TMP_Text line2;
    [SerializeField] private TMP_Text line3;
    [SerializeField] private TMP_Text line4;
    [SerializeField] private TMP_Text line5;

    [Header("Existing Book Menu Items")]
    [SerializeField] private BookMenuItem menuItem1;
    [SerializeField] private BookMenuItem menuItem2;
    [SerializeField] private BookMenuItem menuItem3;
    [SerializeField] private BookMenuItem menuItem4;
    [SerializeField] private BookMenuItem menuItem5;

    [Header("Menu Actions")]
    [SerializeField] private BookMenuController bookMenuController;

    [Header("Page Presentation")]
    [SerializeField] private BookTextTransitionController textTransitionController;
    [SerializeField] private BookRightPageController rightPageController;

    [Header("Character Page")]
    [SerializeField] private CharacterBookPageController characterBookPageController;

    [Header("Voice Page")]
    [SerializeField] private VoiceBookPageController voiceBookPageController;

    private readonly List<TMP_Text> transitionTexts = new List<TMP_Text>();
    private readonly List<string> transitionTargets = new List<string>();
    private BookState preparedState;

    private void Start()
    {
        SetState(BookState.MainMenu);
    }

    public void SetState(BookState state)
    {
        preparedState = state;
        if (voiceBookPageController != null)
        {
            voiceBookPageController.SetPageOpen(false);
        }

        transitionTexts.Clear();
        transitionTargets.Clear();

        switch (state)
        {
            case BookState.MainMenu:
                PreparePage(
                    "INCANTATION",
                    "Play", bookMenuController != null ? bookMenuController.OpenPlayMenu : null,
                    "Character", bookMenuController != null ? bookMenuController.OpenCharacter : null,
                    "Options", bookMenuController != null ? bookMenuController.OpenOptions : null,
                    "Quit", bookMenuController != null ? bookMenuController.QuitGame : null);
                break;

            case BookState.PlayMenu:
                PreparePage(
                    "THE RITUAL",
                    "Create Ritual", bookMenuController != null ? bookMenuController.OpenHostMenu : null,
                    "Join Ritual", bookMenuController != null ? bookMenuController.OpenJoinMenu : null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToMainMenu : null,
                    string.Empty, null);
                break;

            case BookState.HostMenu:
                PreparePage(
                    "THE CIRCLE",
                    "Start Ritual", bookMenuController != null ? bookMenuController.StartRitualFromBook : null,
                    "Share Ritual", null,
                    "Take Your Seat", bookMenuController != null ? bookMenuController.OpenLobby : null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToPlayMenu : null);
                break;

            case BookState.JoinMenu:
                PreparePage(
                    "JOIN RITUAL",
                    "Enter Seal", null,
                    "Take Your Seat", null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToPlayMenu : null,
                    string.Empty, null);
                break;

            case BookState.CharacterMenu:
                PreparePage(
                    "CHARACTER",
                    "Color", null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToMainMenu : null,
                    string.Empty, null,
                    string.Empty, null);
                break;

            case BookState.OptionsMenu:
                PreparePage(
                    "OPTIONS",
                    "Voice", bookMenuController != null ? bookMenuController.OpenVoiceOptions : null,
                    "Graphics", null,
                    "Controls", null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToMainMenu : null);
                break;

            case BookState.VoiceMenu:
                PreparePage(
                    "VOICE",
                    "Input Device", voiceBookPageController != null ? voiceBookPageController.ShowInputDevice : null,
                    "Output Device", voiceBookPageController != null ? voiceBookPageController.ShowOutputDevice : null,
                    "Microphone Test", voiceBookPageController != null ? voiceBookPageController.ShowMicrophoneTest : null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToOptions : null);
                break;

            default:
                Debug.LogWarning($"{nameof(BookStateController)} cannot display unsupported state {state}.", this);
                return;
        }

        if (state == BookState.CharacterMenu && menuItem1 != null)
        {
            menuItem1.SetOnClickAction(
                characterBookPageController != null ? characterBookPageController.ShowColors : null);
        }

        if (rightPageController != null)
        {
            rightPageController.PrepareForState(state, transitionTexts, transitionTargets);
        }

        DisableLeftPageInteraction();

        if (textTransitionController != null)
        {
            textTransitionController.PlayTransition(transitionTexts, transitionTargets, CompletePageTransition);
        }
        else
        {
            ApplyTargetsImmediately();
            CompletePageTransition();
        }
    }

    private void PreparePage(
        string titleText,
        string line1Text,
        UnityAction line1Action,
        string line2Text,
        UnityAction line2Action,
        string line3Text,
        UnityAction line3Action,
        string line4Text,
        UnityAction line4Action)
    {
        PreparePage(
            titleText,
            line1Text, line1Action,
            line2Text, line2Action,
            line3Text, line3Action,
            line4Text, line4Action,
            string.Empty, null);
    }

    private void PreparePage(
        string titleText,
        string line1Text,
        UnityAction line1Action,
        string line2Text,
        UnityAction line2Action,
        string line3Text,
        UnityAction line3Action,
        string line4Text,
        UnityAction line4Action,
        string line5Text,
        UnityAction line5Action)
    {
        PrepareEntry(title, null, titleText, null);
        PrepareEntry(line1, menuItem1, line1Text, line1Action);
        PrepareEntry(line2, menuItem2, line2Text, line2Action);
        PrepareEntry(line3, menuItem3, line3Text, line3Action);
        PrepareEntry(line4, menuItem4, line4Text, line4Action);
        PrepareEntry(line5, menuItem5, line5Text, line5Action);
    }

    private void PrepareEntry(TMP_Text textEntry, BookMenuItem menuItem, string value, UnityAction action)
    {
        transitionTexts.Add(textEntry);
        transitionTargets.Add(value ?? string.Empty);

        if (menuItem != null)
        {
            menuItem.SetOnClickAction(action);
            menuItem.SetInteractionEnabled(false);
        }
    }

    private void DisableLeftPageInteraction()
    {
        SetLeftInteraction(false);
    }

    private void CompletePageTransition()
    {
        SetMenuItemInteraction(menuItem1, line1);
        SetMenuItemInteraction(menuItem2, line2);
        SetMenuItemInteraction(menuItem3, line3);
        SetMenuItemInteraction(menuItem4, line4);
        SetMenuItemInteraction(menuItem5, line5);

        if (rightPageController != null)
        {
            rightPageController.CompletePreparedTransition();
        }

        if (voiceBookPageController != null)
        {
            voiceBookPageController.SetPageOpen(preparedState == BookState.VoiceMenu);
        }
    }

    private void SetLeftInteraction(bool enabled)
    {
        if (menuItem1 != null) menuItem1.SetInteractionEnabled(enabled);
        if (menuItem2 != null) menuItem2.SetInteractionEnabled(enabled);
        if (menuItem3 != null) menuItem3.SetInteractionEnabled(enabled);
        if (menuItem4 != null) menuItem4.SetInteractionEnabled(enabled);
        if (menuItem5 != null) menuItem5.SetInteractionEnabled(enabled);
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

    private void ApplyTargetsImmediately()
    {
        for (int i = 0; i < transitionTexts.Count; i++)
        {
            if (transitionTexts[i] != null)
            {
                transitionTexts[i].text = transitionTargets[i];
                transitionTexts[i].maxVisibleCharacters = int.MaxValue;
            }
        }
    }
}
