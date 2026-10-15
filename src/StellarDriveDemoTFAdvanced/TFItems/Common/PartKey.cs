using System;

namespace StellarDriveDemoTFAdvanced.TFItems.Common
{
    /// <summary>A part of a ship (ship id, part id).</summary>
    internal readonly struct PartKey : IEquatable<PartKey>
    {
        public readonly uint ShipId;
        public readonly ushort PartId;

        public PartKey(uint shipId, ushort partId)
        {
            ShipId = shipId;
            PartId = partId;
        }

        public bool Equals(PartKey other) => ShipId == other.ShipId && PartId == other.PartId;
        public override bool Equals(object obj) => obj is PartKey other && Equals(other);
        public override int GetHashCode() => (int)(ShipId * 397) ^ PartId;
        public override string ToString() => ShipId + ":" + PartId;
    }
}
