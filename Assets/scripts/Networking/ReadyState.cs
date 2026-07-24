namespace Incantation.Networking
{
    /// <summary>
    /// Describes whether a connected player has confirmed readiness.
    /// Lobby rules own valid transitions; NetworkPlayer only stores and exposes the value.
    /// </summary>
    public enum ReadyState
    {
        NotReady,
        Ready
    }
}
