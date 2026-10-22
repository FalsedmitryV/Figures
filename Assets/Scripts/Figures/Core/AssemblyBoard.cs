using System;
using System.Collections.Generic;

namespace Figures
{
    /// <summary>
    /// Вся головоломка сборки: фигурки, их гнёзда и то, какая деталь где стоит.
    /// Посадка и правильность разведены намеренно — деталь своего типа встаёт в любое
    /// свободное гнездо этого типа, а чья она, игра не сообщает до проверки. Из этого
    /// зазора растёт вся загадка.
    ///
    /// Собранность считается по комплекту, а не по исходным местам: левая рука в
    /// правом плече своей же фигурки — не ошибка.
    ///
    /// Чистый C#: прогоняется тестом без сцены и без единого кадра.
    /// </summary>
    public sealed class AssemblyBoard
    {
        private readonly IFigureCatalog _catalog;
        private readonly FigureAssembly[] _figures;
        private readonly Dictionary<string, FigureAssembly> _byId = new();
        private readonly Dictionary<string, SocketRef> _placedAt = new();

        public AssemblyBoard(IFigureCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

            var ids = catalog.FigureIds;
            var count = ids?.Count ?? 0;
            _figures = new FigureAssembly[count];

            int total = 0;
            for (int i = 0; i < count; i++)
            {
                var figure = new FigureAssembly(ids[i], catalog.SlotsOf(ids[i]));
                _figures[i] = figure;
                _byId[figure.FigureId] = figure;
                total += figure.SlotCount;
            }

            SlotTotal = total;
        }

        /// <summary>Деталь села в гнездо или покинула его.</summary>
        public event Action Changed;

        public IReadOnlyList<FigureAssembly> Figures => _figures;

        public int SlotTotal { get; }

        public int PlacedCount => _placedAt.Count;

        public bool IsFull => PlacedCount == SlotTotal;

        /// <summary>Все гнёзда заполнены и в каждом стоит своя деталь.</summary>
        public bool IsSolved => IsFull && MistakeCount == 0;

        /// <summary>
        /// Сколько деталей стоят не на своей фигурке. Это ответ на проверку, а не
        /// постоянный индикатор: показывать его всё время — значит убить загадку.
        /// </summary>
        public int MistakeCount
        {
            get
            {
                int wrong = 0;
                foreach (var pair in _placedAt)
                    if (_catalog.PartOf(pair.Key).OwnerFigureId != pair.Value.FigureId) wrong++;
                return wrong;
            }
        }

        public FigureAssembly Figure(string figureId) =>
            figureId != null && _byId.TryGetValue(figureId, out var figure) ? figure : null;

        /// <summary>
        /// Деталь физически входит в это гнездо и гнездо свободно. Про владельца молчит:
        /// проверять его здесь — значит запретить правдоподобную ошибку.
        /// </summary>
        public bool Accepts(string partId, SocketRef socket)
        {
            var part = _catalog.PartOf(partId);
            if (part.IsEmpty || part.Kind != socket.Kind) return false;

            var figure = Figure(socket.FigureId);
            return figure != null && figure.IsFree(socket.Kind, socket.Index);
        }

        public bool TryPlace(string partId, SocketRef socket)
        {
            if (!Accepts(partId, socket)) return false;

            if (_placedAt.TryGetValue(partId, out var from))
                Figure(from.FigureId)?.Take(from.Kind, from.Index);

            Figure(socket.FigureId).Put(socket.Kind, socket.Index, partId);
            _placedAt[partId] = socket;

            Changed?.Invoke();
            return true;
        }

        public bool TryTake(SocketRef socket, out string partId)
        {
            partId = Figure(socket.FigureId)?.Take(socket.Kind, socket.Index);
            if (partId == null) return false;

            _placedAt.Remove(partId);
            Changed?.Invoke();
            return true;
        }

        public bool IsPlaced(string partId) => partId != null && _placedAt.ContainsKey(partId);

        public bool TrySocketOf(string partId, out SocketRef socket)
        {
            if (partId != null) return _placedAt.TryGetValue(partId, out socket);

            socket = default;
            return false;
        }

        /// <summary>Чья деталь по каталогу. Нужно для разбора после проверки.</summary>
        public string OwnerOf(string partId) => _catalog.PartOf(partId).OwnerFigureId;
    }
}
