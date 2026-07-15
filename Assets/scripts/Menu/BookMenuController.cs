using UnityEngine;

public class BookMenuController : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private CameraTransitionManager cameraTransitionManager;
    [SerializeField] private Transform lobbyCameraTarget;
    [SerializeField] private Transform characterCameraTarget;
    [SerializeField] private Transform bookMenuCameraTarget;

    [Header("Lobby Reference")]
    [SerializeField] private LobbyController lobbyController;

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
        if (cameraTransitionManager == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot open the Character view because {nameof(cameraTransitionManager)} is not assigned.", this);
            return;
        }

        if (characterCameraTarget == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot open the Character view because {nameof(characterCameraTarget)} is not assigned.", this);
            return;
        }

        cameraTransitionManager.MoveTo(characterCameraTarget);
    }

    public void OpenOptions()
    {
        Debug.Log("Book options page is not implemented yet.", this);
    }

    public void ReturnToBookMenu()
    {
        if (cameraTransitionManager == null || bookMenuCameraTarget == null)
        {
            Debug.LogWarning($"{nameof(BookMenuController)} cannot return to the Book Menu because {nameof(cameraTransitionManager)} or {nameof(bookMenuCameraTarget)} is not assigned.", this);
            return;
        }

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
