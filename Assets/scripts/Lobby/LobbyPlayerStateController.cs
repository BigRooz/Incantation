using System;
using UnityEngine;

public class LobbyPlayerStateController : MonoBehaviour
{
    [SerializeField] private LobbyPlayerState currentState = LobbyPlayerState.NotSeated;

    public LobbyPlayerState CurrentState => currentState;
    public bool CanEditPriestName => currentState == LobbyPlayerState.NotSeated;
    public bool CanSelectCharacter => currentState == LobbyPlayerState.NotSeated ||
                                      currentState == LobbyPlayerState.Seated ||
                                      currentState == LobbyPlayerState.Ready;
    public bool CanTakeSeat => currentState == LobbyPlayerState.NotSeated;
    public bool CanLeaveRitual => currentState == LobbyPlayerState.NotSeated;
    public bool CanToggleReady => currentState == LobbyPlayerState.Seated || currentState == LobbyPlayerState.Ready;
    public bool CanLeaveSeat => currentState == LobbyPlayerState.Seated || currentState == LobbyPlayerState.Ready;

    public event Action<LobbyPlayerState, LobbyPlayerState> StateChanged;

    private void Awake()
    {
        currentState = LobbyPlayerState.NotSeated;
    }

    public bool TryTakeSeat()
    {
        return TryTransitionTo(LobbyPlayerState.Seated);
    }

    public bool TryLeaveSeat()
    {
        if (currentState != LobbyPlayerState.Seated && currentState != LobbyPlayerState.Ready)
            return false;

        SetState(LobbyPlayerState.NotSeated);
        return true;
    }

    public bool TryReady()
    {
        return TryTransitionTo(LobbyPlayerState.Ready);
    }

    public bool TryUnready()
    {
        return TryTransitionTo(LobbyPlayerState.Seated);
    }

    [ContextMenu("Lobby State/Take Seat")]
    private void TestTakeSeat()
    {
        TryTakeSeat();
    }

    [ContextMenu("Lobby State/Leave Seat")]
    private void TestLeaveSeat()
    {
        TryLeaveSeat();
    }

    [ContextMenu("Lobby State/Ready")]
    private void TestReady()
    {
        TryReady();
    }

    [ContextMenu("Lobby State/Unready")]
    private void TestUnready()
    {
        TryUnready();
    }

    private bool TryTransitionTo(LobbyPlayerState nextState)
    {
        if (!IsValidTransition(currentState, nextState))
            return false;

        SetState(nextState);
        return true;
    }

    private void SetState(LobbyPlayerState nextState)
    {
        if (currentState == nextState)
            return;

        LobbyPlayerState previousState = currentState;
        currentState = nextState;
        StateChanged?.Invoke(previousState, currentState);
    }

    private static bool IsValidTransition(LobbyPlayerState fromState, LobbyPlayerState toState)
    {
        return (fromState == LobbyPlayerState.NotSeated && toState == LobbyPlayerState.Seated)
            || (fromState == LobbyPlayerState.Seated && toState == LobbyPlayerState.Ready)
            || (fromState == LobbyPlayerState.Ready && toState == LobbyPlayerState.Seated);
    }
}
