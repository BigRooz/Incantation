using UnityEngine;
using Incantation.Networking;

public class BookMenuController : MonoBehaviour
{
    [Header("Book State Reference")]
    [SerializeField] private BookStateController bookStateController;
    [SerializeField] private BookTextModeController bookTextModeController;

    [Header("Camera References")]
    [SerializeField] private CameraTransitionManager cameraTransitionManager;
    [SerializeField] private Transform lobbyCameraTarget;
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

    public string EnteredSeal => enteredSeal;

    private void Awake()
    {
        if (cameraTransitionManager == null || bookMenuCameraTarget == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot set the startup camera because {nameof(cameraTransitionManager)} or {nameof(bookMenuCameraTarget)} is not assigned.", this);
            return;
        }

        cameraTransitionManager.MoveToImmediate(bookMenuCameraTarget);
    }

    public void OpenPlayMenu()
    {
        bookStateController.SetState(BookState.PlayMenu);
    }

    public void OpenHostMenu()
    {
        bookStateController.SetState(BookState.HostMenu);
    }

    public void OpenJoinMenu()
    {
        RitualSealService.Instance?.CancelJoin();
        enteredSeal = string.Empty;
        bookStateController.SetState(BookState.JoinMenu);
    }

    public void CreateNetworkRitual()
    {
        RitualSealService service = RitualSealService.Instance;
        if (service == null || !service.CreateRitual())
        {
            Debug.LogWarning("The Ritual Seal service could not create a ritual.", this);
            return;
        }

        bookStateController.SetState(BookState.RitualCreated);
    }

    public void BeginSealEntry()
    {
        enteredSeal = string.Empty;
        RitualSealService.Instance?.BeginJoinEntry();
        bookStateController.SetState(BookState.JoinSealEntry);
    }

    public void CancelJoinRitual()
    {
        enteredSeal = string.Empty;
        bookStateController.SetState(BookState.PlayMenu);
        RitualSealService.Instance?.CancelJoin();
    }

    public void SubmitSeal()
    {
        RitualSealService service = RitualSealService.Instance;
        if (service != null && !service.IsJoining &&
            service.JoinStatus != RitualJoinStatus.Joining && service.JoinRitual(enteredSeal))
        {
            bookStateController.SetState(BookState.JoinSealEntry);
        }
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
                if (enteredSeal.Length > 0)
                {
                    enteredSeal = enteredSeal.Substring(0, enteredSeal.Length - 1);
                    changed = true;
                }
            }
            else if (character == '\n' || character == '\r')
            {
                SubmitSeal();
            }
            else if (enteredSeal.Length < 4)
            {
                string normalized = RitualSealService.NormalizeSeal(character.ToString());
                if (normalized.Length == 1)
                {
                    enteredSeal += normalized;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            bookStateController.SetState(BookState.JoinSealEntry);
        }
    }

    public void ReturnToMainMenu()
    {
        if (bookRotationController != null)
            bookRotationController.RotateToFront();

        bookStateController.SetState(BookState.MainMenu);
    }

    public void ReturnFromCharacterView()
    {
        bookStateController.SetState(characterReturnState);
        ReturnToBookMenu();
    }

    public void ReturnToPlayMenu()
    {
        bookStateController.SetState(BookState.PlayMenu);
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

    public void OpenLobby()
    {
        if (cameraTransitionManager == null || lobbyCameraTarget == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot open the Lobby because {nameof(cameraTransitionManager)} or {nameof(lobbyCameraTarget)} is not assigned.", this);
            return;
        }

        if (lobbyController != null)
            lobbyController.OpenLobby();

        if (bookStateController != null && bookStateController.CurrentState != BookState.Lobby)
            bookStateController.SetState(BookState.Lobby);

        cameraTransitionManager.MoveTo(lobbyCameraTarget);
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
        bookStateController.SetState(BookState.CharacterMenu);
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
        bookStateController.SetState(BookState.OptionsMenu);
    }

    public void OpenVoiceOptions()
    {
        bookStateController.SetState(BookState.VoiceMenu);
    }

    public void ReturnToOptions()
    {
        bookStateController.SetState(BookState.OptionsMenu);
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
