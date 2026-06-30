using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class NotebookController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private SpellPhraseLibrary spellPhraseLibrary;
    [SerializeField] private bool showDisabledPhrases;

    [Header("Notebook Presentation")]
    [Tooltip("Optional physical card/book object held by the local player.")]
    [SerializeField] private GameObject notebookVisualRoot;
    [Tooltip("Root UI panel that contains the readable notebook spell list.")]
    [SerializeField] private GameObject notebookUiRoot;
    [SerializeField] private Transform spellPageParent;
    [SerializeField] private NotebookSpellPage spellPagePrefab;

    [Header("Behavior")]
    [SerializeField] private bool openOnStart;
    [SerializeField] private bool rebuildPagesWhenOpened = true;
    [SerializeField] private bool clearExistingPagesOnBuild = true;
    [SerializeField] private bool manageCursor;
    [SerializeField] private CursorLockMode closedCursorLockMode = CursorLockMode.Locked;

    [Header("Events")]
    public UnityEvent<bool> OnNotebookOpenChanged = new UnityEvent<bool>();

    private readonly List<NotebookSpellPage> spawnedPages = new List<NotebookSpellPage>();
    private bool isOpen;
    private bool hasBuiltPages;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    public bool IsOpen => isOpen;
    public SpellPhraseLibrary SpellPhraseLibrary => spellPhraseLibrary;

    private void Awake()
    {
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        SetOpen(openOnStart, true);
    }

    private void OnDisable()
    {
        if (manageCursor)
            RestoreCursor();
    }

    public void ToggleNotebook()
    {
        SetOpen(!isOpen);
    }

    public void OpenNotebook()
    {
        SetOpen(true);
    }

    public void CloseNotebook()
    {
        SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        SetOpen(open, false);
    }

    public void RefreshPages()
    {
        BuildPages();
    }

    private void SetOpen(bool open, bool force)
    {
        if (!force && isOpen == open)
            return;

        isOpen = open;

        if (notebookVisualRoot != null)
            notebookVisualRoot.SetActive(isOpen);

        if (notebookUiRoot != null)
            notebookUiRoot.SetActive(isOpen);

        if (isOpen && (rebuildPagesWhenOpened || !hasBuiltPages))
            BuildPages();

        if (manageCursor)
            ApplyCursorState(isOpen);

        OnNotebookOpenChanged.Invoke(isOpen);
    }

    private void BuildPages()
    {
        if (spellPageParent == null)
        {
            Debug.LogWarning("NotebookController requires a spell page parent before it can display spell phrases.", this);
            return;
        }

        if (spellPagePrefab == null)
        {
            Debug.LogWarning("NotebookController requires a NotebookSpellPage prefab before it can display spell phrases.", this);
            return;
        }

        if (clearExistingPagesOnBuild)
            ClearSpawnedPages();

        if (spellPhraseLibrary == null)
        {
            hasBuiltPages = true;
            Debug.LogWarning("NotebookController has no SpellPhraseLibrary assigned.", this);
            return;
        }

        IReadOnlyList<SpellPhrase> spellPhrases = spellPhraseLibrary.GetSpellPhrases();

        for (int i = 0; i < spellPhrases.Count; i++)
        {
            SpellPhrase spellPhrase = spellPhrases[i];

            if (spellPhrase == null)
                continue;

            if (!showDisabledPhrases && !spellPhrase.Enabled)
                continue;

            NotebookSpellPage page = Instantiate(spellPagePrefab, spellPageParent);
            page.Display(spellPhrase);
            spawnedPages.Add(page);
        }

        hasBuiltPages = true;
    }

    private void ClearSpawnedPages()
    {
        for (int i = spawnedPages.Count - 1; i >= 0; i--)
        {
            NotebookSpellPage page = spawnedPages[i];

            if (page == null)
                continue;

            if (Application.isPlaying)
                Destroy(page.gameObject);
            else
                DestroyImmediate(page.gameObject);
        }

        spawnedPages.Clear();
        hasBuiltPages = false;
    }

    private void ApplyCursorState(bool notebookOpen)
    {
        if (notebookOpen)
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode == CursorLockMode.None ? closedCursorLockMode : previousCursorLockMode;
    }

    private void RestoreCursor()
    {
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }
}
