using System;
using System.Collections.Generic;

namespace Figures
{
    /// <summary>
    /// Магазин коробок с деталями. Решает ровно одно: какие детали приходят в коробке.
    /// Денег он не касается — ими распоряжается тот, у кого кошелёк.
    ///
    /// Коробка не присылает деталь, которая у игрока уже есть: id в каталоге уникален,
    /// и дубликат был бы деталью без владельца. Поэтому пул конечный, и это видно —
    /// <see cref="RemainingCount"/> честно показывает, сколько ещё можно получить.
    /// Когда каталог вырастет до серий, пул перестанет быть узким местом, а правило
    /// останется тем же.
    ///
    /// Чистый C#: прогоняется тестом без сцены.
    /// </summary>
    public sealed class PartBoxShop
    {
        private readonly IFigureCatalog _catalog;
        private readonly PartInventory _inventory;
        private readonly BoxOffer[] _offers;
        private readonly Random _random;
        private readonly List<string> _pool = new();

        public PartBoxShop(IFigureCatalog catalog, PartInventory inventory,
            IReadOnlyList<BoxOffer> offers, int seed)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _random = new Random(seed);

            var count = offers?.Count ?? 0;
            _offers = new BoxOffer[count];
            for (int i = 0; i < count; i++) _offers[i] = offers[i];
        }

        public IReadOnlyList<BoxOffer> Offers => _offers;

        /// <summary>Сколько деталей каталога игроку ещё не принадлежит.</summary>
        public int RemainingCount
        {
            get
            {
                var ids = _catalog.PartIds;
                int left = 0;
                for (int i = 0; i < ids.Count; i++)
                    if (!_inventory.Has(ids[i])) left++;
                return left;
            }
        }

        public bool TryFind(string offerId, out BoxOffer offer)
        {
            for (int i = 0; i < _offers.Length; i++)
                if (_offers[i].Id == offerId)
                {
                    offer = _offers[i];
                    return true;
                }

            offer = BoxOffer.None;
            return false;
        }

        /// <summary>
        /// Набрать содержимое коробки. Выбор случайный по зерну: раскладка и содержимое
        /// обязаны повторяться между прогонами, иначе два теста читаемости не сравнить.
        /// </summary>
        public List<string> Pack(in BoxOffer offer)
        {
            FillPool();

            var take = Math.Min(offer.PartCount, _pool.Count);
            var packed = new List<string>(take);

            for (int i = 0; i < take; i++)
            {
                var index = _random.Next(_pool.Count);
                packed.Add(_pool[index]);

                // Из пула убирается перестановкой с хвостом: порядок пула не важен,
                // а сдвиг списка на каждой детали — нет.
                _pool[index] = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }

            return packed;
        }

        private void FillPool()
        {
            _pool.Clear();

            var ids = _catalog.PartIds;
            for (int i = 0; i < ids.Count; i++)
                if (!_inventory.Has(ids[i])) _pool.Add(ids[i]);
        }
    }
}
