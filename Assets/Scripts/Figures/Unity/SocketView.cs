using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Гнездо: поза, в которую встаёт деталь, и тип, который гнездо принимает. Висит
    /// либо на подставке — там только гнёзда ног, — либо на детали: на тазу гнёзда ног
    /// и торса, на торсе — рук и головы, на голове — лица и причёски. Никакой подсказки
    /// о владельце гнездо не даёт.
    ///
    /// Занятость здесь не хранится: её знает ядро, а второй источник правды разошёлся бы
    /// с ним на первом же снятом куске.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SocketView : MonoBehaviour
    {
        [SerializeField] private PartSlotKind kind;

        [Tooltip("Номер среди гнёзд того же типа у этого держателя. Пара рук — 0 и 1.")]
        [Min(0)][SerializeField] private int index;

        [Tooltip("Необязательно. Включается, когда гнездо готово принять деталь в руке.")]
        [SerializeField] private Renderer readyMarker;

        private Color _markerBaseColor = Color.white;

        public PartSlotKind Kind => kind;

        public int Index => index;

        /// <summary>Подставка, если гнездо на ней.</summary>
        public FigureStandView Stand { get; internal set; }

        /// <summary>Деталь, если гнездо на ней.</summary>
        public PartView Holder { get; internal set; }

        public SocketRef Ref => Holder != null
            ? SocketRef.OfPart(Holder.PartId, kind, index)
            : SocketRef.OfStand(Stand != null ? Stand.FigureId : null, kind, index);

        private void Awake() => HideMarker();

        /// <summary>Гнездо, собранное кодом, а не в редакторе: у примитива-заглушки.</summary>
        public void Setup(PartSlotKind slotKind, int slotIndex, Renderer marker)
        {
            kind = slotKind;
            index = slotIndex;
            readyMarker = marker;
            HideMarker();
        }

        public void SetReady(bool on, Color color)
        {
            if (readyMarker == null) return;

            readyMarker.enabled = on;
            GreyBoxBuilder.Tint(readyMarker, on ? color : _markerBaseColor);
        }

        private void HideMarker()
        {
            if (readyMarker == null) return;

            _markerBaseColor = GreyBoxBuilder.ReadTint(readyMarker);
            readyMarker.enabled = false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, 0.03f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.05f);
        }
    }
}