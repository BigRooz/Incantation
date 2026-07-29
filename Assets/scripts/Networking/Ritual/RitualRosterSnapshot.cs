using System;
using System.Collections.Generic;

namespace Incantation.Networking.Ritual
{
    /// <summary>
    /// One immutable, serialization-safe participant in the authoritative ritual roster.
    /// </summary>
    public readonly struct RitualRosterEntrySnapshot
    {
        public RitualRosterEntrySnapshot(
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

        public string PlayerId { get; }
        public int SeatId { get; }
        public bool IsActive { get; }
        public bool IsAlive { get; }
        public bool IsEliminated => !IsAlive;
    }

    /// <summary>
    /// Immutable roster ordered by SeatManager's configured physical traversal.
    /// </summary>
    public readonly struct RitualRosterSnapshot
    {
        private readonly RitualRosterEntrySnapshot[] entries;

        public RitualRosterSnapshot(
            uint version,
            RitualTraversalDirection traversalDirection,
            RitualRosterEntrySnapshot[] entries)
        {
            Version = version;
            TraversalDirection = traversalDirection;
            this.entries = entries == null
                ? Array.Empty<RitualRosterEntrySnapshot>()
                : (RitualRosterEntrySnapshot[])entries.Clone();
        }

        public uint Version { get; }
        public RitualTraversalDirection TraversalDirection { get; }
        public int Count => entries?.Length ?? 0;
        public RitualRosterEntrySnapshot[] Entries => CloneEntries(entries);
        public RitualRosterEntrySnapshot[] ActivePlayers => FilterEntries(requireActive: true);
        public RitualRosterEntrySnapshot[] AlivePlayers => FilterEntries(requireAlive: true);
        public RitualRosterEntrySnapshot[] EliminatedPlayers => FilterEntries(requireAlive: false);

        public bool TryGetPlayerBySeat(int seatId, out RitualRosterEntrySnapshot entry)
        {
            if (entries != null)
            {
                foreach (RitualRosterEntrySnapshot candidate in entries)
                {
                    if (candidate.SeatId == seatId)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        public bool TryGetSeatByPlayer(string playerId, out int seatId)
        {
            if (!string.IsNullOrEmpty(playerId) && entries != null)
            {
                foreach (RitualRosterEntrySnapshot candidate in entries)
                {
                    if (string.Equals(candidate.PlayerId, playerId, StringComparison.Ordinal))
                    {
                        seatId = candidate.SeatId;
                        return true;
                    }
                }
            }

            seatId = -1;
            return false;
        }

        private RitualRosterEntrySnapshot[] FilterEntries(bool requireActive = false, bool? requireAlive = null)
        {
            if (entries == null || entries.Length == 0)
                return Array.Empty<RitualRosterEntrySnapshot>();

            List<RitualRosterEntrySnapshot> matches = new(entries.Length);
            foreach (RitualRosterEntrySnapshot entry in entries)
            {
                if (requireActive && !entry.IsActive)
                    continue;

                if (requireAlive.HasValue && entry.IsAlive != requireAlive.Value)
                    continue;

                matches.Add(entry);
            }

            return matches.ToArray();
        }

        private static RitualRosterEntrySnapshot[] CloneEntries(
            RitualRosterEntrySnapshot[] source)
        {
            return source == null
                ? Array.Empty<RitualRosterEntrySnapshot>()
                : (RitualRosterEntrySnapshot[])source.Clone();
        }
    }
}
