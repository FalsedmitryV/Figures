using System;
using System.Collections.Generic;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Числа игрового цикла: цены коробок, стартовый счёт и во что интернет оценивает
    /// коллекцию. Логики нет — ассет только отдаёт <see cref="EconomySettings"/>,
    /// которые загрузчик передаёт в ядро.
    /// </summary>
    [CreateAssetMenu(menuName = "Figures/Economy", fileName = "EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Serializable]
        public sealed class BoxEntry
        {
            [Tooltip("Служебный id. По нему терминал заказывает коробку.")]
            [SerializeField] private string id = "box";

            [Tooltip("Название на витрине магазина.")]
            [SerializeField] private string title = "Коробка";

            [Min(0)][SerializeField] private int price = 100;

            [Tooltip("Сколько деталей приходит. Повторов не бывает: id детали уникален.")]
            [Min(1)][SerializeField] private int partCount = 5;

            public BoxOffer Offer => new(id, title, price, partCount);
        }

        [Header("Старт")]
        [Min(0)][SerializeField] private int startBalance = 150;

        [Tooltip("Деталей на складе при старте. Ноль — фотографировать нечего, и цикл не начнётся.")]
        [Min(0)][SerializeField] private int starterPartCount = 10;

        [Tooltip("Одно зерно — один и тот же стартовый набор и то же содержимое коробок.")]
        [SerializeField] private int seed = 1;

        [Header("Коробки")]
        [SerializeField] private List<BoxEntry> boxes = new();

        [Header("Лайки и деньги")]
        [Tooltip("Лайков за фигурку, собранную целиком и верно.")]
        [Min(0)][SerializeField] private int likesPerAssembled = 80;

        [Tooltip("Лайков за деталь на подставке. Деталь должна примерно отбивать свою цену в коробке.")]
        [Min(0)][SerializeField] private int likesPerPlacedPart = 12;

        [Tooltip("Штраф за чужую деталь на витрине: он и связывает деньги с опознанием.")]
        [Min(0)][SerializeField] private int likesPerMistake = 20;

        [Tooltip("Монет за лайк прироста. Платят за прирост, а не за факт публикации.")]
        [Min(0f)][SerializeField] private float coinsPerLike = 1.5f;

        [Tooltip("Сколько снимков камеры ждёт в альбоме до выкладки. Полный альбом — сначала удали лишние.")]
        [Min(1)][SerializeField] private int albumCapacity = 8;

        [Header("Склад")]
        [Tooltip("Детали на подносе — только из склада. Снять — выкладывается весь каталог, как до коробок.")]
        [SerializeField] private bool useInventory = true;

        public EconomySettings Settings => new()
        {
            StartBalance = startBalance,
            StarterPartCount = starterPartCount,
            Seed = seed,
            Offers = Offers,
            UseInventory = useInventory,
            AlbumCapacity = albumCapacity,
            Rates = new PhotoRates
            {
                PerAssembled = likesPerAssembled,
                PerPlacedPart = likesPerPlacedPart,
                PerMistake = likesPerMistake,
                CoinsPerLike = coinsPerLike
            }
        };

        private IReadOnlyList<BoxOffer> Offers
        {
            get
            {
                if (boxes.Count == 0) return EconomySettings.Default.Offers;

                var offers = new List<BoxOffer>(boxes.Count);
                foreach (var box in boxes)
                    if (box != null) offers.Add(box.Offer);

                return offers;
            }
        }
    }
}
