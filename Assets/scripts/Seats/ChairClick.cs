using UnityEngine;

public class ChairClick : MonoBehaviour
{
    [Header("Seat associé")]
    public Seat seat;

    private void OnMouseDown()
    {
        if (seat == null)
        {
            Debug.LogWarning($"{name} n'a pas de Seat assigné.");
            return;
        }

        SeatManager seatManager = FindFirstObjectByType<SeatManager>();

        if (seatManager == null)
        {
            Debug.LogError("Aucun SeatManager trouvé dans la scène.");
            return;
        }

        seatManager.TrySit(seat);
    }
}