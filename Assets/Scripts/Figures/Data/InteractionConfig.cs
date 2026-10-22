using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Числа захвата, защёлкивания и осмотра. Отклик щелчка — половина впечатления
    /// в cozy-игре, поэтому его длительность и подсветка настраиваются, а не вшиты.
    /// </summary>
    [CreateAssetMenu(menuName = "Figures/Interaction", fileName = "InteractionConfig")]
    public sealed class InteractionConfig : ScriptableObject
    {
        [Header("Слои")]
        [Tooltip("Слои деталей: по ним идёт луч захвата.")]
        [SerializeField] private LayerMask partLayers = ~0;

        [Tooltip("Слои поверхностей: по ним ведётся деталь в руке.")]
        [SerializeField] private LayerMask surfaceLayers = ~0;

        [Header("Дотягивание")]
        [Tooltip("Дальность захвата от камеры, метры. Дальше деталь не берётся.")]
        [Min(0.2f)][SerializeField] private float reachDistance = 2.2f;

        [Header("Перенос")]
        [Tooltip("Насколько деталь приподнята над поверхностью, юниты.")]
        [Min(0f)][SerializeField] private float carryLift = 0.04f;

        [Tooltip("Если луч ни во что не попал — держать деталь на этой дистанции от камеры.")]
        [Min(0.1f)][SerializeField] private float carryFallbackDistance = 1.2f;

        [Header("Защёлкивание")]
        [Tooltip("Радиус поиска гнезда вокруг детали, юниты.")]
        [Min(0.01f)][SerializeField] private float snapRadius = 0.12f;

        [Tooltip("Длительность доводки детали до гнезда, секунды.")]
        [Min(0f)][SerializeField] private float snapDuration = 0.12f;

        [Tooltip("Длительность возврата детали на поднос, секунды. Нужна при выключенном dropOnRelease.")]
        [Min(0f)][SerializeField] private float returnDuration = 0.2f;

        [Header("Отпускание")]
        [Tooltip("Деталь откреплена: отпущенная мимо гнезда падает там, где её отпустили. " +
                 "Снять — вернётся на своё место на подносе.")]
        [SerializeField] private bool dropOnRelease = true;

        [Header("Инвентарь")]
        [Tooltip("Сколько ячеек внизу экрана.")]
        [Min(1)][SerializeField] private int hotbarSlots = 6;

        [Header("Осмотр")]
        [Tooltip("Стартовая дистанция от камеры при осмотре, юниты.")]
        [Min(0.05f)][SerializeField] private float inspectDistance = 0.45f;

        [Tooltip("Границы приближения при осмотре, юниты.")]
        [SerializeField] private Vector2 inspectZoomRange = new(0.22f, 0.8f);

        [Min(0f)][SerializeField] private float inspectZoomSpeed = 0.0015f;

        [Tooltip("Градусов на пиксель смещения указателя.")]
        [Min(0f)][SerializeField] private float inspectRotateSpeed = 0.35f;

        [Header("Подсветка")]
        [Tooltip("Множитель яркости детали под курсором.")]
        [Min(1f)][SerializeField] private float hoverBoost = 1.35f;

        [Tooltip("Цвет гнезда, готового принять деталь.")]
        [SerializeField] private Color socketReadyColor = new(1f, 1f, 1f, 0.5f);

        public LayerMask PartLayers => partLayers;
        public LayerMask SurfaceLayers => surfaceLayers;
        public float ReachDistance => reachDistance;
        public bool DropOnRelease => dropOnRelease;
        public int HotbarSlots => hotbarSlots;
        public float CarryLift => carryLift;
        public float CarryFallbackDistance => carryFallbackDistance;
        public float SnapRadius => snapRadius;
        public float SnapDuration => snapDuration;
        public float ReturnDuration => returnDuration;
        public float InspectDistance => inspectDistance;
        public Vector2 InspectZoomRange => inspectZoomRange;
        public float InspectZoomSpeed => inspectZoomSpeed;
        public float InspectRotateSpeed => inspectRotateSpeed;
        public float HoverBoost => hoverBoost;
        public Color SocketReadyColor => socketReadyColor;
    }
}
