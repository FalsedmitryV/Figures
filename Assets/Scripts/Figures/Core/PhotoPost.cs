namespace Figures
{
    /// <summary>Опубликованное фото: сколько лайков собрало и сколько принесло.</summary>
    public readonly struct PhotoPost
    {
        public readonly int Likes;

        /// <summary>Прирост лайков над лучшим прежним фото. Платят именно за него.</summary>
        public readonly int Gain;

        public readonly int Payout;

        public PhotoPost(int likes, int gain, int payout)
        {
            Likes = likes;
            Gain = gain;
            Payout = payout;
        }

        /// <summary>Фото не лучше прежнего: лайки есть, денег нет.</summary>
        public bool IsRepeat => Payout <= 0;

        public static PhotoPost None => default;
    }
}
