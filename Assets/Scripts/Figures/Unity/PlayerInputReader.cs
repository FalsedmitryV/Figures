using UnityEngine;
using UnityEngine.InputSystem;

namespace Figures
{
    /// <summary>
    /// Единственное место, где игрок знает про устройства. Отдаёт кадр
    /// <see cref="PlayerInputFrame"/>, дальше решает логика.
    ///
    /// Карта берётся из общего ассета привязок: все клавиши лежат в одном месте,
    /// иначе коллизию двух действий на одной кнопке не видно.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        public const int ExecutionOrder = -200;

        private const string MoveActionName = "Move";
        private const string LookActionName = "Look";
        private const string SprintActionName = "Sprint";
        private const string CrouchActionName = "Crouch";
        private const string JumpActionName = "Jump";
        private const string InteractActionName = "Interact";
        private const string PointerActionName = "Attack";
        private const string CancelActionName = "Cancel";

        [Tooltip("Ассет со всеми привязками. Пусто — ввода не будет.")]
        [SerializeField] private InputActionAsset actions;

        [SerializeField] private string actionMapName = "Player";

        [Tooltip("Карта, из которой берётся отмена для отпускания курсора.")]
        [SerializeField] private string uiMapName = "UI";

        private InputActionMap _map;
        private InputAction _move;
        private InputAction _look;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private InputAction _interact;
        private InputAction _pointer;
        private InputAction _cancel;

        public PlayerInputFrame Current { get; private set; }

        private void Awake()
        {
            if (actions == null)
            {
                DevLog.Info("[Figures] Ассет привязок игрока не задан — ввод выключен.");
                return;
            }

            _map = actions.FindActionMap(actionMapName, false);
            if (_map == null)
            {
                Debug.LogWarning($"[Figures] В ассете {actions.name} нет карты '{actionMapName}'.", this);
                return;
            }

            _move = _map.FindAction(MoveActionName, false);
            _look = _map.FindAction(LookActionName, false);
            _sprint = _map.FindAction(SprintActionName, false);
            _crouch = _map.FindAction(CrouchActionName, false);
            _jump = _map.FindAction(JumpActionName, false);
            _interact = _map.FindAction(InteractActionName, false);
            _pointer = _map.FindAction(PointerActionName, false);

            // Из карты UI включается одно действие, а не вся карта: остальное в ней
            // спорит с игровым вводом за те же кнопки мыши.
            _cancel = actions.FindActionMap(uiMapName, false)?.FindAction(CancelActionName, false);
        }

        private void OnEnable()
        {
            _map?.Enable();
            _cancel?.Enable();
        }

        private void OnDisable()
        {
            _map?.Disable();
            _cancel?.Disable();
            Current = PlayerInputFrame.None;
        }

        private void Update()
        {
            if (_map == null) return;

            var look = _look?.ReadValue<Vector2>() ?? Vector2.zero;

            Current = new PlayerInputFrame(
                _move?.ReadValue<Vector2>() ?? Vector2.zero,
                look,
                LookIsDelta(),
                _sprint?.IsPressed() ?? false,
                _crouch?.IsPressed() ?? false,
                _crouch?.WasPressedThisFrame() ?? false,
                _jump?.WasPressedThisFrame() ?? false,
                _interact?.WasPressedThisFrame() ?? false,
                _pointer?.WasPressedThisFrame() ?? false,
                _cancel?.WasPressedThisFrame() ?? false);
        }

        /// <summary>
        /// Мышь и стик приходят в одно действие, но значат разное: у мыши это пиксели
        /// за кадр, у стика — величина наклона. Разделить их может только тот, кто
        /// видит устройство, то есть этот класс.
        /// </summary>
        private bool LookIsDelta()
        {
            var device = _look?.activeControl?.device;
            return device is Pointer || device == null;
        }
    }
}
