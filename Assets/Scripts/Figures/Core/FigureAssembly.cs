using System;
using System.Collections.Generic;

namespace Figures
{
    /// <summary>
    /// Одна собираемая фигурка. Гнёзд одного типа может быть несколько (две руки,
    /// две ноги), и они равноправны: деталь входит в любое свободное гнездо своего
    /// типа, включая чужую, — ошибка обязана быть возможной, иначе опознание
    /// деталей перестаёт быть загадкой.
    ///
    /// Левая рука в правом плече ошибкой не считается: фигурка собрана, если все
    /// её детали из её комплекта, а не если каждая легла на исходное место.
    /// </summary>
    public sealed class FigureAssembly
    {
        private readonly PartSlotKind[] _slots;
        private readonly string[] _placed;

        public FigureAssembly(string figureId, IReadOnlyList<PartSlotKind> slots)
        {
            if (string.IsNullOrEmpty(figureId))
                throw new ArgumentException("Пустой id фигурки", nameof(figureId));

            FigureId = figureId;

            var count = slots?.Count ?? 0;
            _slots = new PartSlotKind[count];
            for (int i = 0; i < count; i++) _slots[i] = slots[i];
            _placed = new string[count];
        }

        public string FigureId { get; }

        public IReadOnlyList<PartSlotKind> Slots => _slots;

        public int SlotCount => _slots.Length;

        public int FilledCount { get; private set; }

        public bool IsFull => FilledCount == _slots.Length;

        /// <summary>Сколько гнёзд такого типа у фигурки.</summary>
        public int CountOf(PartSlotKind kind)
        {
            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] == kind) count++;
            return count;
        }

        public bool HasSlot(PartSlotKind kind, int index = 0) => IndexOf(kind, index) >= 0;

        public bool IsFree(PartSlotKind kind, int index = 0)
        {
            var i = IndexOf(kind, index);
            return i >= 0 && _placed[i] == null;
        }

        /// <summary>Что стоит в гнезде. null — гнездо свободно или такого гнезда нет.</summary>
        public string PartAt(PartSlotKind kind, int index = 0)
        {
            var i = IndexOf(kind, index);
            return i < 0 ? null : _placed[i];
        }

        /// <summary>Что стоит в гнезде по его порядковому номеру среди всех гнёзд фигурки.</summary>
        public string PartAtSlot(int slot) => slot >= 0 && slot < _placed.Length ? _placed[slot] : null;

        internal bool PutAny(PartSlotKind kind, string partId)
        {
            if (string.IsNullOrEmpty(partId)) return false;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != kind || _placed[i] != null) continue;

                _placed[i] = partId;
                FilledCount++;
                return true;
            }

            return false;
        }

        internal void Clear()
        {
            Array.Clear(_placed, 0, _placed.Length);
            FilledCount = 0;
        }

        /// <summary>Номер гнезда в общем списке по типу и порядковому номеру внутри типа.</summary>
        private int IndexOf(PartSlotKind kind, int index)
        {
            if (index < 0) return -1;

            int seen = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != kind) continue;
                if (seen == index) return i;
                seen++;
            }

            return -1;
        }
    }
}
