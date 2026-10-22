using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Figures
{
    /// <summary>
    /// Ячейки внизу экрана. Как и прицел, собираются сами при связке: канвас в сцене не
    /// нужен. Показывают тип и цвет детали — то же, что видно, когда она в руке, — и
    /// сколько деталей на ней собрано.
    ///
    /// Решений здесь нет: что куда кладётся, решает <see cref="Hotbar"/>.
    /// Выключенный компонент — ячеек на экране нет, сам инвентарь работает.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HotbarView : MonoBehaviour
    {
        private const float Inset = 6f;
        private const float LabelSize = 16f;
        private const float SwatchShare = 0.62f;

        [Tooltip("Шрифт с кириллицей. Пусто — встроенный шрифт TMP, а в нём русских букв нет.")]
        [SerializeField] private TMP_FontAsset font;

        [Tooltip("Сторона ячейки, пиксели при разрешении 1920x1080.")]
        [Min(32f)][SerializeField] private float slotSize = 88f;

        [Min(0f)][SerializeField] private float spacing = 10f;

        [Tooltip("Отступ от нижнего края экрана, пиксели при разрешении 1920x1080.")]
        [Min(0f)][SerializeField] private float bottomMargin = 24f;

        [SerializeField] private Color frameColor = new(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color selectedColor = new(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color emptyColor = new(1f, 1f, 1f, 0.08f);

        private Hotbar _hotbar;
        private Func<string, PartView> _viewOf;
        private GameObject _root;
        private Image[] _frames;
        private Image[] _swatches;
        private TextMeshProUGUI[] _labels;

        /// <summary>viewOf — как найти деталь на сцене по id: её цвет и то, что на ней собрано.</summary>
        public void Bind(Hotbar hotbar, Func<string, PartView> viewOf)
        {
            if (_hotbar != null) _hotbar.Changed -= Redraw;
            if (_root != null) Destroy(_root);

            _hotbar = hotbar;
            _viewOf = viewOf;
            _root = null;

            if (_hotbar == null) return;

            _root = Build(_hotbar.Size);
            _root.SetActive(enabled);
            _hotbar.Changed += Redraw;
            Redraw();
        }

        private void OnEnable()
        {
            if (_root != null) _root.SetActive(true);
        }

        private void OnDisable()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_hotbar != null) _hotbar.Changed -= Redraw;
        }

        private void Redraw()
        {
            for (int i = 0; i < _frames.Length; i++)
            {
                _frames[i].color = i == _hotbar.Selected ? selectedColor : frameColor;

                var partId = _hotbar.PartAt(i);
                var part = partId != null ? _viewOf?.Invoke(partId) : null;

                _swatches[i].color = part != null ? part.Tint : emptyColor;
                _labels[i].text = part != null ? Describe(part) : string.Empty;
            }
        }

        private static string Describe(PartView part)
        {
            // Кусок спрятан со сцены, поэтому и неактивные потомки считаются.
            var attached = part.GetComponentsInChildren<PartView>(includeInactive: true).Length - 1;
            var name = KindName(part.Kind);
            return attached > 0 ? $"{name} +{attached}" : name;
        }

        private static string KindName(PartSlotKind kind) => kind switch
        {
            PartSlotKind.Head => "Голова",
            PartSlotKind.Hair => "Причёска",
            PartSlotKind.Face => "Лицо",
            PartSlotKind.Torso => "Торс",
            PartSlotKind.Pelvis => "Таз",
            PartSlotKind.Arm => "Рука",
            PartSlotKind.Leg => "Нога",
            _ => kind.ToString()
        };

        private GameObject Build(int count)
        {
            var canvasGo = new GameObject("Hotbar", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Рейкастера нет намеренно: ячейки не должны перехватывать щелчок, которым
            // берут деталь.
            var width = count * slotSize + (count - 1) * spacing;

            var row = (RectTransform)new GameObject("Row", typeof(RectTransform)).transform;
            row.SetParent(canvasGo.transform, false);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, bottomMargin);
            row.sizeDelta = new Vector2(width, slotSize);

            _frames = new Image[count];
            _swatches = new Image[count];
            _labels = new TextMeshProUGUI[count];

            for (int i = 0; i < count; i++)
            {
                var left = i * (slotSize + spacing) / width;
                var right = (i * (slotSize + spacing) + slotSize) / width;

                _frames[i] = Box($"Slot_{i}", row, new Vector2(left, 0f), new Vector2(right, 1f), 0f);
                _swatches[i] = Box("Swatch", _frames[i].transform,
                    new Vector2(0f, 1f - SwatchShare), Vector2.one, Inset);
                _labels[i] = Label(_frames[i].transform);
            }

            return canvasGo;
        }

        private static Image Box(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float inset)
        {
            var go = new GameObject(name, typeof(Image));
            Stretch((RectTransform)go.transform, parent, anchorMin, anchorMax, inset);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI Label(Transform parent)
        {
            var go = new GameObject("Label", typeof(TextMeshProUGUI));
            Stretch((RectTransform)go.transform, parent, Vector2.zero, new Vector2(1f, 1f - SwatchShare), Inset * 0.5f);

            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = LabelSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(RectTransform rect, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            float inset)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}