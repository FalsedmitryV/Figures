namespace Figures
{
    /// <summary>
    /// Во что интернет оценивает коллекцию. Числа рабочие по умолчанию: цикл
    /// «коробка — сборка — фото — деньги» замыкается и без конфига.
    /// </summary>
    public sealed class PhotoRates
    {
        public static PhotoRates Default => new();

        /// <summary>Лайков за фигурку, собранную целиком и верно.</summary>
        public int PerAssembled { get; set; } = 80;

        /// <summary>
        /// Лайков за каждую стоящую на подставке деталь. Подобрано так, чтобы деталь
        /// примерно отбивала свою цену в коробке: иначе каждая покупка — убыток, и
        /// игрок упирается в тупик, когда денег нет, а фото уже не растёт.
        /// </summary>
        public int PerPlacedPart { get; set; } = 12;

        /// <summary>
        /// Штраф за чужую деталь на витрине. Он и связывает экономику с головоломкой:
        /// платят не за количество деталей, а за правильно опознанные.
        /// </summary>
        public int PerMistake { get; set; } = 20;

        /// <summary>Монет за лайк прироста.</summary>
        public float CoinsPerLike { get; set; } = 1.5f;
    }
}
