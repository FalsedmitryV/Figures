namespace Figures
{
    /// <summary>Коробка в продаже: что написано на витрине магазина, без логики.</summary>
    public readonly struct BoxOffer
    {
        public readonly string Id;
        public readonly string Title;
        public readonly int Price;
        public readonly int PartCount;

        public BoxOffer(string id, string title, int price, int partCount)
        {
            Id = id;
            Title = title;
            Price = price;
            PartCount = partCount;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Id);

        public static BoxOffer None => default;
    }
}
