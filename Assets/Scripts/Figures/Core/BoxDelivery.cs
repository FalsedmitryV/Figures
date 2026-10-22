using System.Collections.Generic;

namespace Figures
{
    /// <summary>Что пришло в коробке: сама коробка, детали и чем кончился заказ.</summary>
    public readonly struct BoxDelivery
    {
        private static readonly string[] Nothing = new string[0];

        public readonly OrderResult Result;
        public readonly BoxOffer Offer;

        /// <summary>
        /// Сколько коробка стоила на самом деле. К концу каталога присылать больше
        /// нечего, коробка приходит неполной, и цена считается по факту — иначе ящик на
        /// шестнадцать деталей берёт полную цену за три.
        /// </summary>
        public readonly int Price;

        public readonly IReadOnlyList<string> Parts;

        public BoxDelivery(OrderResult result, BoxOffer offer, int price, IReadOnlyList<string> parts)
        {
            Result = result;
            Offer = offer;
            Price = price;
            Parts = parts ?? Nothing;
        }

        public bool IsDelivered => Result == OrderResult.Delivered;

        public int Count => Parts?.Count ?? 0;

        /// <summary>Коробка пришла неполной: в каталоге кончаются детали.</summary>
        public bool IsPartial => IsDelivered && Count < Offer.PartCount;

        public static BoxDelivery Failed(OrderResult result, BoxOffer offer, int price = 0) =>
            new(result, offer, price, null);
    }
}
