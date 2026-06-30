using UnityEngine;
using UnityEngine.EventSystems;

public class NotebookInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NotebookController notebookController;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.E;
    [SerializeField] private bool inputEnabled = true;
    [SerializeField] private bool ignoreInputWhenUiSelected = true;

    private void Reset()
    {
        notebookController = GetComponent<NotebookController>();
    }

    private void Awake()
    {
        if (notebookController == null)
            notebookController = GetComponent<NotebookController>();
    }

    private void Update()
    {
        if (!inputEnabled || notebookController == null)
            return;

        if (ignoreInputWhenUiSelected && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            return;

        if (Input.GetKeyDown(toggleKey))
            notebookController.ToggleNotebook();
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }
}
