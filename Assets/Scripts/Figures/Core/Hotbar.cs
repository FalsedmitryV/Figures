using System;

namespace Figures
{
    /// <summary>
    /// Ячейки под рукой: куда игрок убирает деталь из руки и откуда достаёт её обратно.
    /// Хранит id корня куска — всё, что на нём собрано, убирается вместе с ним.
    ///
    /// Не путать с <see cref="PartInventory"/>: тот — склад купленного, этот — карманы.
    /// Чистый C#: прогоняется тестом без сцены и без единого кадра.
    /// </summary>
    public sealed class Hotbar
    {
        private readonly string[] _slots;

        public Hotbar(int size)
        {
            if (size < 1) throw new ArgumentOutOfRangeException(nameof(size));
            _slots = new string[size];
        }

        /// <summary>Сменилось содержимое ячеек или выбранная ячейка.</summary>
        public event Action Changed;

        public int Size => _slots.Length;

        public int Selected { get; private set; }

        public string PartAt(int slot) => slot >= 0 && slot < _slots.Length ? _slots[slot] : null;

        public bool Contains(string partId) => partId != null && Array.IndexOf(_slots, partId) >= 0;

        /// <summary>Сдвинуть выбор на steps ячеек по кругу.</summary>
        public void Scroll(int steps)
        {
            if (steps == 0) return;

            var next = ((Selected + steps) % _slots.Length + _slots.Length) % _slots.Length;
            if (next == Selected) return;

            Selected = next;
            Changed?.Invoke();
        }

        /// <summary>Убрать деталь: в выбранную ячейку, если она пуста, иначе в первую пустую.</summary>
        public bool TryStore(string partId)
        {
            if (string.IsNullOrEmpty(partId) || Contains(partId)) return false;

            var slot = _slots[Selected] == null ? Selected : Array.IndexOf(_slots, null);
            if (slot < 0) return false;

            _slots[slot] = partId;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Достать то, что лежит в выбранной ячейке.</summary>
        public bool TryTakeSelected(out string partId)
        {
            partId = _slots[Selected];
            if (partId == null) return false;

            _slots[Selected] = null;
            Changed?.Invoke();
            return true;
        }
    }
}