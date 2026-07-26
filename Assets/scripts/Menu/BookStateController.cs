using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Incantation.Networking;

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

    [Header("Lobby")]
    [SerializeField] private LobbyPlayerStateController lobbyPlayerStateController;

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
    private bool hasStarted;
    private bool hasPreparedPage;
    private bool hostCreationTransitionActive;
    private bool hostCreationAwaitingResult;
    private int lastRenderedCircleMemberCount = -1;
    private int lastRenderedReadyCount = -1;

    public BookState CurrentState => preparedState;
    public bool IsHostCreationTransitionActive => hostCreationTransitionActive;

    private void OnEnable()
    {
        if (lobbyPlayerStateController != null)
        {
            lobbyPlayerStateController.StateChanged += HandleLobbyPlayerStateChanged;
        }

        if (RitualSealService.Instance != null)
        {
            RitualSealService.Instance.Changed += HandleRitualSealChanged;
        }

        NetworkPlayer.CircleRosterChanged += HandleCircleRosterChanged;

        if (hasStarted)
        {
            RefreshFromCurrentCircleState();
        }
    }

    private void OnDisable()
    {
        if (lobbyPlayerStateController != null)
        {
            lobbyPlayerStateController.StateChanged -= HandleLobbyPlayerStateChanged;
        }

        if (RitualSealService.Instance != null)
        {
            RitualSealService.Instance.Changed -= HandleRitualSealChanged;
        }

        NetworkPlayer.CircleRosterChanged -= HandleCircleRosterChanged;
    }

    private void Start()
    {
        hasStarted = true;
        RitualSealService sealService = RitualSealService.Instance;
        if (ShouldDisplayJoinedCircle(sealService))
        {
            ChangePage(BookState.Lobby);
        }
        else if (sealService != null && sealService.IsHostingRitual)
        {
            ShowHostLobbySilently();
        }
        else
        {
            ChangePage(BookState.MainMenu);
        }
    }

    public void ChangePage(BookState state)
    {
        if (state == BookState.RitualCreated)
        {
            state = BookState.HostMenu;
        }

        if (state == BookState.HostMenu &&
            (RitualSealService.Instance == null || !RitualSealService.Instance.IsHostingRitual))
        {
            state = BookState.PlayMenu;
        }

        if (hasPreparedPage && preparedState == state)
        {
            RefreshCurrentPageContent();
            return;
        }

        preparedState = state;
        if (state == BookState.Lobby)
        {
            lastRenderedCircleMemberCount = NetworkPlayer.CircleMemberCount;
            lastRenderedReadyCount = NetworkPlayer.ReadyCircleMemberCount;
        }

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
                    "Create Ritual", bookMenuController != null ? bookMenuController.CreateNetworkRitual : null,
                    "Join Ritual", bookMenuController != null ? bookMenuController.OpenJoinMenu : null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToMainMenu : null,
                    string.Empty, null);
                break;

            case BookState.HostMenu:
                PrepareHostPage();
                break;

            case BookState.JoinMenu:
            case BookState.JoinSealEntry:
                PreparePage(
                    "JOIN RITUAL",
                    string.Empty, null,
                    string.Empty, null,
                    string.Empty, null,
                    "Back", bookMenuController != null ? bookMenuController.CancelJoinRitual : null);
                break;

            case BookState.Lobby:
                PrepareLobbyPage(lobbyPlayerStateController != null
                    ? lobbyPlayerStateController.CurrentState
                    : LobbyPlayerState.NotSeated);
                break;

            case BookState.CharacterMenu:
                PreparePage(
                    "CHARACTER",
                    "Skin", characterBookPageController != null ? characterBookPageController.ShowSkin : null,
                    "Hair", characterBookPageController != null ? characterBookPageController.ShowHair : null,
                    "Beard", characterBookPageController != null ? characterBookPageController.ShowBeards : null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnFromCharacterView : null);
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

        hasPreparedPage = true;

        if (rightPageController != null && state != BookState.Lobby)
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

    public bool BeginHostCreationTransition(System.Action startHostCreation)
    {
        if (hostCreationTransitionActive || preparedState != BookState.PlayMenu)
        {
            return false;
        }

        hostCreationTransitionActive = true;
        hostCreationAwaitingResult = true;
        preparedState = BookState.HostMenu;
        PreparePendingHostCreationDestination(string.Empty);
        hasPreparedPage = true;
        DisableLeftPageInteraction();

        if (textTransitionController != null)
        {
            textTransitionController.PlayTransition(
                transitionTexts,
                transitionTargets,
                CompleteHostCreationTransition,
                () => StartHostCreationWhileHidden(startHostCreation));
        }
        else
        {
            StartHostCreationWhileHidden(startHostCreation);
            ApplyTargetsImmediately();
            CompleteHostCreationTransition();
        }

        return true;
    }

    private void PrepareLobbyPage(LobbyPlayerState lobbyPlayerState)
    {
        int readyPlayerCount = NetworkPlayer.ReadyCircleMemberCount;
        bool isLocalPlayerReady =
            NetworkPlayer.LocalPlayer != null &&
            NetworkPlayer.LocalPlayer.IsCircleMember &&
            NetworkPlayer.LocalPlayer.IsReady;

        if (lobbyPlayerState == LobbyPlayerState.Ready)
        {
            lobbyPlayerState = LobbyPlayerState.Seated;
        }

        string rightLine1Text;
        string rightLine2Text;
        string rightLine3Text;
        UnityAction rightLine3Action = null;

        switch (lobbyPlayerState)
        {
            case LobbyPlayerState.NotSeated:
                PreparePage(
                    "THE CIRCLE",
                    "Priest Name", bookMenuController != null ? bookMenuController.EditPriestName : null,
                    "Character", bookMenuController != null ? bookMenuController.OpenCharacter : null,
                    "Take My Seat", lobbyPlayerStateController != null ? TakeLobbySeat : null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToActiveHostLobby : null);
                rightLine1Text = NetworkPlayer.LocalPlayer != null
                    ? NetworkPlayer.LocalPlayer.PriestName
                    : "Priest";
                rightLine2Text =
                    $"Players ({NetworkPlayer.CircleMemberCount} / {NetworkPlayer.MaximumCircleMembers})";
                rightLine3Text = "Invite a Priest";
                rightLine3Action = bookMenuController != null ? bookMenuController.InvitePriest : null;
                break;

            case LobbyPlayerState.Seated:
                PreparePage(
                    "THE CIRCLE",
                    isLocalPlayerReady ? "Unready" : "Ready",
                    NetworkPlayer.LocalPlayer != null ? ToggleLocalReady : null,
                    "Leave Seat", lobbyPlayerStateController != null ? LeaveLobbySeat : null,
                    string.Empty, null,
                    "Back", bookMenuController != null ? bookMenuController.ReturnToActiveHostLobby : null);
                rightLine1Text =
                    $"Priests Ready ({readyPlayerCount} / {NetworkPlayer.MaximumCircleMembers})";
                rightLine2Text = "Waiting for Host to Start the Ritual";
                rightLine3Text = string.Empty;
                break;

            default:
                Debug.LogWarning($"{nameof(BookStateController)} cannot display unsupported lobby state {lobbyPlayerState}.", this);
                return;
        }

        if (rightPageController != null)
        {
            rightPageController.PrepareLobbyPage(
                transitionTexts,
                transitionTargets,
                rightLine1Text,
                rightLine2Text,
                rightLine3Text,
                rightLine3Action);
        }
    }

    private void PrepareHostPage()
    {
        PreparePage(
            "CREATE RITUAL",
            "Enter the Circle", bookMenuController != null ? bookMenuController.OpenCirclePage : null,
            "Invite a Priest", bookMenuController != null ? bookMenuController.InvitePriest : null,
            "Quit Ritual", bookMenuController != null ? bookMenuController.QuitRitual : null,
            string.Empty, null);
    }

    private void PrepareHostCreationDestination()
    {
        transitionTexts.Clear();
        transitionTargets.Clear();

        RitualSealService service = RitualSealService.Instance;
        if (service != null && service.IsHostingRitual)
        {
            preparedState = BookState.HostMenu;
            PrepareHostPage();
            rightPageController?.PrepareForState(
                BookState.HostMenu, transitionTexts, transitionTargets);
            return;
        }

        if (service != null && service.IsCreatingRitual)
        {
            PreparePendingHostCreationDestination(service.StatusMessage);
            return;
        }

        preparedState = BookState.PlayMenu;
        PreparePage(
            "THE RITUAL",
            "Create Ritual", bookMenuController != null ? bookMenuController.CreateNetworkRitual : null,
            "Join Ritual", bookMenuController != null ? bookMenuController.OpenJoinMenu : null,
            "Back", bookMenuController != null ? bookMenuController.ReturnToMainMenu : null,
            string.Empty, null);
        rightPageController?.PrepareRitualCreationStatus(
            BookState.PlayMenu,
            service != null ? service.StatusMessage : string.Empty,
            transitionTexts,
            transitionTargets);
    }

    private void PreparePendingHostCreationDestination(string status)
    {
        preparedState = BookState.HostMenu;
        transitionTexts.Clear();
        transitionTargets.Clear();
        PreparePage(
            "CREATE RITUAL",
            "Creating Ritual...", null,
            string.Empty, null,
            string.Empty, null,
            string.Empty, null);
        rightPageController?.PrepareRitualCreationStatus(
            BookState.HostMenu,
            status, transitionTexts, transitionTargets);
    }

    private void StartHostCreationWhileHidden(System.Action startHostCreation)
    {
        startHostCreation?.Invoke();
        PrepareHostCreationDestination();
    }

    private void CompleteHostCreationTransition()
    {
        hostCreationTransitionActive = false;
        CompletePageTransition();

        RitualSealService service = RitualSealService.Instance;
        if (service != null && service.IsHostingRitual)
        {
            hostCreationAwaitingResult = false;
            ShowHostLobbySilently();
        }
        else if (service == null || !service.IsCreatingRitual)
        {
            hostCreationAwaitingResult = false;
            ShowPlayMenuSilently();
        }
    }

    private void TakeLobbySeat()
    {
        if (bookMenuController != null)
        {
            bookMenuController.BeginSeatSelection();
        }
    }

    private void LeaveLobbySeat()
    {
        if (bookMenuController != null)
        {
            bookMenuController.TryLeaveLobbySeat();
        }
    }

    private void ToggleLocalReady()
    {
        NetworkPlayer.LocalPlayer?.RequestToggleReady();
    }

    private void HandleLobbyPlayerStateChanged(
        LobbyPlayerState previousState,
        LobbyPlayerState currentState)
    {
        if (preparedState == BookState.Lobby)
        {
            RefreshCurrentPageContent();
        }
    }

    private void HandleRitualSealChanged()
    {
        if (hostCreationTransitionActive)
        {
            return;
        }

        if (ShouldDisplayJoinedCircle(RitualSealService.Instance))
        {
            TransitionToCircle();
        }
        else if (preparedState == BookState.JoinSealEntry)
        {
            RefreshJoinSealPresentation();
        }
        else if (preparedState == BookState.HostMenu)
        {
            if (RitualSealService.Instance != null && RitualSealService.Instance.IsHostingRitual)
            {
                hostCreationAwaitingResult = false;
                ShowHostLobbySilently();
            }
            else if (RitualSealService.Instance != null &&
                     RitualSealService.Instance.IsCreatingRitual)
            {
                RefreshCurrentPageContent();
            }
            else if (hostCreationAwaitingResult)
            {
                hostCreationAwaitingResult = false;
                ShowPlayMenuSilently();
            }
            else
            {
                ChangePage(BookState.PlayMenu);
            }
        }
        else if (preparedState == BookState.PlayMenu)
        {
            if (RitualSealService.Instance != null && RitualSealService.Instance.IsHostingRitual)
            {
                ShowHostLobbySilently();
            }
            else
            {
                RefreshCurrentPageContent();
            }
        }
    }

    private void HandleCircleRosterChanged()
    {
        RefreshFromCurrentCircleState();
    }

    private void RefreshFromCurrentCircleState()
    {
        if (ShouldDisplayJoinedCircle(RitualSealService.Instance))
        {
            TransitionToCircle();
        }
        else if (preparedState == BookState.Lobby)
        {
            if (NetworkPlayer.IsLocalPlayerCircleMember)
            {
                if (lastRenderedCircleMemberCount != NetworkPlayer.CircleMemberCount ||
                    lastRenderedReadyCount != NetworkPlayer.ReadyCircleMemberCount)
                {
                    RefreshCurrentPageContent();
                }
            }
            else
            {
                Debug.Log("[Book Menu] Local Circle membership ended; returning to the main menu.", this);
                ChangePage(BookState.MainMenu);
            }
        }
        else if (preparedState == BookState.HostMenu &&
                 RitualSealService.Instance != null &&
                 RitualSealService.Instance.IsHostingRitual)
        {
            RefreshCurrentPageContent();
        }
    }

    private void TransitionToCircle()
    {
        if (preparedState == BookState.Lobby)
        {
            if (lastRenderedCircleMemberCount != NetworkPlayer.CircleMemberCount ||
                lastRenderedReadyCount != NetworkPlayer.ReadyCircleMemberCount)
            {
                RefreshCurrentPageContent();
            }

            return;
        }

        Debug.Log("[Book Menu] Transitioning to The Circle.", this);
        ChangePage(BookState.Lobby);
    }

    public void RefreshCurrentPageContent()
    {
        switch (preparedState)
        {
            case BookState.Lobby:
                RefreshLobbyContent();
                break;

            case BookState.JoinMenu:
            case BookState.JoinSealEntry:
                RefreshJoinSealPresentation();
                break;

            case BookState.PlayMenu:
                RefreshPlayMenuContent();
                break;

            case BookState.HostMenu:
                RefreshHostContent();
                break;
        }
    }

    public void ShowPriestNameEditor(
        string value,
        string status,
        UnityAction confirmAction,
        UnityAction cancelAction)
    {
        rightPageController?.ShowPriestNameEditor(value, status, confirmAction, cancelAction);
    }

    public void ShowHostedSealEditor(
        string value,
        string status,
        UnityAction confirmAction,
        UnityAction cancelAction)
    {
        rightPageController?.ShowHostedSealEditor(value, status, confirmAction, cancelAction);
    }

    private void RefreshHostContent()
    {
        RitualSealService service = RitualSealService.Instance;
        if (service == null || !service.IsHostingRitual)
        {
            RefreshLeftEntry(line1, menuItem1, "Creating Ritual...", null);
            RefreshLeftEntry(line2, menuItem2, string.Empty, null);
            RefreshLeftEntry(line3, menuItem3, string.Empty, null);
            RefreshLeftEntry(line4, menuItem4, string.Empty, null);
            RefreshLeftEntry(line5, menuItem5, string.Empty, null);
            rightPageController?.RefreshRitualCreationStatusSilently(
                service != null ? service.StatusMessage : string.Empty);
            return;
        }

        RefreshLeftEntry(line1, menuItem1, "Enter the Circle",
            bookMenuController != null ? bookMenuController.OpenCirclePage : null);
        RefreshLeftEntry(line2, menuItem2, "Invite a Priest",
            bookMenuController != null ? bookMenuController.InvitePriest : null);
        RefreshLeftEntry(line3, menuItem3, "Quit Ritual",
            bookMenuController != null ? bookMenuController.QuitRitual : null);
        RefreshLeftEntry(line4, menuItem4, string.Empty, null);
        RefreshLeftEntry(line5, menuItem5, string.Empty, null);

        rightPageController?.RefreshHostPageSilently();
    }

    private void RefreshPlayMenuContent()
    {
        RitualSealService service = RitualSealService.Instance;
        bool isCreating = service != null && service.IsCreatingRitual;
        RefreshLeftEntry(
            line1,
            menuItem1,
            isCreating ? "Creating Ritual..." : "Create Ritual",
            !isCreating && bookMenuController != null
                ? bookMenuController.CreateNetworkRitual
                : null);
        RefreshLeftEntry(
            line2,
            menuItem2,
            "Join Ritual",
            bookMenuController != null ? bookMenuController.OpenJoinMenu : null);
        RefreshLeftEntry(
            line3,
            menuItem3,
            "Back",
            bookMenuController != null ? bookMenuController.ReturnToMainMenu : null);
        RefreshLeftEntry(line4, menuItem4, string.Empty, null);
        RefreshLeftEntry(line5, menuItem5, string.Empty, null);
        rightPageController?.RefreshRitualCreationStatusSilently(
            service != null ? service.StatusMessage : string.Empty);
    }

    private void ShowPlayMenuSilently()
    {
        preparedState = BookState.PlayMenu;
        hasPreparedPage = true;
        transitionTexts.Clear();
        transitionTargets.Clear();
        voiceBookPageController?.SetPageOpen(false);
        ApplyTextImmediately(title, "THE RITUAL");
        RefreshPlayMenuContent();
    }

    private void ShowHostLobbySilently()
    {
        preparedState = BookState.HostMenu;
        hasPreparedPage = true;
        transitionTexts.Clear();
        transitionTargets.Clear();
        voiceBookPageController?.SetPageOpen(false);
        ApplyTextImmediately(title, "CREATE RITUAL");
        RefreshHostContent();
    }

    private void RefreshLobbyContent()
    {
        LobbyPlayerState lobbyPlayerState = lobbyPlayerStateController != null
            ? lobbyPlayerStateController.CurrentState
            : LobbyPlayerState.NotSeated;
        if (lobbyPlayerState == LobbyPlayerState.Ready)
        {
            lobbyPlayerState = LobbyPlayerState.Seated;
        }

        bool isLocalPlayerReady =
            NetworkPlayer.LocalPlayer != null &&
            NetworkPlayer.LocalPlayer.IsCircleMember &&
            NetworkPlayer.LocalPlayer.IsReady;

        if (lobbyPlayerState == LobbyPlayerState.NotSeated)
        {
            RefreshLeftEntry(line1, menuItem1, "Priest Name",
                bookMenuController != null ? bookMenuController.EditPriestName : null);
            RefreshLeftEntry(line2, menuItem2, "Character",
                bookMenuController != null ? bookMenuController.OpenCharacter : null);
            RefreshLeftEntry(line3, menuItem3, "Take My Seat",
                lobbyPlayerStateController != null ? TakeLobbySeat : null);
            RefreshLeftEntry(line4, menuItem4, "Back",
                bookMenuController != null ? bookMenuController.ReturnToActiveHostLobby : null);

            rightPageController?.RefreshLobbyContent(
                NetworkPlayer.LocalPlayer != null
                    ? NetworkPlayer.LocalPlayer.PriestName
                    : "Priest",
                $"Players ({NetworkPlayer.CircleMemberCount} / {NetworkPlayer.MaximumCircleMembers})",
                "Invite a Priest",
                bookMenuController != null ? bookMenuController.InvitePriest : null);
        }
        else
        {
            RefreshLeftEntry(line1, menuItem1, isLocalPlayerReady ? "Unready" : "Ready",
                NetworkPlayer.LocalPlayer != null ? ToggleLocalReady : null);
            RefreshLeftEntry(line2, menuItem2, "Leave Seat",
                lobbyPlayerStateController != null ? LeaveLobbySeat : null);
            RefreshLeftEntry(line3, menuItem3, string.Empty, null);
            RefreshLeftEntry(line4, menuItem4, "Back",
                bookMenuController != null ? bookMenuController.ReturnToActiveHostLobby : null);

            rightPageController?.RefreshLobbyContent(
                $"Priests Ready ({NetworkPlayer.ReadyCircleMemberCount} / {NetworkPlayer.MaximumCircleMembers})",
                "Waiting for Host to Start the Ritual",
                string.Empty,
                null);
        }

        lastRenderedCircleMemberCount = NetworkPlayer.CircleMemberCount;
        lastRenderedReadyCount = NetworkPlayer.ReadyCircleMemberCount;
    }

    private static void RefreshLeftEntry(
        TMP_Text textEntry,
        BookMenuItem menuItem,
        string value,
        UnityAction action)
    {
        ApplyTextImmediately(textEntry, value);
        if (menuItem == null)
        {
            return;
        }

        menuItem.SetOnClickAction(action);
        bool enabled = action != null && !string.IsNullOrEmpty(value);
        if (menuItem.InteractionEnabled != enabled)
        {
            menuItem.SetInteractionEnabled(enabled);
        }
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

    private static bool ShouldDisplayJoinedCircle(RitualSealService sealService)
    {
        return sealService != null &&
               !sealService.IsHostingRitual &&
               sealService.JoinStatus == RitualJoinStatus.Joined &&
               NetworkPlayer.IsLocalPlayerCircleMember;
    }

    public void RefreshJoinSealPresentation()
    {
        if (preparedState == BookState.JoinSealEntry && rightPageController != null)
        {
            rightPageController.RefreshJoinSealSilently();
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
