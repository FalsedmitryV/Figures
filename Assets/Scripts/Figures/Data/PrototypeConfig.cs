using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Корневой конфиг — хаб ссылок. Единственный Unity-класс со ссылкой на него —
    /// загрузчик FiguresBootstrap; остальные получают значения вызовом.
    /// </summary>
    [CreateAssetMenu(menuName = "Figures/Prototype Config", fileName = "PrototypeConfig")]
    public sealed class PrototypeConfig : ScriptableObject
    {
        [Header("Сборка фигурок")]
        [SerializeField] private FigureCatalog catalog;
        [SerializeField] private GreyBoxConfig greyBox;
        [SerializeField] private InteractionConfig interaction;

        [Header("Игрок и комната")]
        [Tooltip("Необязательно. Пусто — игрок работает на встроенных числах.")]
        [SerializeField] private PlayerConfig player;

        [Tooltip("Необязательно. Нужен сборке серой комнаты в редакторе.")]
        [SerializeField] private RoomConfig room;

        [Tooltip("Необязательно. Пусто — игровой цикл работает на встроенных числах.")]
        [SerializeField] private EconomyConfig economy;

        public FigureCatalog Catalog => catalog;

        public GreyBoxConfig GreyBox => greyBox;

        public InteractionConfig Interaction => interaction;

        public PlayerConfig Player => player;

        public RoomConfig Room => room;

        public EconomyConfig Economy => economy;

        /// <summary>Заполнено ли то, без чего не собрать головоломку. Игрока это не касается.</summary>
        public bool IsComplete => catalog != null && greyBox != null && interaction != null;
    }
}
