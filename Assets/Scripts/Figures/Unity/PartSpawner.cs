using System.Collections.Generic;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Раскладывает детали на подносе — сеткой с дрожанием, а не россыпью: случайные
    /// позиции уводили детали за край подноса, а деталь, которой не видно, игрок взять
    /// не может.
    ///
    /// Ячейка у детали своя и постоянная: она считается по месту детали в каталоге, а
    /// не по порядку выкладывания. Поэтому деталь, пришедшая в коробке, ложится на своё
    /// место и не сдвигает уже лежащие, а раскладка остаётся сравнимой между прогонами.
    ///
    /// Раскладка детерминирована по зерну: проверка читаемости имеет смысл только
    /// тогда, когда разные игроки видят одно и то же.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartSpawner : MonoBehaviour
    {
        /// <summary>Зазор между деталью и столешницей, юниты: физика не любит стыка вплотную.</summary>
        private const float RestLift = 0.002f;

        [Tooltip("Центр области раскладки. Пусто — сам объект.")]
        [SerializeField] private Transform tray;

        [Tooltip("Размер области раскладки по X и Z, юниты. Должна целиком попадать на столешницу.")]
        [SerializeField] private Vector2 traySize = new(1.1f, 0.42f);

        [Tooltip("Одно зерно — одна и та же раскладка у всех игроков.")]
        [SerializeField] private int seed = 1;

        [Tooltip("Разворачивать детали случайно вокруг вертикали.")]
        [SerializeField] private bool randomYaw = true;

        [Tooltip("Дрожание внутри ячейки сетки, доля ячейки. 0 — ровные ряды.")]
        [Range(0f, 0.5f)][SerializeField] private float jitter = 0.15f;

        private readonly List<PartView> _spawned = new();
        private readonly Dictionary<string, int> _cellOf = new();

        public Transform Tray => tray != null ? tray : transform;

        public IReadOnlyList<PartView> Spawned => _spawned;

        /// <summary>Выложить набор деталей заново. Пусто — весь каталог, как до коробок.</summary>
        public IReadOnlyList<PartView> Spawn(PrototypeConfig config, IReadOnlyList<string> partIds = null)
        {
            Clear();
            Add(config, partIds ?? config?.Catalog?.PartIds);
            return _spawned;
        }

        /// <summary>Выложить пришедшие детали, не трогая уже лежащие.</summary>
        public int Deliver(PrototypeConfig config, IReadOnlyList<string> partIds) => Add(config, partIds);

        public void Clear()
        {
            foreach (var view in _spawned)
                if (view != null) Destroy(view.gameObject);

            _spawned.Clear();
        }

        private int Add(PrototypeConfig config, IReadOnlyList<string> partIds)
        {
            if (config == null || config.Catalog == null)
            {
                Debug.LogWarning("[Figures] Нет каталога — раскладывать нечего.", this);
                return 0;
            }

            if (partIds == null || partIds.Count == 0) return 0;

            var catalog = config.Catalog;
            var cells = IndexCells(catalog);
            var columns = Columns(cells);
            var rows = Mathf.Max(1, Mathf.CeilToInt(cells / (float)columns));
            var origin = Tray;

            int added = 0;
            for (int i = 0; i < partIds.Count; i++)
            {
                var spec = catalog.PartOf(partIds[i]);
                if (spec.IsEmpty || !_cellOf.TryGetValue(spec.Id, out var cell)) continue;

                var view = Create(spec, catalog, config, origin, cell, columns, rows);
                if (view == null) continue;

                _spawned.Add(view);
                added++;
            }

            return added;
        }

        private PartView Create(PartSpec spec, FigureCatalog catalog, PrototypeConfig config,
            Transform origin, int cell, int columns, int rows)
        {
            var entry = catalog.EntryOf(spec.Id);
            var prefab = entry?.Prefab;

            var go = prefab != null
                ? Instantiate(prefab)
                : GreyBoxBuilder.BuildPart(spec.Kind, config.GreyBox, spec.Id);

            go.name = spec.Id;
            go.transform.SetParent(origin, worldPositionStays: false);

            // Своё зерно на ячейку, а не общий поток случайных чисел: иначе дрожание и
            // поворот детали зависели бы от того, в какой коробке она пришла.
            var random = new System.Random(seed * 397 + cell);
            var yaw = randomYaw ? (float)random.NextDouble() * 360f : 0f;
            go.transform.SetLocalPositionAndRotation(Cell(cell, columns, rows, random),
                Quaternion.Euler(0f, yaw, 0f));

            if (go.GetComponentInChildren<Collider>(includeInactive: true) == null)
                go.AddComponent<BoxCollider>();

            // Деталь наследует слой подноса: так луч захвата и луч поверхности не
            // ловят друг друга, а слой остаётся настройкой сцены, а не кода.
            SetLayer(go.transform, origin.gameObject.layer);

            Rest(go.transform, origin.position.y);

            if (!go.TryGetComponent(out PartView view)) view = go.AddComponent<PartView>();
            var def = catalog.DefOf(spec.OwnerFigureId);
            view.Bind(spec, def != null ? def.Tint : Color.white);

            return view;
        }

        /// <summary>Место детали в каталоге — это и есть её ячейка на подносе.</summary>
        private int IndexCells(FigureCatalog catalog)
        {
            var ids = catalog.PartIds;
            if (_cellOf.Count == ids.Count) return ids.Count;

            _cellOf.Clear();
            for (int i = 0; i < ids.Count; i++) _cellOf[ids[i]] = i;

            return ids.Count;
        }

        /// <summary>Столбцов столько, чтобы ячейки вышли примерно квадратными.</summary>
        private int Columns(int count)
        {
            if (count <= 1) return 1;

            var ratio = Mathf.Max(0.01f, traySize.x / Mathf.Max(0.01f, traySize.y));
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(count * ratio)), 1, count);
        }

        private Vector3 Cell(int order, int columns, int rows, System.Random random)
        {
            var column = order % columns;
            var row = order / columns;

            var step = new Vector2(traySize.x / columns, traySize.y / rows);
            var x = -traySize.x * 0.5f + step.x * (column + 0.5f);
            var z = -traySize.y * 0.5f + step.y * (row + 0.5f);

            x += ((float)random.NextDouble() - 0.5f) * step.x * jitter * 2f;
            z += ((float)random.NextDouble() - 0.5f) * step.y * jitter * 2f;

            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// Поставить деталь на плоскость подноса, а не утопить её центром в столешницу:
        /// деталь откреплена, и первая же включённая физика выдавила бы её наверх.
        /// Высота считается по рендерерам, поэтому работает и для префаба, и для
        /// примитива-заглушки.
        /// </summary>
        private static void Rest(Transform target, float planeY)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            target.position += Vector3.up * (planeY + RestLift - bounds.min.y);
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
        }

        private void OnDrawGizmosSelected()
        {
            var origin = Tray;
            Gizmos.color = new Color(1f, 0.9f, 0.5f, 0.4f);
            Gizmos.matrix = Matrix4x4.TRS(origin.position, origin.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(traySize.x, 0.01f, traySize.y));
        }
    }
}
