using System;

namespace Figures
{
    /// <summary>Счёт игрока. Целые монеты: дробных цен в игре про коробки не бывает.</summary>
    public sealed class Wallet
    {
        public Wallet(int startBalance) => Balance = Math.Max(0, startBalance);

        public event Action Changed;

        public int Balance { get; private set; }

        public int Earned { get; private set; }

        public int Spent { get; private set; }

        public bool CanAfford(int price) => price >= 0 && price <= Balance;

        public void Earn(int amount)
        {
            if (amount <= 0) return;

            Balance += amount;
            Earned += amount;
            Changed?.Invoke();
        }

        public bool TrySpend(int price)
        {
            if (!CanAfford(price)) return false;

            Balance -= price;
            Spent += price;
            Changed?.Invoke();
            return true;
        }
    }
}
