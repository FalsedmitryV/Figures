using System.Collections.Generic;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Каталог фигурок и деталей. Реализует контракт ядра и держит индексы для
    /// быстрого поиска; вид берёт отсюда же префаб и цвет владельца.
    /// </summary>
    [CreateAssetMenu(menuName = "Figures/Figure Catalog", fileName = "FigureCatalog")]
    public sealed class FigureCatalog : ScriptableObject, IFigureCatalog
    {
        private static readonly PartSlotKind[] NoSlots = new PartSlotKind[0];

        [SerializeField] private List<FigureDef> figures = new();

        private readonly List<string> _figureIds = new();
        private readonly List<string> _partIds = new();
        private readonly Dictionary<string, PartSpec> _parts = new();
        private readonly Dictionary<string, PartEntry> _entries = new();
        private readonly Dictionary<string, PartSlotKind[]> _slots = new();
        private readonly Dictionary<string, FigureDef> _defs = new();

        private bool _built;

        public IReadOnlyList<FigureDef> Definitions => figures;

        public IReadOnlyList<string> FigureIds
        {
            get
            {
                Build();
                return _figureIds;
            }
        }

        public IReadOnlyList<string> PartIds
        {
            get
            {
                Build();
                return _partIds;
            }
        }

        public IReadOnlyList<PartSlotKind> SlotsOf(string figureId)
        {
            Build();
            return _slots.TryGetValue(figureId ?? string.Empty, out var slots) ? slots : NoSlots;
        }

        public PartSpec PartOf(string partId)
        {
            Build();
            return _parts.TryGetValue(partId ?? string.Empty, out var spec) ? spec : PartSpec.None;
        }

        /// <summary>Описание фигурки — вид берёт отсюда цвет. null, если id неизвестен.</summary>
        public FigureDef DefOf(string figureId)
        {
            Build();
            return _defs.TryGetValue(figureId ?? string.Empty, out var def) ? def : null;
        }

        /// <summary>Запись детали — вид берёт отсюда необязательный префаб.</summary>
        public PartEntry EntryOf(string partId)
        {
            Build();
            return _entries.TryGetValue(partId ?? string.Empty, out var entry) ? entry : null;
        }

        /// <summary>Пересобрать индексы после правки ассетов.</summary>
        public void Rebuild()
        {
            _built = false;
            Build();
        }

        private void OnValidate() => _built = false;

        private void OnDisable() => _built = false;

        private void Build()
        {
            if (_built) return;
            _built = true;

            _figureIds.Clear();
            _partIds.Clear();
            _parts.Clear();
            _entries.Clear();
            _slots.Clear();
            _defs.Clear();

            foreach (var def in figures)
            {
                if (def == null) continue;

                var figureId = def.Id;
                if (string.IsNullOrEmpty(figureId) || _defs.ContainsKey(figureId))
                {
                    Debug.LogWarning($"[Figures] Пустой или повторяющийся id фигурки: {def.name}", def);
                    continue;
                }

                _defs[figureId] = def;
                _figureIds.Add(figureId);

                // Повторы типа — это парные гнёзда, а не дубликаты: номер внутри типа
                // различает их, но деталь входит в любое гнездо своего типа.
                var kinds = new List<PartSlotKind>(def.Parts.Count);
                var seen = new Dictionary<PartSlotKind, int>();

                foreach (var entry in def.Parts)
                {
                    if (entry == null) continue;

                    seen.TryGetValue(entry.Kind, out var index);
                    seen[entry.Kind] = index + 1;

                    var partId = FigureDef.PartId(figureId, entry.Kind, index);
                    kinds.Add(entry.Kind);
                    _partIds.Add(partId);
                    _parts[partId] = new PartSpec(partId, entry.Kind, figureId);
                    _entries[partId] = entry;
                }

                _slots[figureId] = kinds.ToArray();
            }
        }
    }
}
