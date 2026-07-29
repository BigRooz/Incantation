using System;

namespace Incantation.Networking.Ritual
{
    public readonly struct RitualSequenceId :
        IEquatable<RitualSequenceId>,
        IComparable<RitualSequenceId>
    {
        public RitualSequenceId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public int CompareTo(RitualSequenceId other) => Value.CompareTo(other.Value);
        public bool Equals(RitualSequenceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RitualSequenceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(RitualSequenceId left, RitualSequenceId right) => left.Equals(right);
        public static bool operator !=(RitualSequenceId left, RitualSequenceId right) => !left.Equals(right);
    }

    public readonly struct RitualTurnSequenceId :
        IEquatable<RitualTurnSequenceId>,
        IComparable<RitualTurnSequenceId>
    {
        public RitualTurnSequenceId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public int CompareTo(RitualTurnSequenceId other) => Value.CompareTo(other.Value);
        public bool Equals(RitualTurnSequenceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RitualTurnSequenceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(RitualTurnSequenceId left, RitualTurnSequenceId right) => left.Equals(right);
        public static bool operator !=(RitualTurnSequenceId left, RitualTurnSequenceId right) => !left.Equals(right);
    }

    public readonly struct RitualPhraseSequenceId :
        IEquatable<RitualPhraseSequenceId>,
        IComparable<RitualPhraseSequenceId>
    {
        public RitualPhraseSequenceId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public int CompareTo(RitualPhraseSequenceId other) => Value.CompareTo(other.Value);
        public bool Equals(RitualPhraseSequenceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RitualPhraseSequenceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(RitualPhraseSequenceId left, RitualPhraseSequenceId right) => left.Equals(right);
        public static bool operator !=(RitualPhraseSequenceId left, RitualPhraseSequenceId right) => !left.Equals(right);
    }

    public readonly struct RitualWordSequenceId :
        IEquatable<RitualWordSequenceId>,
        IComparable<RitualWordSequenceId>
    {
        public RitualWordSequenceId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public int CompareTo(RitualWordSequenceId other) => Value.CompareTo(other.Value);
        public bool Equals(RitualWordSequenceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RitualWordSequenceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(RitualWordSequenceId left, RitualWordSequenceId right) => left.Equals(right);
        public static bool operator !=(RitualWordSequenceId left, RitualWordSequenceId right) => !left.Equals(right);
    }

    public readonly struct RitualOutcomeSequenceId :
        IEquatable<RitualOutcomeSequenceId>,
        IComparable<RitualOutcomeSequenceId>
    {
        public RitualOutcomeSequenceId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public int CompareTo(RitualOutcomeSequenceId other) => Value.CompareTo(other.Value);
        public bool Equals(RitualOutcomeSequenceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RitualOutcomeSequenceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(RitualOutcomeSequenceId left, RitualOutcomeSequenceId right) => left.Equals(right);
        public static bool operator !=(RitualOutcomeSequenceId left, RitualOutcomeSequenceId right) => !left.Equals(right);
    }
}
