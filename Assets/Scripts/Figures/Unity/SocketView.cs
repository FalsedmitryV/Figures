using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Гнездо на подставке: поза, в которую встаёт деталь, и тип, который гнездо
    /// принимает. Никакой подсказки о владельце гнездо не даёт.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SocketView : MonoBehaviour
    {
        [SerializeField] private PartSlotKind kind;

        [Tooltip("Номер среди гнёзд того же типа у этой фигурки. Пара рук — 0 и 1.")]
        [Min(0)][SerializeField] private int index;

        [Tooltip("Необязательно. Включается, когда гнездо готово принять деталь в руке.")]
        [SerializeField] private Renderer readyMarker;

        private Color _markerBaseColor = Color.white;

        public PartSlotKind Kind => kind;

        public int Index => index;

        public FigureStandView Stand { get; internal set; }

        public PartView Occupant { get; internal set; }

        public bool IsFree => Occupant == null;

        public SocketRef Ref => new(Stand != null ? Stand.FigureId : null, kind, index);

        private void Awake()
        {
            if (readyMarker == null) return;

            _markerBaseColor = GreyBoxBuilder.ReadTint(readyMarker);
            readyMarker.enabled = false;
        }

        public void SetReady(bool on, Color color)
        {
            if (readyMarker == null) return;

            readyMarker.enabled = on;
            GreyBoxBuilder.Tint(readyMarker, on ? color : _markerBaseColor);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsFree ? new Color(1f, 1f, 1f, 0.35f) : new Color(0.4f, 1f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.03f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.05f);
        }
    }
}
