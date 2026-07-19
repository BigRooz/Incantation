using UnityEngine;

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
        bookStateController.SetState(BookState.JoinMenu);
    }

    public void ReturnToMainMenu()
    {
        bookStateController.SetState(BookState.MainMenu);
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

        cameraTransitionManager.MoveTo(lobbyCameraTarget);
    }

    public void OpenCharacter()
    {
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
