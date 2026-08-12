using UnityEngine;
using Incantation.Networking;

public class BookMenuController : MonoBehaviour
{
    [Header("Book State Reference")]
    [SerializeField] private BookStateController bookStateController;
    [SerializeField] private BookTextModeController bookTextModeController;

    [Header("Camera References")]
    [SerializeField] private CameraTransitionManager cameraTransitionManager;
    [SerializeField, HideInInspector] private Transform lobbyCameraTarget;
    [SerializeField] private Transform characterCameraTarget;
    [SerializeField] private Transform bookMenuCameraTarget;

    [Header("Book Rotation Reference")]
    [SerializeField] private BookRotationController bookRotationController;

    [Header("Lobby Reference")]
    [SerializeField] private LobbyController lobbyController;

    [Header("External Links")]
    [SerializeField] private string discordUrl;

    private BookState characterReturnState = BookState.MainMenu;
    private string enteredSeal = string.Empty;
    private string editorValue = string.Empty;
    private string editorStatus = string.Empty;
    private bool editingPriestName;
    private bool editingHostedSeal;

    public string EnteredSeal => enteredSeal;
    public bool HasValidSeal => RitualSealService.NormalizeSeal(enteredSeal).Length == 4;
    public bool CanSubmitSeal => HasValidSeal &&
                                 RitualSealService.Instance != null &&
                                 !RitualSealService.Instance.IsJoining &&
                                 RitualSealService.Instance.JoinStatus != RitualJoinStatus.Joining;

    private void Awake()
    {
        if (cameraTransitionManager != null)
        {
            cameraTransitionManager.RegisterBookInteractionTarget(bookMenuCameraTarget);
        }

        if (cameraTransitionManager == null || bookMenuCameraTarget == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot set the startup camera because {nameof(cameraTransitionManager)} or {nameof(bookMenuCameraTarget)} is not assigned.", this);
            return;
        }

        cameraTransitionManager.MoveToImmediate(bookMenuCameraTarget);
    }

    private void OnEnable()
    {
        NetworkPlayer.RitualStartAuthorized += HandleAuthorizedRitualStart;

        if (RitualSealService.Instance != null)
        {
            RitualSealService.Instance.Changed += HandleRitualSealServiceChanged;
        }
    }

    private void OnDisable()
    {
        NetworkPlayer.RitualStartAuthorized -= HandleAuthorizedRitualStart;

        if (RitualSealService.Instance != null)
        {
            RitualSealService.Instance.Changed -= HandleRitualSealServiceChanged;
        }

        if (NetworkPlayer.LocalPlayer != null)
        {
            NetworkPlayer.LocalPlayer.PriestNameChanged -= HandleLocalPriestNameChanged;
        }

        LocalInputContextGate.RestoreGameplay();

        if (cameraTransitionManager != null)
        {
            cameraTransitionManager.UnregisterBookInteractionTarget(bookMenuCameraTarget);
        }
    }

    public void OpenPlayMenu()
    {
        bookStateController.ChangePage(BookState.PlayMenu);
    }

    public void OpenHostMenu()
    {
        CreateNetworkRitual();
    }

    public void OpenJoinMenu()
    {
        RitualSealService.Instance?.CancelJoin();
        enteredSeal = string.Empty;
        RitualSealService.Instance?.BeginJoinEntry();
        AcquireTextEntryContext();
        bookStateController.ChangePage(BookState.JoinSealEntry);
    }

    public void CreateNetworkRitual()
    {
        if (bookStateController == null ||
            !bookStateController.BeginHostCreationTransition(StartNetworkRitual))
        {
            Debug.LogWarning("The Create Ritual transition could not begin.", this);
        }
    }

    private void StartNetworkRitual()
    {
        RitualSealService service = RitualSealService.Instance;
        if (service == null || !service.CreateRitual())
        {
            Debug.LogWarning("The Ritual Seal service could not create a ritual.", this);
        }
    }

    public void CancelJoinRitual()
    {
        enteredSeal = string.Empty;
        ReleaseTextEntryContext();
        bookStateController.ChangePage(BookState.PlayMenu);
        RitualSealService.Instance?.CancelJoin();
    }

    public void SubmitSeal()
    {
        if (!CanSubmitSeal)
        {
            return;
        }

        RitualSealService service = RitualSealService.Instance;
        service.JoinRitual(enteredSeal);
    }

