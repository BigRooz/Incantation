using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Presents the temporary FishNet foundation runtime state and guarded diagnostic actions.
    /// It contains no production multiplayer menu, session authority, or gameplay behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetFoundationHud : MonoBehaviour
    {
        [SerializeField]
        private FishNetFoundationController foundationController;

        private void OnGUI()
        {
            const float width = 300f;
            const float buttonHeight = 42f;
            const float lineHeight = 24f;
            const float gap = 8f;
            const float left = 20f;
            const float top = 20f;

            if (foundationController == null)
            {
                GUI.Label(new Rect(left, top, width * 2f, buttonHeight), "FishNet foundation controller is not assigned.");
                return;
            }

            float currentTop = top;

            GUI.Box(new Rect(left - 8f, top - 8f, width + 16f, 360f), "FishNet Foundation Diagnostics");
            currentTop += lineHeight;

            string overallState = foundationController.IsHostRunning
                ? "Host Running"
                : "Host Not Running";
            GUI.Label(new Rect(left, currentTop, width, lineHeight), overallState);
            currentTop += lineHeight;

            GUI.Label(new Rect(left, currentTop, width, lineHeight), foundationController.GetServerStatusText());
            currentTop += lineHeight;

            GUI.Label(new Rect(left, currentTop, width, lineHeight), foundationController.GetClientStatusText());
            currentTop += lineHeight;

            GUI.Label(new Rect(left, currentTop, width, lineHeight), $"Tugboat Port: {foundationController.Port}");
            currentTop += lineHeight;

            GUI.Label(new Rect(left, currentTop, width, lineHeight), $"Client Address: {foundationController.ClientAddress}");
            currentTop += lineHeight + gap;

            DrawGuardedButton(
                new Rect(left, currentTop, width, buttonHeight),
                "Start Host",
                foundationController.CanStartHost,
                foundationController.StartHost);
            currentTop += buttonHeight + gap;

            DrawGuardedButton(
                new Rect(left, currentTop, width, buttonHeight),
                "Start Server",
                foundationController.CanStartServer,
                foundationController.StartServer);
            currentTop += buttonHeight + gap;

            DrawGuardedButton(
                new Rect(left, currentTop, width, buttonHeight),
                $"Start Client ({foundationController.ClientAddress})",
                foundationController.CanStartClient,
                foundationController.StartClient);
            currentTop += buttonHeight + gap;

            DrawGuardedButton(
                new Rect(left, currentTop, width, buttonHeight),
                "Disconnect",
                foundationController.CanDisconnect,
                foundationController.Disconnect);
            currentTop += buttonHeight + gap;

            if (!string.IsNullOrEmpty(foundationController.ServerFailureStatus))
            {
                GUIStyle warningStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true
                };
                warningStyle.normal.textColor = new Color(1f, 0.65f, 0.35f);

                GUI.Box(
                    new Rect(left, currentTop, width, 72f),
                    foundationController.ServerFailureStatus,
                    warningStyle);
            }
        }

        public void SetFoundationController(FishNetFoundationController controller)
        {
            foundationController = controller;
        }

        private static void DrawGuardedButton(
            Rect rect,
            string label,
            bool isAvailable,
            System.Func<bool> action)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = isAvailable;

            if (GUI.Button(rect, label))
            {
                action();
            }

            GUI.enabled = previousEnabled;
        }
    }
}
