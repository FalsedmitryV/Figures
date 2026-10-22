using System.Collections.Generic;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Захват, перенос, сборка в руках и защёлкивание деталей.
    ///
    /// Осмотр — не удобство, а ядровая механика: детали взаимозаменяемы, силуэт руки
    /// похож на силуэт другой руки, и опознать владельца можно только разглядев деталь
    /// в руках. Поэтому он вынесен в отдельный режим, а не в приближение камеры.
    ///
    /// Этот же режим — вторая точка сборки. Кусок висит перед глазами, игрок крутит его
    /// мышью, перебирает колесом ячейки и подносит оттуда детали: что село, остаётся в
    /// куске, а кусок убирается в одну ячейку целиком. Первая точка — подставка, и
    /// правила защёлкивания у них одни и те же, ядровые.
    ///
    /// Детали крепятся друг к другу, а на подставку встают только ноги. Взятая деталь
    /// уносит с собой всё, что на ней держится, поэтому торс с руками и головой можно
    /// собрать на столе и поставить целиком. Защёлкивание работает в обе стороны:
    /// торс садится на таз, а таз в руке — на ноги, уже стоящие на подставке.
    ///
    /// Выключенный компонент — детали не берутся: это и есть флаг отключения
    /// подсистемы, сцену и данные ломать не требуется.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class PartGrabber : MonoBehaviour
    {
        public const int ExecutionOrder = 120;

        /// <summary>
        /// Насколько близко должны оказаться свободные гнездо и деталь после защёлкивания,
        /// чтобы сцепиться заодно: таз, севший на одну ногу, цепляет и вторую.
        /// </summary>
        private const float JoinTolerance = 0.01f;

        /// <summary>
        /// Какую долю отхода от куска занимает деталь, только что достанная из ячейки:
        /// она должна появиться сбоку, а не внутри куска.
        /// </summary>
        private const float FitStartShare = 0.6f;

        [SerializeField] private FiguresInputReader input;

        [Tooltip("Камера, из которой идёт луч. Пусто — Camera.main.")]
        [SerializeField] private Camera view;

        [Tooltip("Необязательно. Задан — обзор гасится на время осмотра детали.")]
        [SerializeField] private FirstPersonPlayer player;

        private readonly List<FigureStandView> _stands = new();
        private readonly Dictionary<string, FigureStandView> _standById = new();
        private readonly List<PartView> _carried = new();
        private readonly List<PartView> _focus = new();
        private readonly List<Link> _links = new();
        private readonly List<string> _tree = new();

        private AssemblyBoard _board;
        private InteractionConfig _config;
        private IReadOnlyList<PartView> _parts = System.Array.Empty<PartView>();
        private Transform _looseRoot;

        private PartView _hovered;
        private PartView _held;
        private PartView _bench;
        private Link _link;
        private bool _lookSuppressed;
        private float _benchDistance;
        private Vector2 _fitOffset;
        private Vector3 _flatten;

        private Hotbar _hotbar;
        private bool _wasBusy;

        public PartView Held => _held;

        /// <summary>
        /// Руки заняты: деталь в руке или кусок в режиме сборки. По этому компьютер
        /// понимает, что E сейчас не про него.
        /// </summary>
        public bool HasHeld => _held != null || _bench != null;

        /// <summary>Идёт сборка в руках: кусок висит перед глазами.</summary>
        public bool IsAssembling => _bench != null;

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
            SetLink(default);
            if (_bench != null) EndBench();
            SetLookSuppressed(false);
        }

        /// <summary>
        /// Загрузчик отдаёт готовое ядро и числа. Своей ссылки на конфиг у захвата нет:
        /// корневой конфиг держит только загрузчик.
        /// </summary>
        public void Bind(AssemblyBoard board, InteractionConfig config, IEnumerable<FigureStandView> stands,
           IReadOnlyList<PartView> parts, Transform looseRoot, Hotbar hotbar)
        {
            _board = board;
            _config = config;
            _hotbar = hotbar;
            _parts = parts ?? System.Array.Empty<PartView>();
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
            // Тот же щелчок не должен ещё и хватать деталь со стола. Кадр, в котором
            // компьютер закрыли, тоже пропускается: E, которой его закрыли, иначе тут же
            // достала бы деталь из ячейки.
            var busy = player != null && player.IsUiMode;
            var wasBusy = _wasBusy;
            _wasBusy = busy;
            if (busy || wasBusy) return;

            var frame = input.Current;

            if (_bench != null)
            {
                Bench(frame);
                return;
            }

            Scroll(frame.Zoom);

            if (_held == null)
            {
                if (frame.StashPressed && TakeFromHotbar()) return;

                Hover(frame);
                return;
            }

            if (frame.InspectPressed)
            {
                BeginBench();
                return;
            }

            if (frame.StashPressed)
            {
                Stash();
                return;
            }

            Carry(frame);
        }

        private void Hover(FiguresInputFrame frame)
        {
            SetHovered(Aim());

            if (frame.GrabPressed)
                DevLog.Info($"[Figures] ЛКМ: под прицелом {(_hovered != null ? _hovered.PartId : "ничего")}.");

            if (frame.GrabPressed && _hovered != null) Take(_hovered);
        }

        private void Carry(FiguresInputFrame frame)
        {
            Drag(frame);

            if (frame.GrabPressed) Release();
        }

        /// <summary>
        /// Режим сборки. Кусок висит перед глазами и никуда не падает: мышь его крутит,
        /// колесо перебирает ячейки, E достаёт из выбранной деталь и кладёт её обратно,
        /// ЛКМ защёлкивает поднесённое.
        ///
        /// Пустая выбранная ячейка превращает E в «убрать кусок»: достать из пустой
        /// ячейки нечего, а убрать собранное — ровно то, чего игрок в этот момент хочет.
        /// </summary>
        private void Bench(FiguresInputFrame frame)
        {
            if (frame.InspectPressed)
            {
                EndBench();
                return;
            }

            if (frame.ZoomModifierHeld) Zoom(frame.Zoom);
            else Scroll(frame.Zoom);

            var eye = view.transform;
            var at = eye.position + eye.forward * _benchDistance;
            _bench.PlaceAt(at, _bench.transform.rotation);

            if (_held == null)
            {
                Turn(frame.PointerDelta, eye);

                if (frame.StashPressed)
                {
                    if (!TakeFromHotbar()) Pocket();
                    return;
                }

                if (frame.GrabPressed) EndBench();
                return;
            }

            Fit(frame.PointerDelta, at, eye);

            // Глубину до гнезда игроку не задать: деталь ходит по экрану. Поэтому на
            // время поиска гнезда расстояние меряется поперёк взгляда.
            _flatten = eye.forward;
            SetLink(Nearest());

            if (frame.StashPressed)
            {
                Stash();
                return;
            }

            if (frame.GrabPressed) Join();
        }

        /// <summary>
        /// Войти в режим сборки: деталь из руки перевешивается перед глазами и остаётся
        /// там сама. Приближение сбрасывается на стартовое — иначе кусок возвращается с
        /// тем масштабом, на котором его разглядывали в прошлый раз.
        /// </summary>
        private void BeginBench()
        {
            _bench = _held;
            _held = null;
            _carried.Clear();

            _benchDistance = Mathf.Clamp(
                _config.InspectDistance,
                _config.InspectZoomRange.x,
                _config.InspectZoomRange.y);
            _fitOffset = Vector2.zero;

            SetHovered(null);
            SetLink(default);
            SetLookSuppressed(true);
            Focus();

            DevLog.Info($"[Figures] Режим сборки: в руках {_bench.PartId}.");
        }

        /// <summary>
        /// Выйти из режима сборки: кусок возвращается в руку, а деталь с примерки — в
        /// ячейку. Когда все ячейки заняты, она падает — терять её в руках нельзя, а
        /// третьей руки у игрока нет.
        /// </summary>
        private void EndBench()
        {
            if (_held != null && !Stash())
            {
                DevLog.Info($"[Figures] {_held.PartId} уронена: убрать её было некуда.");

                SetPickable(true);
                _held.Drop();
                _held = null;
                _carried.Clear();
            }

            _held = _bench;
            _bench = null;
            _focus.Clear();
            _flatten = Vector3.zero;

            SetLink(default);
            SetLookSuppressed(false);

            _held.Hold();
            _held.GetComponentsInChildren(_carried);
            SetPickable(false);

            DevLog.Info($"[Figures] Сборка закончена: в руке {_held.PartId}.");
        }

        /// <summary>Убрать собранный кусок из рук: он занимает одну ячейку целиком.</summary>
        private void Pocket()
        {
            EndBench();
            Stash();
        }

        /// <summary>Приближение куска в руках: колесо с модификатором.</summary>
        private void Zoom(float wheel)
        {
            if (Mathf.Approximately(wheel, 0f)) return;

            _benchDistance = Mathf.Clamp(
                _benchDistance - wheel * _config.InspectZoomSpeed,
                _config.InspectZoomRange.x,
                _config.InspectZoomRange.y);
        }

        /// <summary>
        /// Крутить кусок в руках. Обзор на время сборки погашен: одна мышь не может
        /// одновременно поворачивать деталь и голову.
        /// </summary>
        private void Turn(Vector2 delta, Transform eye)
        {
            if (delta.sqrMagnitude <= 0f) return;

            var speed = _config.InspectRotateSpeed;
            _bench.transform.Rotate(eye.up, -delta.x * speed, Space.World);
            _bench.transform.Rotate(eye.right, delta.y * speed, Space.World);
        }

        /// <summary>
        /// Поднести деталь к куску. Смещение накапливается, а не берётся из позиции
        /// указателя: в захваченном курсоре позиции нет вовсе, он заперт в центре экрана.
        /// </summary>
        private void Fit(Vector2 delta, Vector3 at, Transform eye)
        {
            _fitOffset += delta * _config.FitSpeed;

            var range = _config.FitRange;
            if (_fitOffset.sqrMagnitude > range * range) _fitOffset = _fitOffset.normalized * range;

            _held.PlaceAt(at + eye.right * _fitOffset.x + eye.up * _fitOffset.y, _held.transform.rotation);
        }

        /// <summary>
        /// Защёлкнуть поднесённую деталь в кусок, который игрок держит. Обратная сцепка
        /// меняет корень: таз, на который село то, что было в руках, становится куском.
        /// </summary>
        private void Join()
        {
            if (_link.IsEmpty)
            {
                DevLog.Info($"[Figures] {_held.PartId} мимо гнезда — деталь осталась в руке.");
                return;
            }

            var part = _held;

            // Попутные сцепки ищутся по настоящему расстоянию: допуск у них 1 см, и
            // плоская мерка свела бы в одну точку гнёзда, разнесённые по глубине.
            _flatten = Vector3.zero;

            if (!Attach(part, _link, inHand: true))
            {
                DevLog.Info($"[Figures] {part.PartId} в это гнездо не идёт.");
                return;
            }

            _held = null;
            SetLink(default);
            _carried.Clear();

            var root = ViewOf(_board.RootOf(part.PartId));
            if (root != null) _bench = root;
            Focus();

            DevLog.Info($"[Figures] {part.PartId} села в кусок: в руках {_bench.PartId}.");
        }

        /// <summary>
        /// Гнёзда и обзор от первого лица спорят за одну мышь. На время сборки в руках
        /// обзор гасится — иначе кусок поворачивается вместе с миром, и разглядеть его
        /// нельзя. Ход при этом остаётся.
        /// </summary>
        private void SetLookSuppressed(bool on)
        {
            if (_lookSuppressed == on) return;

            _lookSuppressed = on;
            if (player != null) player.SuppressLook(on);
        }

        /// <summary>
        /// Пересобрать кусок в руках: только его детали считаются «снаружи», когда
        /// ищется гнездо. Иначе деталь, поднесённая к глазам, цеплялась бы за стол и за
        /// подставки на другом конце комнаты.
        /// </summary>
        private void Focus()
        {
            _focus.Clear();
            if (_bench == null) return;

            _bench.GetComponentsInChildren(_focus);
            foreach (var part in _focus) part.SetPickable(false);
        }

        private void Drag(FiguresInputFrame frame)
        {
            var ray = view.ScreenPointToRay(frame.Pointer);

            // Подъём идёт по нормали, а не вверх: на вертикальной плоскости переноса
            // «вверх» уводило бы деталь от гнезда вместо того, чтобы отодвинуть её
            // от поверхности. Луч ловит и детали: торс ведётся по тазу, лежащему на
            // столе, иначе до гнёзд таза не дотянуться.
            var point = Physics.Raycast(ray, out var hit, _config.ReachDistance,
                _config.SurfaceLayers | _config.PartLayers, QueryTriggerInteraction.Ignore)
                ? hit.point + hit.normal * _config.CarryLift
                : ray.origin + ray.direction * Mathf.Min(_config.CarryFallbackDistance, _config.ReachDistance);

            _held.PlaceAt(point, _held.transform.rotation);
            SetLink(Nearest());
        }

        /// <summary>Колесо вниз — следующая ячейка, вверх — предыдущая.</summary>
        private void Scroll(float wheel)
        {
            if (_hotbar == null || Mathf.Approximately(wheel, 0f)) return;

            _hotbar.Scroll(wheel > 0f ? -1 : 1);
        }

        /// <summary>
        /// Убрать кусок из руки в ячейку. Убирается целиком, со всем, что на нём собрано,
        /// и прячется со сцены: в мире его нет, пока его не достанут.
        /// </summary>
        private bool Stash()
        {
            if (_hotbar == null) return false;

            if (!_hotbar.TryStore(_held.PartId))
            {
                DevLog.Info("[Figures] Все ячейки заняты — деталь осталась в руке.");
                return false;
            }

            var part = _held;
            _held = null;
            SetLink(default);
            SetPickable(true);
            _carried.Clear();

            DevLog.Info($"[Figures] {part.PartId} убрана в ячейку.");
            part.Hold();
            part.gameObject.SetActive(false);
            return true;
        }

        private bool TakeFromHotbar()
        {
            if (_hotbar == null || !_hotbar.TryTakeSelected(out var partId)) return false;

            var part = ViewOf(partId);
            if (part == null) return false;

            var eye = view.transform;
            part.gameObject.SetActive(true);

            if (_bench != null)
            {
                // В режиме сборки деталь появляется сбоку от куска, а не внутри него.
                _fitOffset = new Vector2(_config.FitRange * FitStartShare, 0f);
                part.PlaceAt(eye.position + eye.forward * _benchDistance + eye.right * _fitOffset.x,
                    part.transform.rotation);
            }
            else
            {
                // Появляется перед глазами, а под прицел её ставит уже следующий кадр переноса.
                var distance = Mathf.Min(_config.CarryFallbackDistance, _config.ReachDistance);
                part.PlaceAt(eye.position + eye.forward * distance, part.transform.rotation);
            }

            Take(part);
            return true;
        }

        private void Take(PartView part)
        {
            var formerHolder = _board.TryHolderOf(part.PartId, out var from) ? from.HolderId : null;
            _board.Detach(part.PartId);

            SetHovered(null);

            part.Hold();
            part.SetHighlighted(false, _config.HoverBoost);
            part.transform.SetParent(_looseRoot, worldPositionStays: true);

            _held = part;
            _held.GetComponentsInChildren(_carried);
            SetPickable(false);
            DevLog.Info($"[Figures] Взята {part.PartId} (кадр {Time.frameCount}).");
            // То, на чём деталь держалась, могло потерять опору: таз на одной ноге,
            // которую сняли, падает со всем, что на нём собрано.
            if (formerHolder != null) Settle(formerHolder, null, Placement.Stay);
        }

        private void Release()
        {
            DevLog.Info($"[Figures] Отпущена {_held.PartId} (кадр {Time.frameCount}).");

            var part = _held;
            var link = _link;
            _held = null;
            SetLink(default);
            SetPickable(true);

            var attached = !link.IsEmpty && Attach(part, link);
            _carried.Clear();
            if (attached) return;

            // Деталь откреплена: мимо гнезда она падает там, где её отпустили, а не
            // телепортируется на поднос. Возврат остаётся на флаге — он удобен, когда
            // проверяют только читаемость и терять деталь под столом не хочется.
            if (_config.DropOnRelease) part.Drop();
            else part.SnapTo(part.HomePosition, part.HomeRotation, _config.ReturnDuration);
        }

        /// <summary>
        /// Защёлкнуть кусок из руки. Попутные сцепки ищутся по позе, которую кусок займёт,
        /// а не по текущей: таз ещё едет к одной ноге, а вторую уже должен зацепить.
        /// </summary>
        private bool Attach(PartView root, Link link, bool inHand = false)
        {
            var placement = Placement.Align(root.transform, link.Moving, link.Target);

            _links.Clear();
            FindLinks(placement, JoinTolerance, _links);

            if (!_board.TryAttach(link.Part.PartId, link.Socket.Ref)) return false;

            foreach (var extra in _links) _board.TryAttach(extra.Part.PartId, extra.Socket.Ref);

            Settle(root.PartId, root, placement, inHand);
            return true;
        }

        /// <summary>
        /// Разложить кусок по сцене так, как его видит ядро: каждая деталь — в гнезде
        /// держателя, корень — в гнезде подставки, на подставке или на столе. Корню куска
        /// из руки (moved) поза берётся из placement, остальным — текущая.
        ///
        /// inHand — кусок собирают в руках: корень остаётся у игрока и физике не отдаётся.
        /// </summary>
        private void Settle(string partId, PartView moved, Placement placement, bool inHand = false)
        {
            _board.CollectTree(partId, _tree);
            if (_tree.Count == 0) return;

            SettleRoot(_tree[0], moved, placement, inHand);

            for (int i = 1; i < _tree.Count; i++)
            {
                var part = ViewOf(_tree[i]);
                if (part == null || !_board.TryHolderOf(_tree[i], out var holder)) continue;

                var port = SocketOf(holder);
                if (port != null) part.AttachTo(port.transform, _config.SnapDuration);
            }
        }

        private void SettleRoot(string rootId, PartView moved, Placement placement, bool inHand)
        {
            var root = ViewOf(rootId);
            if (root == null) return;

            // Кусок в руках ни на чём не стоит, но и падать не должен: держит его игрок.
            // Поэтому он встаёт как закреплённый — доводка его не отпускает в физику, а
            // перед глазами его всё равно каждый кадр ставит режим сборки.
            if (inHand)
            {
                var sameRoot = root == moved;
                root.SettleAt(_looseRoot,
                    sameRoot ? placement.Position : root.transform.position,
                    sameRoot ? placement.Rotation : root.transform.rotation,
                    0f, anchored: true);
                return;
            }

            var standId = _board.StandOf(rootId);
            var anchored = standId != null;

            if (_board.TryMountOf(rootId, out var mount))
            {
                var socket = SocketOf(mount);
                if (socket != null)
                {
                    var at = socket.transform;
                    root.SettleAt(at, at.position, at.rotation, _config.SnapDuration, anchored: true);
                    return;
                }
            }

            var parent = anchored && _standById.TryGetValue(standId, out var stand) ? stand.transform : _looseRoot;

            if (root == moved)
                root.SettleAt(parent, placement.Position, placement.Rotation, _config.SnapDuration, anchored);
            else
                root.SettleAt(parent, root.transform.position, root.transform.rotation, 0f, anchored);
        }

        private PartView Aim()
        {
            var ray = view.ScreenPointToRay(input.Current.Pointer);

            if (!Physics.Raycast(ray, out var hit, _config.ReachDistance, _config.PartLayers,
                    QueryTriggerInteraction.Ignore))
                return null;

            return hit.collider.GetComponentInParent<PartView>();
        }

        private Link Nearest()
        {
            _links.Clear();
            FindLinks(Placement.Stay, _config.SnapRadius, _links);

            var best = default(Link);
            foreach (var link in _links)
                if (best.IsEmpty || link.Sqr < best.Sqr) best = link;

            return best;
        }

        /// <summary>
        /// Все пары «деталь — гнездо» между куском в руке и остальным миром ближе radius.
        /// Перебираются все гнёзда нужного типа, а не одно: парные гнёзда равноправны, и
        /// рука входит в любое плечо. Чужую деталь гнездо принимает так же, как свою:
        /// отказ по владельцу выдал бы ответ до проверки.
        ///
        /// В режиме сборки «остальной мир» — это только кусок в руках (<see cref="_focus"/>):
        /// подставки и стол на другом конце комнаты в примерке не участвуют.
        /// </summary>
        private void FindLinks(Placement placement, float radius, List<Link> into)
        {
            var limit = radius * radius;

            foreach (var part in _carried)
            {
                var at = placement.Apply(part.transform.position);

                // Свободен от держателя только корень куска: остальные держатся внутри него.
                if (!_board.TryHolderOf(part.PartId, out _))
                    foreach (var other in _parts)
                        if (IsOutside(other))
                            foreach (var port in other.Ports)
                                Consider(part, port, at, port.transform.position, false, limit, into);

                if (_focus.Count == 0 && BodyPlan.MountsOnStand(part.Kind))
                    foreach (var stand in _stands)
                        foreach (var socket in stand.Sockets)
                            if (socket != null)
                                Consider(part, socket, at, socket.transform.position, false, limit, into);

                foreach (var port in part.Ports)
                {
                    var portAt = placement.Apply(port.transform.position);
                    foreach (var other in _parts)
                        if (IsOutside(other) && !_board.TryHolderOf(other.PartId, out _))
                            Consider(other, port, other.transform.position, portAt, true, limit, into);
                }
            }
        }

        private void Consider(PartView part, SocketView socket, Vector3 partAt, Vector3 socketAt, bool reverse,
            float limit, List<Link> into)
        {
            if (socket.Kind != part.Kind) return;

            var delta = socketAt - partAt;

            // Плоская мерка режима сборки: вдоль взгляда расстояние не считается, потому
            // что этой оси у подносимой детали нет — она ходит по экрану.
            if (_flatten != Vector3.zero) delta -= _flatten * Vector3.Dot(delta, _flatten);

            var sqr = delta.sqrMagnitude;
            if (sqr > limit || !_board.Accepts(part.PartId, socket.Ref)) return;

            into.Add(new Link(part, socket, reverse, sqr));
        }

        private bool IsOutside(PartView part) =>
            part != null && part.gameObject.activeInHierarchy && !_carried.Contains(part)
            && (_focus.Count == 0 || _focus.Contains(part));

        public PartView ViewOf(string partId)
        {
            foreach (var part in _parts)
                if (part != null && part.PartId == partId) return part;
            return null;
        }

        private SocketView SocketOf(SocketRef reference)
        {
            if (!reference.OnStand) return ViewOf(reference.HolderId)?.Port(reference.Kind, reference.Index);

            return _standById.TryGetValue(reference.HolderId ?? string.Empty, out var stand)
                ? stand.Socket(reference.Kind, reference.Index)
                : null;
        }

        private void SetPickable(bool on)
        {
            foreach (var part in _carried) part.SetPickable(on);
        }

        private void SetHovered(PartView part)
        {
            if (_hovered == part) return;

            if (_hovered != null) _hovered.SetHighlighted(false, _config.HoverBoost);
            _hovered = part;
            if (_hovered != null) _hovered.SetHighlighted(true, _config.HoverBoost);
        }

        private void SetLink(Link link)
        {
            if (_link.Socket == link.Socket && _link.Part == link.Part) return;

            if (!_link.IsEmpty) _link.Socket.SetReady(false, _config.SocketReadyColor);
            _link = link;
            if (!_link.IsEmpty) _link.Socket.SetReady(true, _config.SocketReadyColor);
        }

        /// <summary>
        /// Пара «деталь — гнездо». Прямая — деталь из руки в гнездо снаружи; обратная —
        /// гнездо на куске в руке, деталь снаружи (таз в руке садится на стоящую ногу).
        /// </summary>
        private readonly struct Link
        {
            public readonly PartView Part;
            public readonly SocketView Socket;
            public readonly bool Reverse;
            public readonly float Sqr;

            public Link(PartView part, SocketView socket, bool reverse, float sqr)
            {
                Part = part;
                Socket = socket;
                Reverse = reverse;
                Sqr = sqr;
            }

            public bool IsEmpty => Socket == null;

            /// <summary>Точка, которая едет вместе с куском в руке.</summary>
            public Transform Moving => Reverse ? Socket.transform : Part.transform;

            /// <summary>Точка снаружи, к которой она едет.</summary>
            public Transform Target => Reverse ? Part.transform : Socket.transform;
        }

        /// <summary>Поза куска в руке после защёлкивания: куда уедет любая его точка.</summary>
        private readonly struct Placement
        {
            private readonly Vector3 _from;
            private readonly Quaternion _fromInverse;

            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            private Placement(Vector3 from, Quaternion fromRotation, Vector3 position, Quaternion rotation)
            {
                _from = from;
                _fromInverse = Quaternion.Inverse(fromRotation);
                Position = position;
                Rotation = rotation;
            }

            public static Placement Stay =>
                new(Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.identity);

            /// <summary>Сдвинуть кусок с корнем root так, чтобы moving совпал с target.</summary>
            public static Placement Align(Transform root, Transform moving, Transform target)
            {
                var rotation = target.rotation * Quaternion.Inverse(moving.rotation) * root.rotation;
                var offset = Quaternion.Inverse(root.rotation) * (moving.position - root.position);
                return new Placement(root.position, root.rotation, target.position - rotation * offset, rotation);
            }

            public Vector3 Apply(Vector3 point) => Position + Rotation * (_fromInverse * (point - _from));
        }
    }
}