    private void Update()
    {
        if (editingPriestName || editingHostedSeal)
        {
            HandleEditorInput();
            return;
        }

        if (bookStateController == null || bookStateController.CurrentState != BookState.JoinSealEntry)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelJoinRitual();
            return;
        }

        string input = Input.inputString;
        bool changed = false;
        for (int i = 0; i < input.Length; i++)
        {
            char character = input[i];
            if (character == '\b')
            {
                if (enteredSeal.Length > 0)
                {
                    enteredSeal = enteredSeal.Substring(0, enteredSeal.Length - 1);
                    changed = true;
                }
            }
            else if (character == '\n' || character == '\r')
            {
                if (CanSubmitSeal)
                {
                    SubmitSeal();
                }

                return;
            }
            else
            {
                string normalized = RitualSealService.NormalizeSeal(character.ToString());
                if (normalized.Length == 1)
                {
                    if (enteredSeal.Length < 4)
                    {
                        enteredSeal += normalized;
                        changed = true;
                    }
                }
            }
        }

        if (changed)
        {
            RitualSealService service = RitualSealService.Instance;
            if (service != null)
            {
                service.BeginJoinEntry();
            }
            else
            {
                bookStateController.RefreshJoinSealPresentation();
            }
        }
    }

    public void ReturnToMainMenu()
    {
        if (bookRotationController != null)
            bookRotationController.RotateToFront();

        bookStateController.ChangePage(BookState.MainMenu);
    }

    public void ReturnFromCharacterView()
    {
        bookStateController.ChangePage(characterReturnState);
        ReturnToBookMenu();
    }

    public void ReturnToPlayMenu()
    {
        bookStateController.ChangePage(BookState.PlayMenu);
    }

    public void StartRitualFromBook()
    {
        if (lobbyController == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot request ritual start because no {nameof(LobbyController)} is assigned.", this);
            return;
        }

        NetworkPlayer localPlayer = NetworkPlayer.LocalPlayer;
        if (localPlayer == null)
        {
            Debug.LogWarning("Cannot request ritual start because no local NetworkPlayer exists.", this);
            return;
        }

        if (!lobbyController.HasSelectedLobbySeat())
        {
            Debug.LogWarning("Cannot start ritual: the local player has not selected a seat.", this);
            return;
        }

        lobbyController.PrepareSharedBookPresentation();
        if (!localPlayer.RequestRitualStart())
            lobbyController.RestoreLocalBookPresentation();
    }

    private void HandleAuthorizedRitualStart()
    {
        if (lobbyController == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot start the authorized ritual because no {nameof(LobbyController)} is assigned.", this);
            return;
        }

        if (bookTextModeController != null)
            bookTextModeController.ShowRitualTexts();

        lobbyController.StartLobbyRitual();
    }

    public void OpenCirclePage()
    {
        if (bookStateController != null)
        {
            bookStateController.ChangePage(BookState.Lobby);
        }
    }

    public void BeginSeatSelection()
    {
        if (lobbyController == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot begin seat selection because no {nameof(LobbyController)} is assigned.", this);
            return;
        }

        lobbyController.BeginSeatSelection();
    }

    [System.Obsolete("Use BeginSeatSelection for the physical Seat-selection flow.")]
    public void OpenLobby()
    {
        BeginSeatSelection();
    }

    public void EditPriestName()
    {
        NetworkPlayer localPlayer = NetworkPlayer.LocalPlayer;
        if (localPlayer == null)
        {
            return;
        }

        editingPriestName = true;
        editingHostedSeal = false;
        AcquireTextEntryContext();
        editorValue = localPlayer.PriestName;
        editorStatus = string.Empty;
        localPlayer.PriestNameChanged -= HandleLocalPriestNameChanged;
        localPlayer.PriestNameChanged += HandleLocalPriestNameChanged;
        RefreshActiveEditor();
    }

    public void EditHostedSeal()
    {
        RitualSealService service = RitualSealService.Instance;
        if (service == null || !service.IsHostingRitual)
        {
            return;
        }

        editingHostedSeal = true;
        editingPriestName = false;
        AcquireTextEntryContext();
        editorValue = service.ActiveSeal;
        editorStatus = string.Empty;
        RefreshActiveEditor();
    }

    public void ConfirmActiveEditor()
    {
        if (editingPriestName)
        {
            if (!NetworkPlayer.TryNormalizePriestName(
                    editorValue, out string normalizedName, out editorStatus))
            {
                RefreshActiveEditor();
                return;
            }

            if (NetworkPlayer.LocalPlayer != null &&
                NetworkPlayer.LocalPlayer.PriestName == normalizedName)
            {
                CancelActiveEditor();
                return;
            }

            if (NetworkPlayer.LocalPlayer == null ||
                !NetworkPlayer.LocalPlayer.RequestPriestName(normalizedName))
            {
                editorStatus = "Unable to save name";
                RefreshActiveEditor();
                return;
            }
            editorStatus = "Saving...";
            RefreshActiveEditor();
            return;
        }

        if (editingHostedSeal)
        {
            RitualSealService service = RitualSealService.Instance;
            if (service == null || !service.RequestSealReplacement(editorValue))
            {
                editorStatus = service != null ? service.StatusMessage : "Seal service unavailable";
            }
            else
            {
                editorStatus = service.StatusMessage;
            }

            RefreshActiveEditor();
        }
    }

    public void CancelActiveEditor()
    {
        if (NetworkPlayer.LocalPlayer != null)
        {
            NetworkPlayer.LocalPlayer.PriestNameChanged -= HandleLocalPriestNameChanged;
        }

        editingPriestName = false;
        editingHostedSeal = false;
        editorStatus = string.Empty;
        ReleaseTextEntryContext();
        bookStateController.RefreshCurrentPageContent();
    }

    private void HandleEditorInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelActiveEditor();
            return;
        }

        string input = Input.inputString;
        bool changed = false;
        for (int i = 0; i < input.Length; i++)
        {
            char character = input[i];
            if (character == '\b')
            {
                if (editorValue.Length > 0)
                {
                    editorValue = editorValue.Substring(0, editorValue.Length - 1);
                    changed = true;
                }
            }
            else if (character == '\n' || character == '\r')
            {
                ConfirmActiveEditor();
                return;
            }
            else if (editingHostedSeal)
            {
                string normalized = RitualSealService.NormalizeSeal(character.ToString());
                if (normalized.Length == 1 && editorValue.Length < 4)
                {
                    editorValue += normalized;
                    changed = true;
                }
            }
            else if (!char.IsControl(character))
            {
                editorValue += character;
                changed = true;
            }
        }

        if (changed)
        {
            editorStatus = string.Empty;
            RefreshActiveEditor();
        }
    }

    private void RefreshActiveEditor()
    {
        if (editingPriestName)
        {
            bookStateController.ShowPriestNameEditor(
                editorValue, editorStatus, ConfirmActiveEditor, CancelActiveEditor);
        }
        else if (editingHostedSeal)
        {
            RitualSealService service = RitualSealService.Instance;
            if (service != null && service.StatusMessage == "Seal already used")
            {
                editorStatus = service.StatusMessage;
            }

            bookStateController.ShowHostedSealEditor(
                editorValue, editorStatus, ConfirmActiveEditor, CancelActiveEditor);
        }
    }

    private void HandleLocalPriestNameChanged(string previousName, string currentName)
    {
        if (!editingPriestName)
        {
            return;
        }

        CancelActiveEditor();
    }

    private void HandleRitualSealServiceChanged()
    {
        if (!editingHostedSeal)
        {
            return;
        }

        RitualSealService service = RitualSealService.Instance;
        if (service == null)
        {
            editorStatus = "Seal service unavailable";
            RefreshActiveEditor();
            return;
        }

        string normalized = RitualSealService.NormalizeSeal(editorValue);
        if (!service.IsReplacingSeal && service.ActiveSeal == normalized &&
            string.IsNullOrEmpty(service.StatusMessage))
        {
            CancelActiveEditor();
            return;
        }

        editorStatus = service.StatusMessage;
        RefreshActiveEditor();
    }

    public void LeaveLobbyRitual()
    {
        ReturnToActiveHostLobby();
    }

    public void ReturnToActiveHostLobby()
    {
        if (RitualSealService.Instance != null && RitualSealService.Instance.IsHostingRitual)
        {
            bookStateController.ChangePage(BookState.HostMenu);
        }
    }

    public void PresentConnectedLobbyAfterMatch()
    {
        if (bookTextModeController != null)
            bookTextModeController.ShowMenuTexts();

        ReturnToBookMenu();
        if (RitualSealService.Instance != null && RitualSealService.Instance.IsHostingRitual)
            bookStateController.ChangePage(BookState.HostMenu);
        else
            bookStateController.ChangePage(BookState.Lobby);
    }

    public void QuitHostedRitual()
    {
        RitualSealService service = RitualSealService.Instance;
        if (service == null || !service.QuitHostedRitual())
        {
            Debug.LogWarning("The hosted ritual could not be stopped.", this);
            return;
        }

        lobbyController?.EndSeatSelection();
        ReturnToBookMenu();
    }

    public void QuitRitual()
    {
        RitualSealService service = RitualSealService.Instance;
        if (service == null)
        {
            ReturnToBookMenu();
            bookStateController.ChangePage(BookState.PlayMenu);
            return;
        }

        if (service.IsHostingRitual || service.IsCreatingRitual)
        {
            QuitHostedRitual();
            return;
        }

        if (service.JoinStatus == RitualJoinStatus.Joined)
        {
            if (!service.LeaveJoinedRitual())
            {
                if (!service.IsLeavingJoinedRitual)
                {
                    Debug.LogWarning("The joined ritual could not be left.", this);
                }

                return;
            }

            lobbyController?.EndSeatSelection();
            ReturnToBookMenu();
            bookStateController.ChangePage(BookState.PlayMenu);
            return;
        }

        ReturnToBookMenu();
        bookStateController.ChangePage(BookState.PlayMenu);
    }

    public void InvitePriest()
    {
        Debug.Log("Invite a Priest is not implemented yet.", this);
    }

    public bool TryLeaveLobbySeat()
    {
        if (lobbyController == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot leave the lobby seat because no {nameof(LobbyController)} is assigned.", this);
            return false;
        }

        return lobbyController.TryLeaveLobbySeat();
    }

    public void OpenCharacter()
    {
        EnterCharacterView();
    }

    public void ShowCharacter()
    {
        EnterCharacterView();
    }

    public void EnterCharacterView()
    {
        if (bookStateController == null)
            return;

        if (bookStateController.CurrentState != BookState.CharacterMenu)
        {
            characterReturnState = bookStateController.CurrentState;
            bookStateController.ChangePage(BookState.CharacterMenu);
        }

        if (cameraTransitionManager == null || characterCameraTarget == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot show the Character because {nameof(cameraTransitionManager)} or {nameof(characterCameraTarget)} is not assigned.", this);
            return;
        }

        if (bookRotationController != null)
            bookRotationController.RotateToBack();

        cameraTransitionManager.MoveTo(characterCameraTarget);
    }

    public void OpenOptions()
    {
        bookStateController.ChangePage(BookState.OptionsMenu);
    }

    public void OpenVoiceOptions()
    {
        bookStateController.ChangePage(BookState.VoiceMenu);
    }

    public void ReturnToOptions()
    {
        bookStateController.ChangePage(BookState.OptionsMenu);
    }

    public void OpenLeaderboard()
    {
        Debug.Log("Leaderboard is not implemented yet.", this);
    }

    public void OpenDiscord()
    {
        if (string.IsNullOrEmpty(discordUrl))
        {
            Debug.LogWarning("Discord URL is not assigned.", this);
            return;
        }

        Application.OpenURL(discordUrl);
    }

    public void ReturnToBookMenu()
    {
        if (bookTextModeController != null)
            bookTextModeController.ShowMenuTexts();

        if (cameraTransitionManager == null || bookMenuCameraTarget == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot return to the Book Menu because {nameof(cameraTransitionManager)} or {nameof(bookMenuCameraTarget)} is not assigned.", this);
            return;
        }

        if (bookRotationController != null)
            bookRotationController.RotateToFront();

        cameraTransitionManager.MoveTo(bookMenuCameraTarget);
    }

    public void HandleBookStateChanging(BookState state)
    {
        if (state == BookState.JoinSealEntry || editingPriestName || editingHostedSeal)
        {
            AcquireTextEntryContext();
            return;
        }

        ReleaseTextEntryContext();
    }

    private static void AcquireTextEntryContext()
    {
        LocalInputContextGate.SetContext(LocalInputContext.TextEntry);
    }

    private static void ReleaseTextEntryContext()
    {
        if (LocalInputContextGate.IsTextEntryActive)
        {
            LocalInputContextGate.SetContext(LocalInputContext.BookInteraction);
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

}
