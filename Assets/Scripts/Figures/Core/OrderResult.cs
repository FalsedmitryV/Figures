namespace Figures
{
    /// <summary>
    /// Чем кончился заказ коробки. Отказ называется причиной, а не просто false:
    /// «денег не хватает» и «присылать больше нечего» — это разные сообщения игроку и
    /// разные следующие шаги.
    /// </summary>
    public enum OrderResult
    {
        Delivered = 0,
        UnknownOffer = 1,
        NotEnoughMoney = 2,
        NothingLeft = 3
    }
}
