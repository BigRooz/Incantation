using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Incantation.UI
{
    /// <summary>
    /// Local-only visual consumer for an authoritative winner identity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RitualGameOverPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject resultRoot;
        [SerializeField] private TMP_Text resultText;
        private Button returnButton;
        private TMP_Text returnText;
        private Action returnAction;

        private void Awake()
        {
            EnsurePresentationHierarchy();
            resultRoot.SetActive(false);
        }

        public void ShowResult(
            string winnerDisplayName,
            bool isLocalWinner,
            bool canReturnToLobby,
            Action onReturnToLobby)
        {
            EnsurePresentationHierarchy();
            resultText.text = isLocalWinner
                ? "VICTORY"
                : $"{GetSafeDisplayName(winnerDisplayName)} WINS";
            resultRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            returnAction = canReturnToLobby ? onReturnToLobby : null;
            returnButton.interactable = canReturnToLobby;
            returnText.text = canReturnToLobby ? "RETURN TO LOBBY" : "WAITING FOR HOST";
        }

        public void HideResult()
        {
            EnsurePresentationHierarchy();
            resultRoot.SetActive(false);
            resultText.text = string.Empty;
            returnAction = null;
        }

        private void EnsurePresentationHierarchy()
        {
            if (resultRoot != null && resultText != null && returnButton != null && returnText != null)
                return;

            GameObject canvasObject = new(
                "GameOverCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject panelObject = new(
                "GameOverPanel",
                typeof(RectTransform),
                typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform panelTransform = panelObject.GetComponent<RectTransform>();
            panelTransform.anchorMin = Vector2.zero;
            panelTransform.anchorMax = Vector2.one;
            panelTransform.offsetMin = Vector2.zero;
            panelTransform.offsetMax = Vector2.zero;
            panelObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.58f);

            GameObject textObject = new(
                "ResultText",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);
            RectTransform textTransform = textObject.GetComponent<RectTransform>();
            textTransform.anchorMin = new Vector2(0.1f, 0.35f);
            textTransform.anchorMax = new Vector2(0.9f, 0.65f);
            textTransform.offsetMin = Vector2.zero;
            textTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 86f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 38f;
            text.fontSizeMax = 86f;
            text.color = new Color(0.9f, 0.78f, 0.5f, 1f);

            GameObject buttonObject = new(
                "ReturnToLobbyButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(panelObject.transform, false);
            RectTransform buttonTransform = buttonObject.GetComponent<RectTransform>();
            buttonTransform.anchorMin = new Vector2(0.35f, 0.2f);
            buttonTransform.anchorMax = new Vector2(0.65f, 0.3f);
            buttonTransform.offsetMin = Vector2.zero;
            buttonTransform.offsetMax = Vector2.zero;
            buttonObject.GetComponent<Image>().color = new Color(0.2f, 0.12f, 0.08f, 0.9f);
            returnButton = buttonObject.GetComponent<Button>();
            returnButton.onClick.AddListener(HandleReturnClicked);

            GameObject returnTextObject = new(
                "ReturnText",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            returnTextObject.transform.SetParent(buttonObject.transform, false);
            RectTransform returnTextTransform = returnTextObject.GetComponent<RectTransform>();
            returnTextTransform.anchorMin = Vector2.zero;
            returnTextTransform.anchorMax = Vector2.one;
            returnTextTransform.offsetMin = Vector2.zero;
            returnTextTransform.offsetMax = Vector2.zero;
            returnText = returnTextObject.GetComponent<TextMeshProUGUI>();
            returnText.alignment = TextAlignmentOptions.Center;
            returnText.fontSize = 34f;
            returnText.color = text.color;

            resultRoot = canvasObject;
            resultText = text;
        }

        private void HandleReturnClicked()
        {
            returnAction?.Invoke();
        }

        private static string GetSafeDisplayName(string winnerDisplayName)
        {
            return string.IsNullOrWhiteSpace(winnerDisplayName)
                ? "THE LAST PRIEST"
                : winnerDisplayName.Trim();
        }
    }
}
