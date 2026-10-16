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

        /// <summary>
        /// A part's interior surfaces (renderers named "TF_PaintInner", like the inside of the TF
        /// Advanced station modules) have their own paint color, kept under the part id with this bit set.
        /// </summary>
        public const ushort InteriorFlag = 0x8000;

        public ushort RealPartId => (ushort)(PartId & ~InteriorFlag);
        public bool IsInterior => (PartId & InteriorFlag) != 0;
        public PartKey Interior => new PartKey(ShipId, (ushort)(PartId | InteriorFlag));
        public PartKey Exterior => new PartKey(ShipId, RealPartId);

        public bool Equals(PartKey other) => ShipId == other.ShipId && PartId == other.PartId;
        public override bool Equals(object obj) => obj is PartKey other && Equals(other);
        public override int GetHashCode() => (int)(ShipId * 397) ^ PartId;
        public override string ToString() => ShipId + ":" + PartId;
    }
}
