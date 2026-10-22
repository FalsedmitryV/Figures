namespace Figures
{
    /// <summary>Деталь без сцены: тип посадки и то, чья она по каталогу.</summary>
    public readonly struct PartSpec
    {
        public readonly string Id;
        public readonly PartSlotKind Kind;
        public readonly string OwnerFigureId;

        public PartSpec(string id, PartSlotKind kind, string ownerFigureId)
        {
            Id = id;
            Kind = kind;
            OwnerFigureId = ownerFigureId;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Id);

        public static PartSpec None => default;
    }
}
