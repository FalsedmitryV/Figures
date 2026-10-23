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
        private const string PrimaryActionName = "Click";
        private const string SecondaryActionName = "RightClick";
        private const string WheelActionName = "ScrollWheel";
        private const string StashActionName = "Stash";
        private const string WheelModifierActionName = "Sprint";

        [Tooltip("Ассет со всеми привязками. Пусто — ввода не будет.")]
        [SerializeField] private InputActionAsset actions;

        [SerializeField] private string actionMapName = "UI";

        [Tooltip("Карта игрока.")]
        [SerializeField] private string playerMapName = "Player";

        private InputActionMap _map;
        private InputAction _point;
        private InputAction _primary;
        private InputAction _secondary;
        private InputAction _wheel;
        private InputAction _stash;
        private InputAction _wheelModifier;

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
            _primary = _map.FindAction(PrimaryActionName, false);
            _secondary = _map.FindAction(SecondaryActionName, false);
            _wheel = _map.FindAction(WheelActionName, false);
            var playerMap = actions.FindActionMap(playerMapName, false);
            _stash = playerMap?.FindAction(StashActionName, false);
            _wheelModifier = playerMap?.FindAction(WheelModifierActionName, false);
        }

        // Действия из карты игрока только включаются, но не выключаются: карта принадлежит игроку.
        private void OnEnable()
        {
            _map?.Enable();
            _stash?.Enable();
            _wheelModifier?.Enable();
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
            // смещение приходится брать прямо у устройства: указатель стоит на месте.
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
                _primary?.WasPressedThisFrame() ?? false,
                _primary?.IsPressed() ?? false,
                _primary?.WasReleasedThisFrame() ?? false,
                _secondary?.WasPressedThisFrame() ?? false,
                (_wheel?.ReadValue<Vector2>() ?? Vector2.zero).y,
                _wheelModifier?.IsPressed() ?? false,
                _stash?.WasPressedThisFrame() ?? false);
        }
    }
}