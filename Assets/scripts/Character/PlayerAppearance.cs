using System;

namespace Incantation.Character
{
    /// <summary>
    /// Stable cosmetic slot identifiers carried across the network.
    /// Add future slots here without changing the replication mechanism.
    /// </summary>
    public enum AppearanceSlot : ushort
    {
        Skin = 0,
        Hair = 1,
        Beard = 2,
        Moustache = 3,
        Horns = 4,
        Hat = 5,
        RitualPaint = 6,
        Scar = 7,
        FaceAccessory = 8,
        RobeColor = 9
    }

    /// <summary>
    /// One lightweight choice in a player's appearance model.
    /// ValueId is a local presentation-catalog index, never a visual object reference.
    /// </summary>
    [Serializable]
    public struct AppearanceSlotValue : IEquatable<AppearanceSlotValue>
    {
        public AppearanceSlot Slot;
        public int ValueId;

        public AppearanceSlotValue(AppearanceSlot slot, int valueId)
        {
            Slot = slot;
            ValueId = valueId;
        }

        public bool Equals(AppearanceSlotValue other)
        {
            return Slot == other.Slot && ValueId == other.ValueId;
        }

        public override bool Equals(object obj)
        {
            return obj is AppearanceSlotValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((ushort)Slot, ValueId);
        }
    }
}
