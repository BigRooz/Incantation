using TMPro;
using UnityEngine;
using UnityEngine.Events;

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

    [Header("Right Page")]
    [SerializeField] private BookRightPageController rightPageController;

    [Header("Character Page")]
    [SerializeField] private CharacterBookPageController characterBookPageController;

    private void Start()
    {
        SetState(BookState.MainMenu);
    }

    public void SetState(BookState state)
    {
        switch (state)
        {
            case BookState.MainMenu:
                ApplyPage(
                    "INCANTATION",
                    "Play", bookMenuController != null ? bookMenuController.OpenPlayMenu : null,
                    "Character", bookMenuController != null ? bookMenuController.OpenCharacter : null,
                    "Options", bookMenuController != null ? bookMenuController.OpenOptions : null,
                    "Quit", bookMenuController != null ? bookMenuController.QuitGame : null);
                break;

            case BookState.PlayMenu:
                ApplyPage(
                    "THE RITUAL",
                    "Create Ritual", bookMenuController != null ? bookMenuController.OpenHostMenu : null,
                    "Join Ritual", bookMenuController != null ? bookMenuController.OpenJoinMenu : null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToMainMenu : null,
                    string.Empty, null);
                break;

            case BookState.HostMenu:
                ApplyPage(
                    "THE CIRCLE",
                    "Start Ritual", bookMenuController != null ? bookMenuController.StartRitualFromBook : null,
                    "Share Ritual", null,
                    "Take Your Seat", bookMenuController != null ? bookMenuController.OpenLobby : null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToPlayMenu : null);
                break;

            case BookState.JoinMenu:
                ApplyPage(
                    "JOIN RITUAL",
                    "Enter Seal", null,
                    "Take Your Seat", null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToPlayMenu : null,
                    string.Empty, null);
                break;

            case BookState.CharacterMenu:
                ApplyPage(
                    "CHARACTER",
                    "Color", characterBookPageController != null ? characterBookPageController.ShowColors : null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToMainMenu : null,
                    string.Empty, null,
                    string.Empty, null);
                break;

            case BookState.OptionsMenu:
                ApplyPage(
                    "OPTIONS",
                    "Audio", null,
                    "Graphics", null,
                    "Controls", null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToMainMenu : null);
                break;

            default:
                Debug.LogWarning($"{nameof(BookStateController)} cannot display unsupported state {state}.", this);
                break;
        }

        if (rightPageController != null)
        {
            rightPageController.RefreshForState(state);
        }

        if (state == BookState.CharacterMenu && characterBookPageController != null)
        {
            characterBookPageController.ResetCharacterPage();
        }
    }

    private void ApplyPage(
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
        SetText(title, titleText);
        SetLine(line1, menuItem1, line1Text, line1Action);
        SetLine(line2, menuItem2, line2Text, line2Action);
        SetLine(line3, menuItem3, line3Text, line3Action);
        SetLine(line4, menuItem4, line4Text, line4Action);
        SetLine(line5, menuItem5, string.Empty, null);
    }

    private void ApplyFiveLinePage(
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
        SetText(title, titleText);
        SetLine(line1, menuItem1, line1Text, line1Action);
        SetLine(line2, menuItem2, line2Text, line2Action);
        SetLine(line3, menuItem3, line3Text, line3Action);
        SetLine(line4, menuItem4, line4Text, line4Action);
        SetLine(line5, menuItem5, line5Text, line5Action);
    }

    private static void SetLine(TMP_Text line, BookMenuItem menuItem, string text, UnityAction action)
    {
        SetText(line, text);

        if (menuItem != null)
        {
            menuItem.SetOnClickAction(action);

            if (string.IsNullOrEmpty(text))
            {
                menuItem.SetInteractionEnabled(false);
            }
            else
            {
                menuItem.RefreshVisualBaseline();
                menuItem.SetInteractionEnabled(true);
            }
        }
    }

    private static void SetText(TMP_Text textEntry, string value)
    {
        if (textEntry != null)
        {
            textEntry.text = value;
        }
    }
}
