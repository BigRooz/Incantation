using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

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

    public void RefreshForState(BookState state)
    {
        switch (state)
        {
            case BookState.MainMenu:
                ShowMainPage();
                break;

            case BookState.HostMenu:
                ShowHostPage();
                break;

            case BookState.JoinMenu:
                ShowJoinPage();
                break;

            default:
                ClearRightPage();
                break;
        }
    }

    public void ClearRightPage()
    {
        SetEntry(rightTitle, null, string.Empty, null);
        SetEntry(rightLine1, rightMenuItem1, string.Empty, null);
        SetEntry(rightLine2, rightMenuItem2, string.Empty, null);
        SetEntry(rightLine3, rightMenuItem3, string.Empty, null);
        SetEntry(rightLine4, rightMenuItem4, string.Empty, null);
        SetEntry(rightLine5, rightMenuItem5, string.Empty, null);
    }

    public void ShowContextPage(
        string titleText,
        string line1Text,
        string line2Text,
        string line3Text,
        string line4Text,
        UnityAction showCharacterAction)
    {
        SetEntry(rightTitle, null, titleText, null);
        SetEntry(rightLine1, rightMenuItem1, line1Text, null);
        SetEntry(rightLine2, rightMenuItem2, line2Text, null);
        SetEntry(rightLine3, rightMenuItem3, line3Text, null);
        SetEntry(rightLine4, rightMenuItem4, line4Text, null);
        SetEntry(rightLine5, rightMenuItem5, "Show Character", showCharacterAction);
    }

    public void SetSealText(string seal)
    {
        SetText(rightLine2, $"Seal: {seal ?? string.Empty}");
    }

    public void SetPlayerCount(int currentPlayers, int maxPlayers)
    {
        SetText(rightLine3, $"Players: {currentPlayers} / {maxPlayers}");
    }

    public void SetPlayerNames(IReadOnlyList<string> playerNames)
    {
        if (playerNames == null || playerNames.Count == 0)
        {
            SetText(rightLine4, string.Empty);
            return;
        }

        SetText(rightLine4, string.Join("\n", playerNames));
    }

    private void ShowMainPage()
    {
        SetEntry(rightTitle, null, string.Empty, null);
        SetEntry(
            rightLine1,
            rightMenuItem1,
            "Leaderboard",
            bookMenuController != null ? bookMenuController.OpenLeaderboard : null);
        SetEntry(
            rightLine2,
            rightMenuItem2,
            "Discord",
            bookMenuController != null ? bookMenuController.OpenDiscord : null);
        SetEntry(rightLine3, rightMenuItem3, string.Empty, null);
        SetEntry(rightLine4, rightMenuItem4, string.Empty, null);
        SetEntry(rightLine5, rightMenuItem5, string.Empty, null);
    }

    private void ShowHostPage()
    {
        SetEntry(rightTitle, null, "THE CIRCLE", null);
        SetEntry(rightLine1, rightMenuItem1, "Invite a Mage", LogInvitePlaceholder);
        SetEntry(rightLine2, rightMenuItem2, "Seal: ----", null);
        SetEntry(rightLine3, rightMenuItem3, "Players: 1 / 8", null);
        SetEntry(rightLine4, rightMenuItem4, "Host Name", null);
        SetEntry(
            rightLine5,
            rightMenuItem5,
            "Take Your Seat",
            bookMenuController != null ? bookMenuController.OpenLobby : null);
    }

    private void ShowJoinPage()
    {
        SetEntry(rightTitle, null, "JOIN THE CIRCLE", null);
        SetEntry(rightLine1, rightMenuItem1, "Enter Seal", null);
        SetEntry(rightLine2, rightMenuItem2, "Seal: ----", null);
        SetEntry(rightLine3, rightMenuItem3, "Players: -- / 8", null);
        SetEntry(rightLine4, rightMenuItem4, "Waiting...", null);
        SetEntry(rightLine5, rightMenuItem5, "Take Your Seat", null);
    }

    private void LogInvitePlaceholder()
    {
        Debug.Log("Invite a Mage is not implemented yet.", this);
    }

    private static void SetEntry(
        TMP_Text textEntry,
        BookMenuItem menuItem,
        string value,
        UnityAction action)
    {
        SetText(textEntry, value);

        if (menuItem != null)
        {
            menuItem.SetOnClickAction(action);

            if (string.IsNullOrEmpty(value))
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
