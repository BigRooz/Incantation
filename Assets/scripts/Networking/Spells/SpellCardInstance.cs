using System;

namespace Incantation.Networking.Spells
{
    /// <summary>
    /// Immutable network identity for one server-assigned spell card.
    /// Instance identity is distinct from the reusable SpellDefinition identity.
    /// </summary>
    [Serializable]
    public struct SpellCardInstance : IEquatable<SpellCardInstance>
    {
        public SpellCardInstance(uint instanceId, string definitionId)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId ?? string.Empty;
        }

        public uint InstanceId;
        public string DefinitionId;
        public bool IsValid => InstanceId > 0 && !string.IsNullOrEmpty(DefinitionId);

        public bool Equals(SpellCardInstance other)
        {
            return InstanceId == other.InstanceId &&
                string.Equals(DefinitionId, other.DefinitionId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SpellCardInstance other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)InstanceId * 397) ^
                    (DefinitionId != null ? StringComparer.Ordinal.GetHashCode(DefinitionId) : 0);
            }
        }
    }
}
