using UnityEngine;

namespace Incantation.Networking
{
    [DisallowMultipleComponent]
    public sealed class FishNetFoundationHud : MonoBehaviour
    {
        [SerializeField]
        private FishNetFoundationController foundationController;

        private void OnGUI()
        {
            const float width = 220f;
            const float height = 42f;
            const float gap = 8f;
            const float left = 20f;
            const float top = 20f;

            if (foundationController == null)
            {
                GUI.Label(new Rect(left, top, width * 2f, height), "FishNet foundation controller is not assigned.");
                return;
            }

            if (GUI.Button(new Rect(left, top, width, height), "Start Host"))
            {
                foundationController.StartHost();
            }

            if (GUI.Button(new Rect(left, top + height + gap, width, height), "Start Server"))
            {
                foundationController.StartServer();
            }

            if (GUI.Button(new Rect(left, top + (height + gap) * 2f, width, height), "Start Client (localhost)"))
            {
                foundationController.StartClient();
            }

            if (GUI.Button(new Rect(left, top + (height + gap) * 3f, width, height), "Disconnect"))
            {
                foundationController.Disconnect();
            }
        }

        public void SetFoundationController(FishNetFoundationController controller)
        {
            foundationController = controller;
        }
    }
}
