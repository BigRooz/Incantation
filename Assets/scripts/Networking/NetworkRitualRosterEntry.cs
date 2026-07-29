using System;

namespace Incantation.Networking
{
    /// <summary>
    /// FishNet transport value for one ritual participant. It intentionally contains no
    /// connection, GameObject, Seat, or presentation reference.
    /// </summary>
    [Serializable]
    public struct NetworkRitualRosterEntry : IEquatable<NetworkRitualRosterEntry>
    {
        public string PlayerId;
        public int SeatId;
        public bool IsActive;
        public bool IsAlive;

        public NetworkRitualRosterEntry(
            string playerId,
            int seatId,
            bool isActive,
            bool isAlive)
        {
            PlayerId = playerId ?? string.Empty;
            SeatId = seatId;
            IsActive = isActive;
            IsAlive = isAlive;
        }

        public bool Equals(NetworkRitualRosterEntry other)
        {
            return string.Equals(PlayerId, other.PlayerId, StringComparison.Ordinal)
                && SeatId == other.SeatId
                && IsActive == other.IsActive
                && IsAlive == other.IsAlive;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkRitualRosterEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = PlayerId != null ? PlayerId.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ SeatId;
                hashCode = (hashCode * 397) ^ IsActive.GetHashCode();
                hashCode = (hashCode * 397) ^ IsAlive.GetHashCode();
                return hashCode;
            }
        }
    }
}
