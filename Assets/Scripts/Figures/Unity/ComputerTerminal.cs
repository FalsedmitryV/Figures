using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Figures
{
    /// <summary>
    /// Компьютер в комнате: заказ коробок с деталями и публикация фото коллекции.
    /// Экран — такой же грейбокс, как и всё остальное: собирается кодом при связке и
    /// словами рассказывает, что делает каждая кнопка. Проверять надо сам цикл, а не
    /// его оформление.
    ///
    /// Решений здесь нет: цены, лайки и деньги считает <see cref="CollectionStudio"/>.
    /// Терминал читает его состояние, показывает и передаёт нажатия.
    ///
    /// Выключенный компонент — компьютер в комнате стоит, но не включается.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComputerTerminal : MonoBehaviour
    {
        private const float PanelWidth = 860f;
        private const float Padding = 28f;

        [SerializeField] private PlayerInputReader input;

        [Tooltip("Кому отдавать курсор и у кого отнимать ход, пока игрок за компьютером.")]
        [SerializeField] private FirstPersonPlayer player;

        [Tooltip("Необязательно. С деталью в руке E убирает её в ячейку, а не включает компьютер.")]
        [SerializeField] private PartGrabber grabber;

        [Tooltip("С какого расстояния компьютер включается, метры.")]
        [Min(0.5f)][SerializeField] private float range = 1.9f;

        [Tooltip("Шрифт с кириллицей. Пусто — встроенный шрифт TMP, а в нём русских букв нет.")]
        [SerializeField] private TMP_FontAsset font;

        private readonly StringBuilder _text = new();

        private CollectionStudio _studio;
        private AssemblyBoard _board;

        private GameObject _screen;
        private TextMeshProUGUI _state;
        private TextMeshProUGUI _photo;
        private TextMeshProUGUI _status;

        private float _cursor;
        private bool _open;
        private bool _dirty;

        public bool IsOpen => _open;

        /// <summary>
        /// Загрузчик отдаёт готовый цикл и доску сборки: своей ссылки на конфиг у
        /// терминала нет. Доска нужна, чтобы снять с витрины снимок для фото.
        ///
        /// Экран собирается здесь, а не в Awake: его содержимое — это коробки из
        /// магазина, и до связки их список неизвестен.
        /// </summary>
        public void Bind(CollectionStudio studio, AssemblyBoard board)
        {
            _studio = studio;
            _board = board;

            if (_studio != null)
            {
                _studio.Wallet.Changed += MarkDirty;
                _studio.Inventory.Changed += MarkDirty;
            }

            if (_board != null) _board.Changed += MarkDirty;

            if (_screen != null) Destroy(_screen);
            _screen = _studio != null ? Build() : null;
            _screen?.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_studio != null)
            {
                _studio.Wallet.Changed -= MarkDirty;
                _studio.Inventory.Changed -= MarkDirty;
            }

            if (_board != null) _board.Changed -= MarkDirty;
        }

        private void OnDisable()
        {
            if (_open) Close();
        }

        private void Update()
        {
            if (input == null) return;

            var frame = input.Current;

            if (_open)
            {
                if (frame.InteractPressed || frame.CancelPressed) Close();
                else if (_dirty) Refresh();

                return;
            }

            if (frame.InteractPressed && IsNear && !HandsBusy) Open();
        }

        private bool HandsBusy => grabber != null && grabber.HasHeld;

        private bool IsNear
        {
            get
            {
                if (player == null) return false;

                var delta = player.transform.position - transform.position;
                return delta.sqrMagnitude <= range * range;
            }
        }

        private void MarkDirty() => _dirty = true;

        private void Open()
        {
            if (_screen == null)
            {
                DevLog.Info("[Figures] Терминал не связан с игровым циклом — включать нечего.");
                return;
            }

            _open = true;
            _screen.SetActive(true);
            if (player != null) player.SetUiMode(true);

            Refresh();
        }

        private void Close()
        {
            _open = false;
            if (_screen != null) _screen.SetActive(false);
            if (player != null) player.SetUiMode(false);
        }

        // =====================================================================
        // ДЕЙСТВИЯ
        // =====================================================================

        private void OnOrderBox(string offerId)
        {
            var delivery = _studio.Order(offerId);

            _status.text = delivery.Result switch
            {
                OrderResult.Delivered when delivery.IsPartial =>
                    $"«{delivery.Offer.Title}» пришла неполной: {delivery.Count} деталей за " +
                    $"{delivery.Price} монет — в каталоге кончаются детали.",
                OrderResult.Delivered =>
                    $"Доставлено: {delivery.Count} деталей из «{delivery.Offer.Title}» за " +
                    $"{delivery.Price} монет. Они уже на подносе.",
                OrderResult.NotEnoughMoney =>
                    $"Не хватает {delivery.Price - _studio.Wallet.Balance} монет — сфотографируй коллекцию.",
                OrderResult.NothingLeft =>
                    "Присылать больше нечего: все детали каталога уже у тебя.",
                _ => "Такой коробки нет в продаже."
            };

            Refresh();
        }

        private void OnPublishPhoto()
        {
            var post = _studio.Publish(CollectionShot.Of(_board));

            _status.text = post.IsRepeat
                ? $"{post.Likes} лайков — не лучше прежнего фото ({_studio.Feed.BestLikes}). " +
                  "За повтор не платят."
                : $"+{post.Likes} лайков, прирост {post.Gain} — на счёт {post.Payout} монет.";

            Refresh();
        }

        // =====================================================================
        // ЭКРАН
        // =====================================================================

        private void Refresh()
        {
            _dirty = false;
            if (_studio == null) return;

            var shot = CollectionShot.Of(_board);
            var rates = _studio.Settings.Rates;

            _text.Clear();
            _text.AppendLine($"Баланс: {_studio.Wallet.Balance} монет.");
            _text.Append($"Склад: {_studio.Inventory.Count} деталей, в каталоге осталось " +
                         $"{_studio.Shop.RemainingCount}.");
            _state.text = _text.ToString();

            _text.Clear();
            _text.AppendLine($"Лайки: собранная фигурка +{rates.PerAssembled}, деталь на подставке " +
                             $"+{rates.PerPlacedPart}, чужая деталь −{rates.PerMistake}.");
            _text.AppendLine($"На витрине: собрано {shot.Assembled}, деталей {shot.PartsPlaced}, " +
                             $"чужих {shot.Mistakes}.");
            _text.Append($"Это {_studio.Feed.LikesOf(shot)} лайков; лучшее фото — {_studio.Feed.BestLikes}, " +
                         $"заплатят {_studio.Feed.PayoutOf(shot)} монет.");
            _photo.text = _text.ToString();
        }

        private GameObject Build()
        {
            var canvasGo = new GameObject("Terminal Screen", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = Panel(canvasGo.transform);

            // Ряды выкладываются сверху вниз вручную: группы компоновки здесь дали бы
            // зависимость от порядка проходов, а экран собирается один раз и его
            // содержимое известно целиком.
            _cursor = Padding;

            Label("КОЛЛЕКЦИОННЫЙ ТЕРМИНАЛ", panel, 30f, new Color(0.95f, 0.95f, 1f), 40f);
            _state = Label(string.Empty, panel, 22f, Color.white, 62f);

            Label("ЗАКАЗ КОРОБОК — приходят случайные детали, которых у тебя ещё нет. " +
                  "Деньги списываются сразу, детали появляются на подносе.",
                panel, 18f, new Color(0.72f, 0.74f, 0.8f), 48f);

            foreach (var offer in _studio.Shop.Offers)
            {
                var id = offer.Id;
                Button($"{offer.Title} — {offer.Price} монет, {offer.PartCount} деталей",
                    panel, () => OnOrderBox(id));
            }

            Label("ФОТО КОЛЛЕКЦИИ — витрина уходит в интернет, лайки превращаются в деньги. " +
                  "Платят только за прирост над лучшим прежним фото, поэтому жать повторно бессмысленно.",
                panel, 18f, new Color(0.72f, 0.74f, 0.8f), 66f);

            _photo = Label(string.Empty, panel, 20f, Color.white, 84f);
            Button("Опубликовать фото коллекции", panel, OnPublishPhoto);

            _status = Label(string.Empty, panel, 20f, new Color(1f, 0.92f, 0.6f), 56f);
            Label("E или Esc — выйти из терминала.", panel, 16f, new Color(0.6f, 0.62f, 0.68f), 22f);

            panel.sizeDelta = new Vector2(PanelWidth, _cursor + Padding);

            EnsureEventSystem();
            return canvasGo;
        }

        private static RectTransform Panel(Transform parent)
        {
            var go = new GameObject("Panel", typeof(Image));
            go.transform.SetParent(parent, false);

            go.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.93f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(PanelWidth, 600f);

            return rect;
        }

        /// <summary>Ряд во всю ширину панели на текущей высоте; курсор уезжает вниз.</summary>
        private RectTransform Row(string name, Transform parent, float height, params System.Type[] parts)
        {
            var go = new GameObject(name, parts);
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(Padding, -(_cursor + height));
            rect.offsetMax = new Vector2(-Padding, -_cursor);

            _cursor += height + 8f;
            return rect;
        }

        private TextMeshProUGUI Label(string text, Transform parent, float size, Color color, float height)
        {
            var rect = Row("Label", parent, height, typeof(TextMeshProUGUI));

            var label = rect.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.raycastTarget = false;

            return label;
        }

        private void Button(string text, Transform parent, UnityEngine.Events.UnityAction action)
        {
            var rect = Row("Button", parent, 46f, typeof(Image), typeof(Button));

            var image = rect.GetComponent<Image>();
            image.color = new Color(0.2f, 0.24f, 0.32f, 1f);

            var button = rect.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var labelGo = new GameObject("Label", typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(rect, false);

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = 20f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14f, 0f);
            labelRect.offsetMax = new Vector2(-14f, 0f);
        }

        /// <summary>
        /// Без EventSystem кнопки на экране не нажимаются вовсе, а в собранной меню
        /// сцене её нет: весь интерфейс здесь — код. Поэтому терминал заводит её сам,
        /// если никто другой этого не сделал.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
