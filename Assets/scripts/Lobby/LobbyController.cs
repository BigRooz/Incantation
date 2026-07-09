using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum LocalGameState
{
    Lobby,
    Ritual
}

public class LobbyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RitualController ritualController;
    [SerializeField] private GameObject lobbyCanvasRoot;
    [SerializeField] private Button startRitualButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitGameButton;

    [Header("Runtime UI")]
    [SerializeField] private bool createLobbyUiIfMissing = true;

    [Header("Lobby Input Control")]
    [SerializeField] private bool autoDisablePlayerMovementDuringLobby = true;
    [SerializeField] private bool lockCursorWhenRitualStarts = true;
    [SerializeField] private List<MonoBehaviour> gameplayInputBehaviours = new List<MonoBehaviour>();

    private readonly Dictionary<MonoBehaviour, bool> originalGameplayInputEnabledStates = new Dictionary<MonoBehaviour, bool>();

    public LocalGameState CurrentState { get; private set; } = LocalGameState.Lobby;

    private void Awake()
    {
        ResolveReferences();

        if (createLobbyUiIfMissing)
            EnsureLobbyUi();

        ShowLobby();
    }

    private void OnEnable()
    {
        AddButtonListeners();
    }

    private void OnDisable()
    {
        RemoveButtonListeners();
        RestoreGameplayInputBehaviours();
    }

    private void LateUpdate()
    {
        if (CurrentState != LocalGameState.Lobby)
            return;

        ApplyLobbyCursorState();
    }

    public void StartLobbyRitual()
    {
        if (ritualController == null)
        {
            Debug.LogWarning($"{nameof(LobbyController)} cannot start the ritual because no {nameof(RitualController)} is assigned.", this);
            return;
        }

        CurrentState = LocalGameState.Ritual;

        if (lobbyCanvasRoot != null)
            lobbyCanvasRoot.SetActive(false);

        RestoreGameplayInputBehaviours();
        ApplyRitualCursorState();
        ritualController.StartRitual();
    }

    public void OpenOptions()
    {
        Debug.Log("Options are not implemented in the local lobby foundation yet.", this);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowLobby()
    {
        CurrentState = LocalGameState.Lobby;

        CacheAndDisableGameplayInputBehaviours();
        ApplyLobbyCursorState();

        if (lobbyCanvasRoot != null)
            lobbyCanvasRoot.SetActive(true);
    }

    private void ResolveReferences()
    {
        if (ritualController == null)
            ritualController = FindFirstObjectByType<RitualController>();
    }

    private void ApplyLobbyCursorState()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ApplyRitualCursorState()
    {
        if (!lockCursorWhenRitualStarts)
            return;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void CacheAndDisableGameplayInputBehaviours()
    {
        if (!autoDisablePlayerMovementDuringLobby)
            return;

        PopulateDefaultGameplayInputBehaviours();

        foreach (MonoBehaviour gameplayInputBehaviour in gameplayInputBehaviours)
        {
            if (gameplayInputBehaviour == null)
                continue;

            if (!originalGameplayInputEnabledStates.ContainsKey(gameplayInputBehaviour))
                originalGameplayInputEnabledStates.Add(gameplayInputBehaviour, gameplayInputBehaviour.enabled);

            gameplayInputBehaviour.enabled = false;
        }
    }

    private void RestoreGameplayInputBehaviours()
    {
        foreach (KeyValuePair<MonoBehaviour, bool> enabledState in originalGameplayInputEnabledStates)
        {
            if (enabledState.Key != null)
                enabledState.Key.enabled = enabledState.Value;
        }

        originalGameplayInputEnabledStates.Clear();
    }

    private void PopulateDefaultGameplayInputBehaviours()
    {
        if (gameplayInputBehaviours == null)
            gameplayInputBehaviours = new List<MonoBehaviour>();

        PlayerMovement[] playerMovementBehaviours = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (PlayerMovement playerMovementBehaviour in playerMovementBehaviours)
        {
            if (playerMovementBehaviour != null && !gameplayInputBehaviours.Contains(playerMovementBehaviour))
                gameplayInputBehaviours.Add(playerMovementBehaviour);
        }
    }

    private void EnsureLobbyUi()
    {
        if (lobbyCanvasRoot == null)
            CreateLobbyCanvas();

        EnsureEventSystem();
        AddButtonListeners();
    }

    private void CreateLobbyCanvas()
    {
        GameObject canvasObject = new GameObject("LobbyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        lobbyCanvasRoot = canvasObject;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        StretchToParent(canvasRect);

        GameObject panelObject = CreateUiObject("LobbyPanel", canvasRect);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Text titleText = CreateText("Title", panelRect, "Incantation", 72, FontStyle.Bold);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 190f);
        titleRect.sizeDelta = new Vector2(700f, 110f);

        startRitualButton = CreateButton("StartRitualButton", panelRect, "Start Ritual", new Vector2(0f, 40f));
        optionsButton = CreateButton("OptionsButton", panelRect, "Options", new Vector2(0f, -50f));
        quitGameButton = CreateButton("QuitGameButton", panelRect, "Quit Game", new Vector2(0f, -140f));
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static Text CreateText(string objectName, Transform parent, string text, int fontSize, FontStyle fontStyle)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        Text textComponent = textObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.color = Color.white;
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        return textComponent;
    }

    private static Button CreateButton(string objectName, Transform parent, string label, Vector2 anchoredPosition)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.13f, 0.11f, 0.1f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.13f, 0.11f, 0.1f, 0.95f);
        colors.highlightedColor = new Color(0.28f, 0.22f, 0.18f, 0.98f);
        colors.pressedColor = new Color(0.08f, 0.06f, 0.05f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(360f, 64f);

        Text labelText = CreateText("Text", buttonRect, label, 28, FontStyle.Normal);
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        StretchToParent(labelRect);

        return button;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void AddButtonListeners()
    {
        if (startRitualButton != null)
        {
            startRitualButton.onClick.RemoveListener(StartLobbyRitual);
            startRitualButton.onClick.AddListener(StartLobbyRitual);
        }

        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveListener(OpenOptions);
            optionsButton.onClick.AddListener(OpenOptions);
        }

        if (quitGameButton != null)
        {
            quitGameButton.onClick.RemoveListener(QuitGame);
            quitGameButton.onClick.AddListener(QuitGame);
        }
    }

    private void RemoveButtonListeners()
    {
        if (startRitualButton != null)
            startRitualButton.onClick.RemoveListener(StartLobbyRitual);

        if (optionsButton != null)
            optionsButton.onClick.RemoveListener(OpenOptions);

        if (quitGameButton != null)
            quitGameButton.onClick.RemoveListener(QuitGame);
    }
}
