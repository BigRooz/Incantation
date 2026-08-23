using System;

namespace Incantation.Networking.Spells
{
    public enum SpellCastResult : byte
    {
        None,
        Accepted,
        Rejected
    }

    [Serializable]
    public struct SpellCastRequest
    {
        public uint RequestSequence;
        public uint RitualSequence;
        public uint TurnSequence;
        public uint CardInstanceId;
        public string DefinitionId;
        public string NormalizedPhrase;

        public SpellCastRequest(
            uint requestSequence,
            uint ritualSequence,
            uint turnSequence,
            uint cardInstanceId,
            string definitionId,
            string normalizedPhrase)
        {
            RequestSequence = requestSequence;
            RitualSequence = ritualSequence;
            TurnSequence = turnSequence;
            CardInstanceId = cardInstanceId;
            DefinitionId = definitionId ?? string.Empty;
            NormalizedPhrase = normalizedPhrase ?? string.Empty;
        }
    }
}
