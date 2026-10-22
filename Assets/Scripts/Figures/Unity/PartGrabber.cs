using System.Collections.Generic;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Захват, перенос, осмотр и защёлкивание деталей.
    ///
    /// Осмотр — не удобство, а ядровая механика: детали взаимозаменяемы, силуэт руки
    /// похож на силуэт другой руки, и опознать владельца можно только разглядев деталь
    /// в руках. Поэтому он вынесен в отдельный режим, а не в приближение камеры.
    ///
    /// Выключенный компонент — детали не берутся: это и есть флаг отключения
    /// подсистемы, сцену и данные ломать не требуется.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class PartGrabber : MonoBehaviour
    {
        public const int ExecutionOrder = 120;

        [SerializeField] private FiguresInputReader input;

        [Tooltip("Камера, из которой идёт луч. Пусто — Camera.main.")]
        [SerializeField] private Camera view;

        [Tooltip("Необязательно. Задан — обзор гасится на время осмотра детали.")]
        [SerializeField] private FirstPersonPlayer player;

        private readonly List<FigureStandView> _stands = new();
        private readonly Dictionary<string, FigureStandView> _standById = new();

        private AssemblyBoard _board;
        private InteractionConfig _config;
        private Transform _looseRoot;

        private PartView _hovered;
        private PartView _held;
        private SocketView _candidate;
        private bool _inspecting;
        private float _inspectDistance;

        public PartView Held => _held;

        public bool HasHeld => _held != null;

        private void Awake()
        {
            if (view == null) view = Camera.main;
            if (input != null && view != null) return;

            DevLog.Info("[Figures] Нет ввода или камеры — захват деталей выключен.");
            enabled = false;
        }

        private void OnDisable()
        {
            if (_config == null) return;

            SetHovered(null);
            SetCandidate(null);
            SetInspecting(false);
        }

        /// <summary>
        /// Загрузчик отдаёт готовое ядро и числа. Своей ссылки на конфиг у захвата нет:
        /// корневой конфиг держит только загрузчик.
        /// </summary>
        public void Bind(AssemblyBoard board, InteractionConfig config,
            IEnumerable<FigureStandView> stands, Transform looseRoot)
        {
            _board = board;
            _config = config;
            _looseRoot = looseRoot;

            _stands.Clear();
            _standById.Clear();

            if (stands == null) return;

            foreach (var stand in stands)
            {
                if (stand == null) continue;

                _stands.Add(stand);
                _standById[stand.FigureId] = stand;
            }
        }

        private void Update()
        {
            if (_board == null || _config == null) return;

            // Пока игрок за компьютером, курсор свободен и щёлкает по кнопкам экрана.
            // Тот же щелчок не должен ещё и хватать деталь со стола.
            if (player != null && player.IsUiMode) return;

            var frame = input.Current;

            if (_held == null)
            {
                Hover(frame);
                return;
            }

            Carry(frame);
        }

        private void Hover(FiguresInputFrame frame)
        {
            SetHovered(Aim());

            if (frame.GrabPressed && _hovered != null) Take(_hovered);
        }

        private void Carry(FiguresInputFrame frame)
        {
            if (frame.InspectHeld)
            {
                if (!_inspecting) BeginInspect();
                Inspect(frame);
            }
            else
            {
                SetInspecting(false);
                Drag(frame);
            }

            if (frame.GrabReleased) Release();
        }

        private void BeginInspect()
        {
            SetInspecting(true);
            _inspectDistance = Mathf.Clamp(
                _config.InspectDistance,
                _config.InspectZoomRange.x,
                _config.InspectZoomRange.y);

            SetCandidate(null);
        }

        /// <summary>
        /// Осмотр и обзор от первого лица спорят за одну мышь: она крутит и деталь, и
        /// голову. Поэтому на время осмотра обзор гасится — иначе деталь поворачивается
        /// вместе с миром, и разглядеть её нельзя.
        /// </summary>
        private void SetInspecting(bool on)
        {
            if (_inspecting == on) return;

            _inspecting = on;
            if (player != null) player.SuppressLook(on);
        }

        private void Inspect(FiguresInputFrame frame)
        {
            if (!Mathf.Approximately(frame.Zoom, 0f))
                _inspectDistance = Mathf.Clamp(
                    _inspectDistance - frame.Zoom * _config.InspectZoomSpeed,
                    _config.InspectZoomRange.x,
                    _config.InspectZoomRange.y);

            var eye = view.transform;
            _held.PlaceAt(eye.position + eye.forward * _inspectDistance, _held.transform.rotation);

            if (frame.PointerDelta.sqrMagnitude <= 0f) return;

            var speed = _config.InspectRotateSpeed;
            _held.transform.Rotate(eye.up, -frame.PointerDelta.x * speed, Space.World);
            _held.transform.Rotate(eye.right, frame.PointerDelta.y * speed, Space.World);
        }

        private void Drag(FiguresInputFrame frame)
        {
            var ray = view.ScreenPointToRay(frame.Pointer);

            // Подъём идёт по нормали, а не вверх: на вертикальной плоскости переноса
            // «вверх» уводило бы деталь от гнезда вместо того, чтобы отодвинуть её
            // от поверхности.
            var point = Physics.Raycast(ray, out var hit, _config.ReachDistance, _config.SurfaceLayers,
                QueryTriggerInteraction.Ignore)
                ? hit.point + hit.normal * _config.CarryLift
                : ray.origin + ray.direction * Mathf.Min(_config.CarryFallbackDistance, _config.ReachDistance);

            _held.PlaceAt(point, _held.transform.rotation);
            SetCandidate(FindSocket(_held, point));
        }

        private void Take(PartView part)
        {
            if (_board.TrySocketOf(part.PartId, out var from) && _board.TryTake(from, out _))
            {
                var socket = SocketOf(from);
                if (socket != null) socket.Occupant = null;
            }

            SetHovered(null);

            part.Hold();
            part.SetHighlighted(false, _config.HoverBoost);
            part.SetPickable(false);
            if (_looseRoot != null) part.transform.SetParent(_looseRoot, worldPositionStays: true);

            _held = part;
            SetInspecting(false);
        }

        private void Release()
        {
            var part = _held;
            _held = null;
            SetInspecting(false);

            var socket = _candidate;
            SetCandidate(null);

            part.SetPickable(true);

            if (socket != null && _board.TryPlace(part.PartId, socket.Ref))
            {
                socket.Occupant = part;
                part.transform.SetParent(socket.transform, worldPositionStays: true);
                part.Hold();
                part.SnapTo(socket.transform.position, socket.transform.rotation, _config.SnapDuration);
                return;
            }

            if (_looseRoot != null) part.transform.SetParent(_looseRoot, worldPositionStays: true);

            // Деталь откреплена: мимо гнезда она падает там, где её отпустили, а не
            // телепортируется на поднос. Возврат остаётся на флаге — он удобен, когда
            // проверяют только читаемость и терять деталь под столом не хочется.
            if (_config.DropOnRelease) part.Drop();
            else part.SnapTo(part.HomePosition, part.HomeRotation, _config.ReturnDuration);
        }

        private PartView Aim()
        {
            var ray = view.ScreenPointToRay(input.Current.Pointer);

            if (!Physics.Raycast(ray, out var hit, _config.ReachDistance, _config.PartLayers,
                    QueryTriggerInteraction.Ignore))
                return null;

            return hit.collider.GetComponentInParent<PartView>();
        }

        /// <summary>
        /// Ближайшее гнездо, которое примет деталь. Перебираются все гнёзда нужного
        /// типа, а не одно: парные гнёзда равноправны, и рука входит в любое плечо.
        /// Чужую деталь гнездо принимает так же, как свою: отказ по владельцу выдал
        /// бы ответ до проверки.
        /// </summary>
        private SocketView FindSocket(PartView part, Vector3 position)
        {
            SocketView best = null;
            var bestSqr = _config.SnapRadius * _config.SnapRadius;

            foreach (var stand in _stands)
                foreach (var socket in stand.Sockets)
                {
                    if (socket == null || socket.Kind != part.Kind || !socket.IsFree) continue;
                    if (!_board.Accepts(part.PartId, socket.Ref)) continue;

                    var sqr = (socket.transform.position - position).sqrMagnitude;
                    if (sqr > bestSqr) continue;

                    bestSqr = sqr;
                    best = socket;
                }

            return best;
        }

        private SocketView SocketOf(SocketRef reference) =>
            _standById.TryGetValue(reference.FigureId ?? string.Empty, out var stand)
                ? stand.Socket(reference.Kind, reference.Index)
                : null;

        private void SetHovered(PartView part)
        {
            if (_hovered == part) return;

            if (_hovered != null) _hovered.SetHighlighted(false, _config.HoverBoost);
            _hovered = part;
            if (_hovered != null) _hovered.SetHighlighted(true, _config.HoverBoost);
        }

        private void SetCandidate(SocketView socket)
        {
            if (_candidate == socket) return;

            if (_candidate != null) _candidate.SetReady(false, _config.SocketReadyColor);
            _candidate = socket;
            if (_candidate != null) _candidate.SetReady(true, _config.SocketReadyColor);
        }
    }
}
