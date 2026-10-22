using System;
using System.Collections.Generic;

namespace Figures
{
    /// <summary>
    /// Детали, которые у игрока есть. Множество, а не счётчик: id детали в каталоге
    /// уникален, второй такой же руки Рин не существует, и «две штуки» означало бы
    /// деталь без владельца — то есть сломанное опознание.
    ///
    /// Порядок получения сохраняется: по нему вид раскладывает детали и может показать,
    /// что пришло последним.
    /// </summary>
    public sealed class PartInventory
    {
        private readonly HashSet<string> _owned = new();
        private readonly List<string> _order = new();

        public event Action Changed;

        /// <summary>Детали в порядке получения.</summary>
        public IReadOnlyList<string> Owned => _order;

        public int Count => _order.Count;

        public bool Has(string partId) => partId != null && _owned.Contains(partId);

        public bool Add(string partId)
        {
            if (string.IsNullOrEmpty(partId) || !_owned.Add(partId)) return false;

            _order.Add(partId);
            Changed?.Invoke();
            return true;
        }

        public int AddRange(IReadOnlyList<string> partIds)
        {
            if (partIds == null) return 0;

            int added = 0;
            for (int i = 0; i < partIds.Count; i++)
            {
                var partId = partIds[i];
                if (string.IsNullOrEmpty(partId) || !_owned.Add(partId)) continue;

                _order.Add(partId);
                added++;
            }

            if (added > 0) Changed?.Invoke();
            return added;
        }
    }
}
