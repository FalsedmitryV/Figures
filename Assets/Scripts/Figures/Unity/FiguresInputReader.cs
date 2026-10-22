using UnityEngine;
using UnityEngine.InputSystem;

namespace Figures
{
    /// <summary>
    /// Единственное место, где игра знает про устройства ввода. Отдаёт кадр
    /// <see cref="FiguresInputFrame"/>, дальше решает логика.
    ///
    /// Карта берётся из ассета привязок: все клавиши лежат в одном месте, иначе
    /// коллизию двух действий на одной кнопке не видно.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class FiguresInputReader : MonoBehaviour
    {
        public const int ExecutionOrder = -200;

        private const string PointActionName = "Point";
        private const string GrabActionName = "Click";
        private const string InspectActionName = "RightClick";
        private const string ZoomActionName = "ScrollWheel";
        private const string StashActionName = "Interact";

        [Tooltip("Ассет со всеми привязками. Пусто — ввода не будет.")]
        [SerializeField] private InputActionAsset actions;

        [SerializeField] private string actionMapName = "UI";

        [Tooltip("Карта, из которой берётся действие «убрать в ячейку / достать».")]
        [SerializeField] private string stashMapName = "Player";

        private InputActionMap _map;
        private InputAction _point;
        private InputAction _grab;
        private InputAction _inspect;
        private InputAction _zoom;
        private InputAction _stash;

        private Vector2 _lastPointer;
        private bool _hasLastPointer;

        public FiguresInputFrame Current { get; private set; }

        private void Awake()
        {
            if (actions == null)
            {
                DevLog.Info("[Figures] Ассет привязок не задан — ввод выключен.");
                return;
            }

            _map = actions.FindActionMap(actionMapName, false);
            if (_map == null)
            {
                Debug.LogWarning($"[Figures] В ассете {actions.name} нет карты '{actionMapName}'.", this);
                return;
            }

            _point = _map.FindAction(PointActionName, false);
            _grab = _map.FindAction(GrabActionName, false);
            _inspect = _map.FindAction(InspectActionName, false);
            _zoom = _map.FindAction(ZoomActionName, false);
            _stash = actions.FindActionMap(stashMapName, false)?.FindAction(StashActionName, false);
        }

        // Чужое действие только включается, но не выключается: карта Player принадлежит
        // игроку, и выключенный захват отнял бы у него E у компьютера.
        private void OnEnable()
        {
            _map?.Enable();
            _stash?.Enable();
        }

        private void OnDisable()
        {
            _map?.Disable();
            Current = FiguresInputFrame.None;
            _hasLastPointer = false;
        }

        private void Update()
        {
            if (_map == null) return;

            // В захваченном курсоре указателя нет: он замирает там, где его заперли.
            // Целью становится центр экрана — то есть то, на что игрок смотрит, — а
            // смещение приходится брать прямо у устройства, иначе осмотр детали стоял бы.
            var locked = Cursor.lockState == CursorLockMode.Locked;

            var pointer = locked
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : _point?.ReadValue<Vector2>() ?? Vector2.zero;

            var delta = locked
                ? Pointer.current?.delta.ReadValue() ?? Vector2.zero
                : (_hasLastPointer ? pointer - _lastPointer : Vector2.zero);

            _lastPointer = pointer;
            _hasLastPointer = !locked;

            Current = new FiguresInputFrame(
                pointer,
                delta,
                _grab?.WasPressedThisFrame() ?? false,
                _grab?.IsPressed() ?? false,
                _grab?.WasReleasedThisFrame() ?? false,
                _inspect?.IsPressed() ?? false,
                (_zoom?.ReadValue<Vector2>() ?? Vector2.zero).y,
                _stash?.WasPressedThisFrame() ?? false);
        }
    }
}
