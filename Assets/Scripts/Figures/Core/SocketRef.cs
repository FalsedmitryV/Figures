using System;

namespace Figures
{
    /// <summary>
    /// Адрес гнезда: фигурка, тип и номер среди гнёзд того же типа. Номер нужен
    /// только чтобы различать парные гнёзда — на то, какая деталь туда входит,
    /// он не влияет.
    /// </summary>
    public readonly struct SocketRef : IEquatable<SocketRef>
    {
        public readonly string FigureId;
        public readonly PartSlotKind Kind;
        public readonly int Index;

        public SocketRef(string figureId, PartSlotKind kind, int index = 0)
        {
            FigureId = figureId;
            Kind = kind;
            Index = index;
        }

        public bool IsEmpty => string.IsNullOrEmpty(FigureId);

        public bool Equals(SocketRef other) =>
            Kind == other.Kind && Index == other.Index && FigureId == other.FigureId;

        public override bool Equals(object obj) => obj is SocketRef other && Equals(other);

        public override int GetHashCode()
        {
            var hash = ((FigureId?.GetHashCode() ?? 0) * 397) ^ (int)Kind;
            return (hash * 397) ^ Index;
        }

        public override string ToString() => $"{FigureId}:{Kind}:{Index}";
    }
}
