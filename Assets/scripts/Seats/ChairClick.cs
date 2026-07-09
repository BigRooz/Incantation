using UnityEngine;

public class ChairClick : MonoBehaviour
{
    [Header("Seat associated")]
    public Seat seat;

    private void OnMouseDown()
    {
        if (seat == null)
        {
            Debug.LogWarning($"{name} has no Seat assigned.");
            return;
        }

        LobbyController lobbyController = FindFirstObjectByType<LobbyController>();

        if (lobbyController != null && lobbyController.CurrentState == LocalGameState.Lobby)
        {
            lobbyController.TrySelectLobbySeat(seat);
            return;
        }

        SeatManager seatManager = FindFirstObjectByType<SeatManager>();

        if (seatManager == null)
        {
            Debug.LogError("No SeatManager found in the scene.");
            return;
        }

        seatManager.TrySit(seat);
    }
}
