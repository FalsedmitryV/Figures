using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Размеры серой комнаты в метрах. Логики нет: по этим числам
    /// <see cref="RoomGreyBoxBuilder"/> ставит коробку, проём и блокаут мебели.
    ///
    /// Масштаб человеческий, не витринный: игрок ростом 1.8 ходит по полу, и всё
    /// остальное считается от него.
    /// </summary>
    [CreateAssetMenu(menuName = "Figures/Room", fileName = "RoomConfig")]
    public sealed class RoomConfig : ScriptableObject
    {
        [Header("Коробка, метры")]
        [Tooltip("Внутренние размеры пола: x — по X, y — по Z.")]
        [SerializeField] private Vector2 innerSize = new(8f, 6f);

        [Min(2.2f)][SerializeField] private float wallHeight = 3f;

        [Min(0.05f)][SerializeField] private float wallThickness = 0.2f;

        [Tooltip("Без перекрытия комнату удобнее смотреть сверху в редакторе.")]
        [SerializeField] private bool buildCeiling = true;

        [Header("Проём")]
        [Tooltip("Стена с проёмом. None — коробка закрыта со всех сторон.")]
        [SerializeField] private RoomWall doorwayWall = RoomWall.South;

        [Min(0.6f)][SerializeField] private float doorwayWidth = 1.2f;

        [Min(1.9f)][SerializeField] private float doorwayHeight = 2.2f;

        [Tooltip("Сдвиг проёма от центра стены, метры.")]
        [SerializeField] private float doorwayOffset;

        [Header("Блокаут")]
        [Tooltip("Стол, полка и ящики: по ним читается масштаб. Выключи — останется пустая коробка.")]
        [SerializeField] private bool buildProps = true;

        [Tooltip("Рабочий стол: x — ширина, y — высота столешницы, z — глубина.")]
        [SerializeField] private Vector3 deskSize = new(1.6f, 0.78f, 0.8f);

        [Tooltip("Компьютер у западной стены: за ним заказывают коробки и публикуют фото.")]
        [SerializeField] private bool buildComputer = true;

        [Tooltip("Компьютерный стол: x — ширина, y — высота столешницы, z — глубина.")]
        [SerializeField] private Vector3 computerDeskSize = new(0.9f, 0.72f, 0.6f);

        [Header("Игрок")]
        [Tooltip("Где игрок появляется: x — по X, y — по Z. Пол на нуле.")]
        [SerializeField] private Vector2 spawnPoint = new(0f, -1.8f);

        [Tooltip("Куда смотрит при старте, градусы. 0 — на север (+Z).")]
        [SerializeField] private float spawnYaw;

        [Header("Материалы")]
        [Tooltip("Необязательно. Пусто — материал примитива по умолчанию.")]
        [SerializeField] private Material floorMaterial;

        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material propMaterial;

        [Header("Свет")]
        [Tooltip("Точечный свет под перекрытием: без него закрытая коробка почти чёрная.")]
        [SerializeField] private bool buildLamp = true;

        [Min(0f)][SerializeField] private float lampIntensity = 3.2f;

        [Min(1f)][SerializeField] private float lampRange = 12f;

        public Vector2 InnerSize => new(Mathf.Max(2f, innerSize.x), Mathf.Max(2f, innerSize.y));
        public float WallHeight => wallHeight;
        public float WallThickness => wallThickness;
        public bool BuildCeiling => buildCeiling;

        public RoomWall DoorwayWall => doorwayWall;
        public float DoorwayWidth => doorwayWidth;
        public float DoorwayHeight => Mathf.Min(doorwayHeight, wallHeight);
        public float DoorwayOffset => doorwayOffset;

        public bool BuildProps => buildProps;
        public Vector3 DeskSize => deskSize;
        public bool BuildComputer => buildComputer;
        public Vector3 ComputerDeskSize => computerDeskSize;

        public Vector3 SpawnPosition => new(spawnPoint.x, 0f, spawnPoint.y);
        public float SpawnYaw => spawnYaw;

        public Material FloorMaterial => floorMaterial;
        public Material WallMaterial => wallMaterial;
        public Material PropMaterial => propMaterial;

        public bool BuildLamp => buildLamp;
        public float LampIntensity => lampIntensity;
        public float LampRange => lampRange;
    }
}
