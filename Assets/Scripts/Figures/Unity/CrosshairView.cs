using UnityEngine;
using UnityEngine.UI;

namespace Figures
{
    /// <summary>
    /// Прицел в центре экрана: именно туда смотрит луч захвата, пока курсор в захвате.
    ///
    /// Канвас не нужен в сцене — компонент собирает свой при старте. Это та же заглушка
    /// вместо предусловия, что и у серых примитивов: прицел появляется от одного
    /// компонента на риге, а не от собранной руками иерархии UI, и когда дойдёт дело
    /// до нормального HUD, готовый спрайт подставляется в <c>dot</c>.
    ///
    /// Выключенный компонент — прицела нет.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrosshairView : MonoBehaviour
    {
        [Tooltip("Необязательно. Пусто — прицел собирается из двух штрихов при старте.")]
        [SerializeField] private RectTransform dot;

        [Tooltip("Длина штриха, пиксели при разрешении 1920x1080.")]
        [Min(2f)][SerializeField] private float size = 10f;

        [Min(1f)][SerializeField] private float thickness = 2f;

        [SerializeField] private Color color = new(1f, 1f, 1f, 0.75f);

        [Tooltip("Скрывать прицел, когда курсор отпущен: целится тогда сам указатель.")]
        [SerializeField] private bool hideWithCursor = true;

        private GameObject _root;

        private void Awake()
        {
            _root = dot != null ? dot.gameObject : Build();
            Apply(IsVisible);
        }

        private void OnDisable() => Apply(false);

        private void Update()
        {
            if (_root != null && _root.activeSelf != IsVisible) Apply(IsVisible);
        }

        private bool IsVisible => !hideWithCursor || Cursor.lockState == CursorLockMode.Locked;

        private void Apply(bool on)
        {
            if (_root != null) _root.SetActive(on);
        }

        private GameObject Build()
        {
            var canvasGo = new GameObject("Crosshair", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Без скалера крестик в 10 пикселей на 4K-экране пропадает совсем.
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Рейкастера нет намеренно: прицел ничего не ловит и не должен перехватывать
            // щелчок, которым берут деталь.
            Stroke(canvasGo.transform, "Horizontal", new Vector2(size, thickness));
            Stroke(canvasGo.transform, "Vertical", new Vector2(thickness, size));

            return canvasGo;
        }

        private void Stroke(Transform parent, string name, Vector2 bar)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = bar;
        }
    }
}
