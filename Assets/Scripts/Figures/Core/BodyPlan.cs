namespace Figures
{
    /// <summary>
    /// Кто к кому крепится. Дерево фигурки растёт от таза: к тазу — ноги и торс,
    /// к торсу — руки и голова, к голове — лицо и причёска. На подставку встают
    /// только ноги: всё остальное держится на подставке через них.
    ///
    /// Правило по типу, а не по владельцу: чужая рука встаёт в плечо так же, как своя.
    /// </summary>
    public static class BodyPlan
    {
        private const int PairedCount = 2;

        public static bool MountsOnStand(PartSlotKind kind) => kind == PartSlotKind.Leg;

        /// <summary>К детали какого типа крепится деталь. У таза держателя нет — он корень.</summary>
        public static bool TryHolderOf(PartSlotKind kind, out PartSlotKind holder)
        {
            switch (kind)
            {
                case PartSlotKind.Leg:
                case PartSlotKind.Torso:
                    holder = PartSlotKind.Pelvis;
                    return true;
                case PartSlotKind.Arm:
                case PartSlotKind.Head:
                    holder = PartSlotKind.Torso;
                    return true;
                case PartSlotKind.Face:
                case PartSlotKind.Hair:
                    holder = PartSlotKind.Head;
                    return true;
                default:
                    holder = default;
                    return false;
            }
        }

        /// <summary>Сколько гнёзд под детали типа kind у детали типа holder.</summary>
        public static int PortCount(PartSlotKind holder, PartSlotKind kind)
        {
            if (!TryHolderOf(kind, out var expected) || expected != holder) return 0;
            return kind is PartSlotKind.Arm or PartSlotKind.Leg ? PairedCount : 1;
        }
    }
}