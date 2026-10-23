using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Figures
{
    /// <summary>
    /// Фотокамера в руках игрока: F поднимает видоискатель, ЛКМ снимает, колесо
    /// приближает. Лайки идут только за то, что попало в рамку и ничем не заслонено, —
    /// ракурс и расстояние становятся частью игры, а не формальностью.
    ///
    /// Денег здесь нет: снимок ложится в альбом <see cref="CollectionStudio.Album"/>, а
    /// выкладывают его в компьютере. Камера решает только, какие детали видны.
    ///
    /// Выключенный компонент — фото публикуется кнопкой в терминале, по всей витрине.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhotoCamera : MonoBehaviour
    {
        /// <summary>Насколько далеко от центра детали лежат точки проверки заслона, доля полуразмера.</summary>
        private const float SampleSpread = 0.35f;

        private const int MaxHits = 16;

        private static readonly Vector3[] SampleAxes =
        {
            Vector3.zero, Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };

        [SerializeField] private PlayerInputReader input;

        [Tooltip("Необязательно. Пусто — колесо не приближает.")]
        [SerializeField] private FiguresInputReader pointer;

        [Tooltip("Объектив. Пусто — Camera.main.")]
        [SerializeField] private Camera view;

        [Tooltip("Необязательно. Пока в руках деталь или кусок, камеру не поднять.")]
        [SerializeField] private PartGrabber grabber;

        [Tooltip("Необязательно. Пока игрок за терминалом, камера опущена.")]
        [SerializeField] private FirstPersonPlayer player;

        [Tooltip("Шрифт с кириллицей. Пусто — встроенный шрифт TMP, а в нём русских букв нет.")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Кадр")]
        [Tooltip("Высота рамки — доля высоты экрана.")]
        [Range(0.2f, 1f)][SerializeField] private float frameHeight = 0.72f;

        [Tooltip("Ширина рамки к её высоте.")]
        [Min(0.5f)][SerializeField] private float frameAspect = 4f / 3f;

        [Tooltip("Дальше этого деталь на снимке не разглядеть, метры.")]
        [Min(0.5f)][SerializeField] private float maxDistance = 4f;

        [Tooltip("Что заслоняет деталь от объектива. Триггеры не заслоняют.")]
        [SerializeField] private LayerMask occluders = ~0;

        [Header("Приближение")]
        [Tooltip("Самый узкий угол обзора, градусы.")]
        [Range(5f, 60f)][SerializeField] private float minFieldOfView = 25f;

        [Tooltip("Градусов за щелчок колеса.")]
        [Min(0.5f)][SerializeField] private float zoomStep = 5f;

        [Header("Снимок")]
        [Tooltip("Ширина картинки снимка в альбоме, пиксели.")]
        [Range(128, 1024)][SerializeField] private int printWidth = 384;

        [Header("Отклик")]
        [Min(0f)][SerializeField] private float flashDuration = 0.25f;

        [Tooltip("Сколько висит итог снимка, секунды.")]
        [Min(0f)][SerializeField] private float resultDuration = 3f;

        [SerializeField] private Color dimColor = new(0f, 0f, 0f, 0.45f);

        private readonly StringBuilder _text = new();
        private readonly HashSet<string> _visible = new();
        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];

        private CollectionStudio _studio;
        private AssemblyBoard _board;
        private IReadOnlyList<PartView> _parts = Array.Empty<PartView>();
        private Func<string, bool> _inFrame;
        private PhotoPrints _prints;
        private int _hidden;
        private int _loose;

        private GameObject _overlay;
        private RectTransform _left, _right, _top, _bottom, _frame;
        private TextMeshProUGUI _hint;
        private TextMeshProUGUI _result;
        private Image _flash;
        private CancellationTokenSource _flashCts;

        private Rect _viewport;
        private bool _aiming;
        private float _baseFieldOfView;
        private float _resultUntil;
        private CursorLockMode _lastLock;
        private CollectionShot _shownShot;
        private int _shownBest = -1;
        private int _shownHidden;
        private int _shownLoose;
        private int _shownAlbum = -1;

        private enum Sight
        {
            Outside,
            Hidden,
            Visible
        }

        /// <summary>Видоискатель поднят: щелчок снимает, а не берёт деталь.</summary>
        public bool IsAiming => _aiming;

        /// <summary>Камере есть чем управляться. Без ввода снимать нечем, и фото остаётся за терминалом.</summary>
        public bool CanShoot => enabled && gameObject.activeInHierarchy && input != null;

        /// <summary>Загрузчик отдаёт цикл, доску и детали сцены: по ним камера решает, что в кадре.</summary>
        /// <param name="prints">Куда класть картинки снимков. Пусто — снимки в альбоме без картинок.</param>
        public void Bind(CollectionStudio studio, AssemblyBoard board, IReadOnlyList<PartView> parts,
            PhotoPrints prints)
        {
            _studio = studio;
            _board = board;
            _parts = parts ?? Array.Empty<PartView>();
            _prints = prints;
        }

        private void Awake()
        {
            if (view == null) view = Camera.main;
            _inFrame = _visible.Contains;

            _overlay = Build();
            _overlay.SetActive(false);
        }

        private void OnDisable()
        {
            if (_aiming) Lower();
        }

        private void OnDestroy() => _flashCts?.Cancel();

        private void Update()
        {
            if (input == null || view == null || _studio == null) return;

            var frame = input.Current;

            // Щелчок, которым курсор вернули в захват, — не снимок: игрок ещё не видит кадра.
            var lockChanged = Cursor.lockState != _lastLock;
            _lastLock = Cursor.lockState;

            if (!_aiming)
            {
                if (frame.PhotoPressed && !HandsBusy && !InUi) Raise();
                return;
            }

            if (frame.PhotoPressed || frame.CancelPressed || InUi)
            {
                Lower();
                return;
            }

            Zoom(pointer != null ? pointer.Current.Wheel : 0f);
            PlaceFrame();
            Survey();

            var shot = CollectionShot.Of(_board, _inFrame);
            if (frame.PointerPressed && !lockChanged) Shoot(shot);

            ShowHint(shot);
            if (_resultUntil > 0f && Time.unscaledTime >= _resultUntil) ClearResult();
        }

        private bool HandsBusy => grabber != null && grabber.HandsBusy;

        private bool InUi => player != null && player.IsUiMode;

        private void Raise()
        {
            _aiming = true;
            _baseFieldOfView = view.fieldOfView;
            _lastLock = Cursor.lockState;
            _shownBest = -1;

            ClearResult();
            PlaceFrame();
            _overlay.SetActive(true);
        }

        private void Lower()
        {
            _aiming = false;
            if (view != null) view.fieldOfView = _baseFieldOfView;

            _flashCts?.Cancel();
            if (_flash != null) _flash.color = Color.clear;
            if (_overlay != null) _overlay.SetActive(false);
        }

        private void Zoom(float wheel)
        {
            if (Mathf.Approximately(wheel, 0f)) return;

            var widest = Mathf.Max(_baseFieldOfView, minFieldOfView);
            view.fieldOfView = Mathf.Clamp(view.fieldOfView - Mathf.Sign(wheel) * zoomStep, minFieldOfView, widest);
        }

        private void Shoot(in CollectionShot shot)
        {
            if (shot.IsEmpty)
            {
                SetResult("В кадре ни одной детали с подставки — снимать нечего.");
                return;
            }

            var album = _studio.Album;
            if (!album.TryTake(shot, out var photo))
            {
                SetResult($"Альбом полон ({album.Capacity} снимков) — удали лишние в компьютере.");
                return;
            }

            _prints?.Capture(photo.Id, view, _viewport, printWidth);

            SetResult($"Снимок №{photo.Id} в альбоме ({album.Count} из {album.Capacity}), " +
                      $"{_studio.Feed.LikesOf(shot)} лайков. Выложить — в компьютере.");

            _flashCts?.Cancel();
            _flashCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            Flash(_flashCts).Forget();
        }

        // =====================================================================
        // ЧТО В КАДРЕ
        // =====================================================================

        /// <summary>
        /// Один проход по деталям сцены за кадр: что видно и на подставке — идёт в снимок,
        /// остальное считается только для подсказки, почему оно не идёт.
        /// </summary>
        private void Survey()
        {
            _visible.Clear();
            _hidden = 0;
            _loose = 0;

            foreach (var part in _parts)
            {
                if (part == null || !part.gameObject.activeInHierarchy) continue;

                var sight = SightOf(part);
                if (sight == Sight.Outside) continue;

                var onStand = _board.StandOf(part.PartId) != null;
                if (sight == Sight.Hidden)
                {
                    if (onStand) _hidden++;
                    continue;
                }

                if (onStand) _visible.Add(part.PartId);
                else _loose++;
            }
        }

        /// <summary>
        /// Деталь видна, если её центр внутри рамки и не дальше предела, а хотя бы одна
        /// точка её объёма открыта объективу. Одной точки центра мало: рука перед торсом
        /// закрывает его середину, но не весь торс.
        /// </summary>
        private Sight SightOf(PartView part)
        {
            var bounds = part.Bounds;
            var point = view.WorldToViewportPoint(bounds.center);
            if (point.z <= view.nearClipPlane || point.z > maxDistance || !_viewport.Contains(point))
                return Sight.Outside;

            var origin = view.transform.position;
            var spread = bounds.extents * SampleSpread;

            foreach (var axis in SampleAxes)
                if (IsClear(origin, bounds.center + Vector3.Scale(axis, spread), part)) return Sight.Visible;

            return Sight.Hidden;
        }

        /// <summary>
        /// Заслоняет только то, что нарисовано. Объектив видит отрисовку, а не физику:
        /// невидимая плоскость у подставки, по которой ведут деталь к гнёздам, стоит прямо
        /// в плоскости фигурки и иначе закрывала бы половину деталей.
        /// </summary>
        private bool IsClear(Vector3 origin, Vector3 target, PartView part)
        {
            var delta = target - origin;
            var distance = delta.magnitude;
            if (distance <= Mathf.Epsilon) return true;

            var count = Physics.RaycastNonAlloc(origin, delta / distance, _hits, distance, occluders,
                QueryTriggerInteraction.Ignore);

            Collider nearest = null;
            var nearestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.distance >= nearestDistance || !IsDrawn(hit.collider)) continue;

                nearest = hit.collider;
                nearestDistance = hit.distance;
            }

            return nearest == null || nearest.GetComponentInParent<PartView>() == part;
        }

        private static bool IsDrawn(Collider collider) =>
            collider.TryGetComponent(out Renderer renderer)
                ? renderer.enabled
                : collider.GetComponentInChildren<Renderer>() != null;

        // =====================================================================
        // ВИДОИСКАТЕЛЬ
        // =====================================================================

        /// <summary>
        /// Рамка задаётся в долях экрана — тех же, в которых камера отвечает, где деталь.
        /// Поэтому то, что игрок видит в рамке, и то, что считается, совпадают при любом
        /// разрешении.
        /// </summary>
        private void PlaceFrame()
        {
            var aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
            var height = frameHeight;
            var width = Mathf.Min(1f, height * frameAspect / aspect);

            _viewport = new Rect((1f - width) * 0.5f, (1f - height) * 0.5f, width, height);

            Anchor(_left, new Vector2(0f, 0f), new Vector2(_viewport.xMin, 1f));
            Anchor(_right, new Vector2(_viewport.xMax, 0f), new Vector2(1f, 1f));
            Anchor(_bottom, new Vector2(_viewport.xMin, 0f), new Vector2(_viewport.xMax, _viewport.yMin));
            Anchor(_top, new Vector2(_viewport.xMin, _viewport.yMax), new Vector2(_viewport.xMax, 1f));
            Anchor(_frame, _viewport.min, _viewport.max);
        }

        private void ShowHint(in CollectionShot shot)
        {
            var best = _studio.Feed.BestLikes;
            var album = _studio.Album.Count;
            if (best == _shownBest && album == _shownAlbum && _hidden == _shownHidden && _loose == _shownLoose
                && Same(shot, _shownShot))
                return;

            _shownShot = shot;
            _shownBest = best;
            _shownAlbum = album;
            _shownHidden = _hidden;
            _shownLoose = _loose;

            _text.Clear();
            _text.Append($"В кадре: фигурок {shot.Displayed}, собрано {shot.Assembled}, деталей {shot.PartsPlaced} — ");
            _text.Append($"{_studio.Feed.LikesOf(shot)} лайков; лучшее фото {best}, заплатят {_studio.Feed.PayoutOf(shot)}.");
            _text.AppendLine();

            // Без этой строки деталь, которая в рамке, но не считается, выглядит поломкой.
            if (_hidden > 0 || _loose > 0)
            {
                _text.Append("Не в счёт: ");
                if (_hidden > 0) _text.Append($"заслонено {_hidden}");
                if (_hidden > 0 && _loose > 0) _text.Append(", ");
                if (_loose > 0) _text.Append($"не на подставке {_loose}");
                _text.AppendLine(".");
            }

            _text.Append($"Альбом {album} из {_studio.Album.Capacity}. ");
            _text.Append("ЛКМ — снять, колесо — приблизить, F или Esc — опустить камеру.");
            _hint.SetText(_text);
        }

        private static bool Same(in CollectionShot a, in CollectionShot b) =>
            a.Assembled == b.Assembled && a.Displayed == b.Displayed
            && a.PartsPlaced == b.PartsPlaced && a.Mistakes == b.Mistakes;

        private void SetResult(string text)
        {
            _result.text = text;
            _resultUntil = Time.unscaledTime + resultDuration;
        }

        private void ClearResult()
        {
            _result.text = string.Empty;
            _resultUntil = 0f;
        }

        private async UniTaskVoid Flash(CancellationTokenSource cts)
        {
            var token = cts.Token;

            try
            {
                for (float elapsed = 0f; elapsed < flashDuration; elapsed += Time.unscaledDeltaTime)
                {
                    _flash.color = new Color(1f, 1f, 1f, 1f - elapsed / flashDuration);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                _flash.color = Color.clear;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                // Вспышку гасит тот, кто её отменил: новый снимок уже зажёг свою.
                if (ReferenceEquals(_flashCts, cts)) _flashCts = null;
                cts.Dispose();
            }
        }

        private GameObject Build()
        {
            var canvasGo = new GameObject("Viewfinder", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Рейкастера нет намеренно: видоискатель ничего не ловит.
            var root = canvasGo.transform;
            _left = Fill("Dim Left", root, dimColor).rectTransform;
            _right = Fill("Dim Right", root, dimColor).rectTransform;
            _bottom = Fill("Dim Bottom", root, dimColor).rectTransform;
            _top = Fill("Dim Top", root, dimColor).rectTransform;

            _frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            _frame.SetParent(root, false);
            Edge(_frame, new Vector2(0f, 0f), new Vector2(1f, 0f));
            Edge(_frame, new Vector2(0f, 1f), new Vector2(1f, 1f));
            Edge(_frame, new Vector2(0f, 0f), new Vector2(0f, 1f));
            Edge(_frame, new Vector2(1f, 0f), new Vector2(1f, 1f));

            _hint = Caption("Hint", root, 22f, Color.white, 24f);
            _result = Caption("Result", root, 24f, new Color(1f, 0.92f, 0.6f), 130f);

            _flash = Fill("Flash", root, Color.clear);
            Anchor(_flash.rectTransform, Vector2.zero, Vector2.one);

            return canvasGo;
        }

        private static Image Fill(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Сторона рамки: линия вдоль края, толщиной в пару пикселей при 1080p.</summary>
        private static void Edge(RectTransform frame, Vector2 from, Vector2 to)
        {
            const float thickness = 2f;

            var rect = Fill("Edge", frame, new Color(1f, 1f, 1f, 0.8f)).rectTransform;
            rect.anchorMin = from;
            rect.anchorMax = to;
            rect.sizeDelta = new Vector2(from.x == to.x ? thickness : 0f, from.y == to.y ? thickness : 0f);
            rect.anchoredPosition = Vector2.zero;
        }

        private TextMeshProUGUI Caption(string name, Transform parent, float size, Color color, float lift)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Bottom;
            label.raycastTarget = false;

            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(40f, lift);
            rect.offsetMax = new Vector2(-40f, lift + 96f);

            return label;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
