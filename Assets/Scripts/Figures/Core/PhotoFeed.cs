using System;

namespace Figures
{
    /// <summary>
    /// Лента, куда игрок выкладывает фото коллекции. Считает лайки и то, сколько за
    /// них причитается; денег не выдаёт — кошелёк один, и распоряжается им студия.
    ///
    /// Главное правило: **платят за прирост**, а не за факт публикации. Деньги идут
    /// только с той части лайков, которая выше лучшего прежнего фото. Иначе одна
    /// собранная фигурка превращается в кнопку «получить монеты», и весь цикл
    /// вырождается в клики по терминалу.
    /// </summary>
    public sealed class PhotoFeed
    {
        private readonly PhotoRates _rates;

        public PhotoFeed(PhotoRates rates) => _rates = rates ?? PhotoRates.Default;

        public event Action<PhotoPost> Published;

        public int PostCount { get; private set; }

        public int TotalLikes { get; private set; }

        /// <summary>Лучшее фото: планка, выше которой надо прыгнуть, чтобы заработать.</summary>
        public int BestLikes { get; private set; }

        public PhotoPost Last { get; private set; }

        /// <summary>
        /// Сколько лайков соберёт такой снимок. Нужно интерфейсу: игрок должен видеть
        /// цену своей витрины до того, как нажмёт «опубликовать».
        /// </summary>
        public int LikesOf(in CollectionShot shot)
        {
            var likes = shot.Assembled * _rates.PerAssembled
                        + shot.PartsPlaced * _rates.PerPlacedPart
                        - shot.Mistakes * _rates.PerMistake;

            return Math.Max(0, likes);
        }

        public int PayoutOf(in CollectionShot shot)
        {
            var gain = Math.Max(0, LikesOf(shot) - BestLikes);
            return (int)Math.Round(gain * _rates.CoinsPerLike, MidpointRounding.AwayFromZero);
        }

        public PhotoPost Publish(in CollectionShot shot)
        {
            var likes = LikesOf(shot);
            var gain = Math.Max(0, likes - BestLikes);
            var payout = (int)Math.Round(gain * _rates.CoinsPerLike, MidpointRounding.AwayFromZero);

            PostCount++;
            TotalLikes += likes;
            BestLikes = Math.Max(BestLikes, likes);
            Last = new PhotoPost(likes, gain, payout);

            Published?.Invoke(Last);
            return Last;
        }
    }
}
