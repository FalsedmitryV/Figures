using System;

namespace Figures
{
    /// <summary>
    /// Игровой цикл целиком: заказал коробку — собрал — сфотографировал — получил
    /// деньги на следующую коробку. Один объект, потому что все четыре шага делят
    /// кошелёк и склад, и разводить их по сцене значит потерять, кто кому платит.
    ///
    /// Студия — единственное место, которое тратит и начисляет деньги. Магазин решает
    /// только «какие детали придут», лента — только «сколько лайков это стоит».
    ///
    /// Чистый C#: весь цикл прогоняется тестом без сцены и без единого кадра.
    /// </summary>
    public sealed class CollectionStudio
    {
        private const string StarterOfferId = "starter";

        public CollectionStudio(IFigureCatalog catalog, EconomySettings settings)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            Settings = settings ?? EconomySettings.Default;

            Wallet = new Wallet(Settings.StartBalance);
            Inventory = new PartInventory();
            Feed = new PhotoFeed(Settings.Rates);
            Album = new PhotoAlbum(Settings.AlbumCapacity);
            Shop = new PartBoxShop(catalog, Inventory, Settings.Offers, Settings.Seed);

            Starter = GiveStarterParts(Settings.StarterPartCount);
        }

        /// <summary>Пришла коробка. Вид по этому событию выкладывает новые детали.</summary>
        public event Action<BoxDelivery> BoxDelivered;

        public event Action<PhotoPost> PhotoPublished;

        public EconomySettings Settings { get; }

        public Wallet Wallet { get; }

        public PartInventory Inventory { get; }

        public PartBoxShop Shop { get; }

        public PhotoFeed Feed { get; }

        /// <summary>Снимки камеры, которые ещё можно выложить или удалить.</summary>
        public PhotoAlbum Album { get; }

        /// <summary>Стартовый набор: он уже на складе, но виду нужно знать, что выложить.</summary>
        public BoxDelivery Starter { get; }

        public BoxDelivery Order(string offerId)
        {
            if (!Shop.TryFind(offerId, out var offer))
                return BoxDelivery.Failed(OrderResult.UnknownOffer, BoxOffer.None);

            if (Shop.RemainingCount <= 0)
                return BoxDelivery.Failed(OrderResult.NothingLeft, offer);

            // Цена считается до набора — по размеру пула, а не по вытащенным деталям.
            // Набор расходует случайность, и отказ после него означал бы, что заказ,
            // на который не хватило денег, изменил содержимое следующей коробки.
            var expected = Math.Min(offer.PartCount, Shop.RemainingCount);
            var price = PriceOf(offer, expected);

            if (!Wallet.CanAfford(price))
                return BoxDelivery.Failed(OrderResult.NotEnoughMoney, offer, price);

            var parts = Shop.Pack(offer);
            if (parts.Count == 0)
                return BoxDelivery.Failed(OrderResult.NothingLeft, offer);

            Wallet.TrySpend(price);
            Inventory.AddRange(parts);

            var delivery = new BoxDelivery(OrderResult.Delivered, offer, price, parts);
            BoxDelivered?.Invoke(delivery);
            return delivery;
        }

        public PhotoPost Publish(in CollectionShot shot)
        {
            var post = Feed.Publish(shot);
            if (post.Payout > 0) Wallet.Earn(post.Payout);

            PhotoPublished?.Invoke(post);
            return post;
        }

        /// <summary>
        /// Выложить снимок из альбома. Выплата считается в момент выкладки, а не съёмки:
        /// планка лучшего фото к этому времени могла подняться.
        /// </summary>
        public bool TryPublish(int photoId, out PhotoPost post)
        {
            post = PhotoPost.None;
            if (!Album.TryGet(photoId, out var photo) || photo.IsPublished) return false;

            post = Publish(photo.Shot);
            Album.MarkPublished(photo, post);
            return true;
        }

        /// <summary>Цена по факту: за неполную коробку платят пропорционально.</summary>
        private static int PriceOf(in BoxOffer offer, int delivered)
        {
            if (offer.PartCount <= 0 || delivered >= offer.PartCount) return offer.Price;

            return (int)Math.Ceiling(offer.Price * delivered / (double)offer.PartCount);
        }

        private BoxDelivery GiveStarterParts(int count)
        {
            if (count <= 0) return BoxDelivery.Failed(OrderResult.NothingLeft, BoxOffer.None);

            var offer = new BoxOffer(StarterOfferId, "Стартовый набор", 0, count);
            var parts = Shop.Pack(offer);
            Inventory.AddRange(parts);

            return new BoxDelivery(OrderResult.Delivered, offer, 0, parts);
        }
    }
}
