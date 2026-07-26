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
    private bool sealHasSupportedCharacterOverflow;

    public string EnteredSeal => enteredSeal;
    public bool HasValidSeal => !sealHasSupportedCharacterOverflow &&
                                RitualSealService.NormalizeSeal(enteredSeal).Length == 4;
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

    private void OnDisable()
    {
        if (cameraTransitionManager != null)
        {
            cameraTransitionManager.UnregisterBookInteractionTarget(bookMenuCameraTarget);
        }
        else
        {
            LocalInputContextGate.RestoreGameplay();
        }
    }

    public void OpenPlayMenu()
    {
        bookStateController.ChangePage(BookState.PlayMenu);
    }

    public void OpenHostMenu()
    {
        bookStateController.ChangePage(BookState.HostMenu);
    }

    public void OpenJoinMenu()
    {
        RitualSealService.Instance?.CancelJoin();
        enteredSeal = string.Empty;
        sealHasSupportedCharacterOverflow = false;
        RitualSealService.Instance?.BeginJoinEntry();
        bookStateController.ChangePage(BookState.JoinSealEntry);
    }

    public void CreateNetworkRitual()
    {
        RitualSealService service = RitualSealService.Instance;
        if (service == null || !service.CreateRitual())
        {
            Debug.LogWarning("The Ritual Seal service could not create a ritual.", this);
            return;
        }

        bookStateController.RefreshCurrentPageContent();
    }

    public void CancelJoinRitual()
    {
        enteredSeal = string.Empty;
        sealHasSupportedCharacterOverflow = false;
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
        if (bookStateController == null || bookStateController.CurrentState != BookState.JoinSealEntry)
        {
            return;
        }

        string input = Input.inputString;
        bool changed = false;
        for (int i = 0; i < input.Length; i++)
        {
            char character = input[i];
            if (character == '\b')
            {
                if (sealHasSupportedCharacterOverflow)
                {
                    sealHasSupportedCharacterOverflow = false;
                    changed = true;
                }
                else if (enteredSeal.Length > 0)
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
                    else if (!sealHasSupportedCharacterOverflow)
                    {
                        sealHasSupportedCharacterOverflow = true;
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
            Debug.LogWarning($"{nameof(BookMenuController)} cannot start the ritual because no {nameof(LobbyController)} is assigned.", this);
            return;
        }

        if (!lobbyController.HasSelectedLobbySeat())
        {
            Debug.LogWarning("Cannot start ritual: the local player has not selected a seat.", this);
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
        Debug.Log("Priest Name editing is not implemented yet.", this);
    }

    public void LeaveLobbyRitual()
    {
        ReturnToPlayMenu();
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
        characterReturnState = bookStateController.CurrentState;
        bookStateController.ChangePage(BookState.CharacterMenu);
    }

    public void ShowCharacter()
    {
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

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

}
