using System;
using System.Collections.Generic;

namespace Figures
{
    /// <summary>
    /// Вся головоломка сборки: какая деталь к какой прикреплена и что стоит на подставках.
    ///
    /// Детали крепятся друг к другу по <see cref="BodyPlan"/>, на подставку встают только
    /// ноги. Собирать можно где угодно — на столе, в руке, на подставке: фигуркой
    /// считается то, что держится на подставке через ноги, а куски вне подставки в счёт
    /// не идут, пока их туда не поставят.
    ///
    /// Посадка и правильность разведены намеренно — деталь своего типа встаёт в любое
    /// свободное гнездо этого типа, а чья она, игра не сообщает до проверки. Из этого
    /// зазора растёт вся загадка.
    ///
    /// Чистый C#: прогоняется тестом без сцены и без единого кадра.
    /// </summary>
    public sealed class AssemblyBoard
    {
        private readonly IFigureCatalog _catalog;
        private readonly FigureAssembly[] _figures;
        private readonly Dictionary<string, FigureAssembly> _byId = new();

        private readonly Dictionary<SocketRef, string> _occupant = new();
        private readonly Dictionary<string, SocketRef> _holderOf = new();
        private readonly Dictionary<string, SocketRef> _mountOf = new();

        private readonly HashSet<string> _visited = new();
        private readonly List<string> _scratch = new();
        private readonly List<string> _walk = new();

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

        /// <summary>Деталь прикрепили или сняли.</summary>
        public event Action Changed;

        public IReadOnlyList<FigureAssembly> Figures => _figures;

        public int SlotTotal { get; }

        /// <summary>Сколько деталей держится на подставках — прямо или через другие детали.</summary>
        public int PlacedCount { get; private set; }

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
                foreach (var figure in _figures)
                    for (int slot = 0; slot < figure.SlotCount; slot++)
                    {
                        var partId = figure.PartAtSlot(slot);
                        if (partId != null && OwnerOf(partId) != figure.FigureId) wrong++;
                    }
                return wrong;
            }
        }

        public FigureAssembly Figure(string figureId) =>
            figureId != null && _byId.TryGetValue(figureId, out var figure) ? figure : null;

        public bool IsFree(SocketRef socket) => !socket.IsEmpty && !_occupant.ContainsKey(socket);

        /// <summary>
        /// Деталь физически входит в это гнездо: тип совпадает, гнездо свободно, держатель
        /// подходит по <see cref="BodyPlan"/>. Про владельца молчит: проверять его здесь —
        /// значит запретить правдоподобную ошибку.
        /// </summary>
        public bool Accepts(string partId, SocketRef socket)
        {
            var part = _catalog.PartOf(partId);
            if (part.IsEmpty || part.Kind != socket.Kind || socket.Index < 0 || !IsFree(socket)) return false;

            return socket.OnStand ? AcceptsOnStand(part, socket) : AcceptsOnPart(part, socket);
        }

        public bool TryAttach(string partId, SocketRef socket)
        {
            if (!Accepts(partId, socket)) return false;

            if (socket.OnStand) _mountOf[partId] = socket;
            else _holderOf[partId] = socket;
            _occupant[socket] = partId;

            Rebuild();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Снять деталь вместе со всем, что на ней держится: она отцепляется от своего
        /// держателя, а её поддерево — от подставок. То, на чём она держалась, остаётся.
        /// </summary>
        public bool Detach(string partId)
        {
            if (string.IsNullOrEmpty(partId)) return false;

            var changed = Unlink(_holderOf, partId);

            _scratch.Clear();
            CollectFrom(partId, _scratch);
            foreach (var id in _scratch) changed |= Unlink(_mountOf, id);

            if (!changed) return false;

            Rebuild();
            Changed?.Invoke();
            return true;
        }

        public bool TryHolderOf(string partId, out SocketRef socket)
        {
            if (partId != null) return _holderOf.TryGetValue(partId, out socket);

            socket = default;
            return false;
        }

        public bool TryMountOf(string partId, out SocketRef socket)
        {
            if (partId != null) return _mountOf.TryGetValue(partId, out socket);

            socket = default;
            return false;
        }

        /// <summary>Деталь, на которой держится весь кусок: та, у которой держателя нет.</summary>
        public string RootOf(string partId)
        {
            while (partId != null && _holderOf.TryGetValue(partId, out var socket)) partId = socket.HolderId;
            return partId;
        }

        /// <summary>На какой подставке стоит кусок с этой деталью. null — кусок вне подставок.</summary>
        public string StandOf(string partId)
        {
            var root = RootOf(partId);
            if (root == null) return null;

            _walk.Clear();
            CollectFrom(root, _walk);
            foreach (var id in _walk)
                if (_mountOf.TryGetValue(id, out var mount)) return mount.HolderId;

            return null;
        }

        /// <summary>Весь кусок, в котором стоит деталь: корень первым, дальше — вниз по креплениям.</summary>
        public void CollectTree(string partId, List<string> into)
        {
            into.Clear();

            var root = RootOf(partId);
            if (root != null) CollectFrom(root, into);
        }

        /// <summary>Чья деталь по каталогу. Нужно для разбора после проверки.</summary>
        public string OwnerOf(string partId) => _catalog.PartOf(partId).OwnerFigureId;

        private bool AcceptsOnStand(PartSpec part, SocketRef socket)
        {
            var figure = Figure(socket.HolderId);
            return figure != null
                   && BodyPlan.MountsOnStand(part.Kind)
                   && socket.Index < figure.CountOf(part.Kind)
                   && !_mountOf.ContainsKey(part.Id)
                   && SameStand(StandOf(part.Id), figure.FigureId);
        }

        private bool AcceptsOnPart(PartSpec part, SocketRef socket)
        {
            var holder = _catalog.PartOf(socket.HolderId);
            return !holder.IsEmpty
                   && socket.Index < BodyPlan.PortCount(holder.Kind, part.Kind)
                   && !_holderOf.ContainsKey(part.Id)
                   && SameStand(StandOf(part.Id), StandOf(holder.Id));
        }

        /// <summary>Два куска на разных подставках в одну фигурку не сращиваются.</summary>
        private static bool SameStand(string a, string b) => a == null || b == null || a == b;

        private void CollectFrom(string partId, List<string> into)
        {
            var start = into.Count;
            into.Add(partId);

            for (int i = start; i < into.Count; i++)
                foreach (var pair in _holderOf)
                    if (pair.Value.HolderId == into[i]) into.Add(pair.Key);
        }

        private bool Unlink(Dictionary<string, SocketRef> links, string partId)
        {
            if (!links.Remove(partId, out var socket)) return false;

            _occupant.Remove(socket);
            return true;
        }

        /// <summary>
        /// Фигурки пересчитываются целиком из креплений: что держится на подставке, то на
        /// ней и стоит. Второго источника правды, который мог бы разойтись с первым, нет.
        /// </summary>
        private void Rebuild()
        {
            foreach (var figure in _figures) figure.Clear();
            _visited.Clear();

            foreach (var pair in _mountOf)
            {
                var figure = Figure(pair.Value.HolderId);
                var root = RootOf(pair.Key);
                if (figure == null || !_visited.Add(root)) continue;

                _scratch.Clear();
                CollectFrom(root, _scratch);
                foreach (var id in _scratch) figure.PutAny(_catalog.PartOf(id).Kind, id);
            }

            int placed = 0;
            foreach (var figure in _figures) placed += figure.FilledCount;
            PlacedCount = placed;
        }
    }
}