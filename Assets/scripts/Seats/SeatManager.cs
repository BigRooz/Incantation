using System.Collections.Generic;
using UnityEngine;

public class SeatManager : MonoBehaviour
{
    [Header("Sièges détectés")]
    public List<Seat> seats = new List<Seat>();

    [Header("Livre")]
    public BookMover bookMover;
    public Seat currentBookSeat;

    private void Awake()
    {
        FindSeats();
    }

    private void Update()
    {
        // TEST : appuie sur ESPACE pour envoyer le livre au prochain joueur occupé
        if (Input.GetKeyDown(KeyCode.Space))
        {
            MoveBookToNextOccupiedSeat();
        }
    }

    private void OnValidate()
    {
        FindSeats();
    }

    [ContextMenu("Find Seats")]
    public void FindSeats()
    {
        seats.Clear();

        Seat[] foundSeats = GetComponentsInChildren<Seat>(true);

        foreach (Seat seat in foundSeats)
        {
            if (seat != null)
                seats.Add(seat);
        }
    }

    public void TrySit(Seat seat)
    {
        if (seat == null)
            return;

        GameObject player = GameObject.FindWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("Aucun objet avec le tag Player trouvé.");
            return;
        }

        // Libère l’ancien siège du joueur si nécessaire
        foreach (Seat s in seats)
        {
            if (s.currentPlayer == player)
                s.Free();
        }

        player.transform.position = seat.playerSpawn.position;
        player.transform.rotation = seat.playerSpawn.rotation;

        seat.Occupy(player);

        Debug.Log($"Le joueur est maintenant assis sur {seat.name}");
    }

    public void MoveBookToSeat(Seat seat)
    {
        if (seat == null)
            return;

        currentBookSeat = seat;

        Debug.Log("SeatManager.MoveBookToSeat used for: " + seat.name);

        if (bookMover != null)
            bookMover.MoveToSeat(seat);
    }

    public void MoveBookToNextOccupiedSeat()
    {
        Seat nextSeat = GetNextOccupiedSeat(currentBookSeat);

        if (nextSeat == null)
        {
            Debug.Log("Aucun autre siège occupé.");
            return;
        }

        MoveBookToSeat(nextSeat);

        Debug.Log("Livre envoyé vers : " + nextSeat.name);
    }

    public List<Seat> GetOccupiedSeats()
    {
        List<Seat> occupiedSeats = new List<Seat>();

        foreach (Seat seat in seats)
        {
            if (!seat.IsFree())
                occupiedSeats.Add(seat);
        }

        return occupiedSeats;
    }

    public Seat GetNextOccupiedSeat(Seat currentSeat)
    {
        List<Seat> occupiedSeats = GetOccupiedSeats();

        if (occupiedSeats.Count == 0)
            return null;

        if (currentSeat == null)
            return occupiedSeats[0];

        int currentIndex = occupiedSeats.IndexOf(currentSeat);

        if (currentIndex == -1)
            return occupiedSeats[0];

        int nextIndex = currentIndex + 1;

        if (nextIndex >= occupiedSeats.Count)
            nextIndex = 0;

        return occupiedSeats[nextIndex];
    }
}
