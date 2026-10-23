using System.Collections.Generic;

namespace Figures
{
    /// <summary>
    /// Числа игрового цикла. Значения по умолчанию рабочие: цикл замыкается в сцене
    /// без конфига, а <see cref="EconomyConfig"/> их только переопределяет.
    /// </summary>
    public sealed class EconomySettings
    {
        private static readonly BoxOffer[] DefaultOffers =
        {
            new("small", "Малая посылка", 60, 3),
            new("standard", "Стандартная коробка", 140, 8),
            new("collector", "Ящик коллекционера", 260, 16)
        };

        public static EconomySettings Default => new();

        public int StartBalance { get; set; } = 150;

        /// <summary>
        /// Сколько деталей у игрока на старте. Не ноль: с пустой витрины нечего
        /// фотографировать, а значит не на что заказать первую коробку.
        /// </summary>
        public int StarterPartCount { get; set; } = 10;

        /// <summary>Одно зерно — один и тот же стартовый набор и то же содержимое коробок.</summary>
        public int Seed { get; set; } = 1;

        public IReadOnlyList<BoxOffer> Offers { get; set; } = DefaultOffers;

        public PhotoRates Rates { get; set; } = PhotoRates.Default;

        /// <summary>Сколько снимков помещается в альбом до выкладки.</summary>
        public int AlbumCapacity { get; set; } = 8;

        /// <summary>
        /// Детали на подносе — только из склада игрока. Снять флаг — на поднос
        /// выкладывается весь каталог, как было до коробок: так проверяют читаемость,
        /// не играя в экономику.
        /// </summary>
        public bool UseInventory { get; set; } = true;
    }
}
