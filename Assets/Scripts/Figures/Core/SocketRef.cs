using System;

namespace Figures
{
    /// <summary>
    /// Адрес гнезда: кто его держит (подставка или деталь), тип детали, которая в него
    /// входит, и номер среди гнёзд того же типа. Номер нужен только чтобы различать
    /// парные гнёзда — на то, какая деталь туда входит, он не влияет.
    /// </summary>
    public readonly struct SocketRef : IEquatable<SocketRef>
    {
        /// <summary>Id фигурки, если гнездо на подставке, или id детали, если на детали.</summary>
        public readonly string HolderId;
        public readonly bool OnStand;
        public readonly PartSlotKind Kind;
        public readonly int Index;

        private SocketRef(string holderId, bool onStand, PartSlotKind kind, int index)
        {
            HolderId = holderId;
            OnStand = onStand;
            Kind = kind;
            Index = index;
        }

        public static SocketRef OfStand(string figureId, PartSlotKind kind, int index = 0) =>
            new(figureId, true, kind, index);

        public static SocketRef OfPart(string partId, PartSlotKind kind, int index = 0) =>
            new(partId, false, kind, index);

        public bool IsEmpty => string.IsNullOrEmpty(HolderId);

        public bool Equals(SocketRef other) =>
            OnStand == other.OnStand && Kind == other.Kind && Index == other.Index && HolderId == other.HolderId;

        public override bool Equals(object obj) => obj is SocketRef other && Equals(other);

        public override int GetHashCode()
        {
            var hash = ((HolderId?.GetHashCode() ?? 0) * 397) ^ (int)Kind;
            hash = (hash * 397) ^ Index;
            return OnStand ? ~hash : hash;
        }

        public override string ToString() => $"{(OnStand ? "stand" : "part")}:{HolderId}:{Kind}:{Index}";
    }
}