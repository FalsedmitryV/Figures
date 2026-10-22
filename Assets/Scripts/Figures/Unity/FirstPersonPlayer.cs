using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Тело игрока: отдаёт кадр ввода ядру, применяет его решение к
    /// <see cref="CharacterController"/> и держит курсор в захвате.
    ///
    /// Решений здесь нет — ни скоростей, ни тяжести: всё это в
    /// <see cref="FirstPersonMotor"/> и <see cref="FirstPersonLook"/>. Класс отвечает
    /// за то, чего без движка не сделать: перемещение капсулы, проверку потолка,
    /// поворот глаз и состояние курсора.
    ///
    /// Без вызова <see cref="Bind"/> работает на встроенных числах
    /// <see cref="FirstPersonSettings.Default"/>: сцена с одной комнатой играбельна
    /// до появления конфигов и загрузчика.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        public const int ExecutionOrder = -50;

        /// <summary>Запас к радиусу проверки потолка: касание стены вставать не мешает.</summary>
        private const float CeilingProbeShrink = 0.95f;

        [SerializeField] private PlayerInputReader input;

        [Tooltip("Глаза: сюда вешается камера. Пусто — наклон взгляда применять некуда.")]
        [SerializeField] private Transform eye;

        [Tooltip("По этим слоям проверяется потолок при вставании. Слой самого игрока исключить.")]
        [SerializeField] private LayerMask ceilingLayers = ~0;

        [Tooltip("Захватывать курсор: обзор мышью работает только в захвате.")]
        [SerializeField] private bool captureCursor = true;

        private CharacterController _controller;
        private FirstPersonSettings _settings = FirstPersonSettings.Default;
        private FirstPersonMotor _motor;
        private FirstPersonLook _look;
        private bool _captured;
        private bool _lookSuppressed;
        private bool _uiMode;

        public FirstPersonMotor Motor => _motor;

        /// <summary>Игрок за интерфейсом: курсор свободен, ход и обзор выключены.</summary>
        public bool IsUiMode => _uiMode;

        /// <summary>
        /// Погасить обзор, не отнимая ход: мышь на это время занята другим. Зовётся
        /// захватом деталей на время осмотра — иначе одна мышь крутит и деталь, и голову.
        /// </summary>
        public void SuppressLook(bool on) => _lookSuppressed = on;

        /// <summary>
        /// Режим интерфейса: курсор свободен, ход и обзор отключены целиком. Включает
        /// его терминал, за которым игрок сидит — щелчок по кнопке на экране не должен
        /// возвращать курсор в захват, а WASD не должны уводить игрока от стола.
        /// </summary>
        public void SetUiMode(bool on)
        {
            if (_uiMode == on) return;

            _uiMode = on;
            if (on) Release();
            else if (captureCursor) Capture();
        }

        /// <summary>
        /// Переопределить числа из конфига. Загрузчик зовёт это из своего Awake, то есть
        /// раньше нашего: пока компонента нет, настройки просто запоминаются, а ядро
        /// соберётся в Awake уже на них.
        /// </summary>
        public void Bind(FirstPersonSettings settings)
        {
            if (settings == null) return;

            _settings = settings;
            if (_controller != null) Build();
        }

        /// <summary>Поставить игрока на место и погасить скорость: телепорт, не толчок.</summary>
        public void Place(Vector3 position, float yaw)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _controller.enabled = true;

            _look.Reset(yaw);
            _motor.Stop();
            ApplyLook();
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            Build();
        }

        private void OnEnable()
        {
            if (captureCursor) Capture();
        }

        private void OnDisable() => Release();

        private void Update()
        {
            var frame = input != null ? input.Current : PlayerInputFrame.None;

            if (_uiMode)
            {
                frame = frame.WithoutControl();
            }
            else
            {
                StepCursor(frame);
                if (!_captured && captureCursor) frame = frame.WithoutControl();
                else if (_lookSuppressed) frame = frame.WithoutLook();
            }

            _look.Step(frame, Time.deltaTime);
            ApplyLook();

            var motion = _motor.Step(frame, _look.Yaw, _controller.isGrounded, StandBlocked(), Time.deltaTime);
            ApplyHeight();
            _controller.Move(motion);
        }

        private void Build()
        {
            var yaw = _look?.Yaw ?? transform.eulerAngles.y;
            var pitch = _look?.Pitch ?? 0f;

            _motor = new FirstPersonMotor(_settings);
            _look = new FirstPersonLook(_settings);
            _look.Reset(yaw, pitch);

            ApplyHeight();
            ApplyLook();
        }

        private void ApplyLook()
        {
            transform.rotation = Quaternion.Euler(0f, _look.Yaw, 0f);
            if (eye != null) eye.localRotation = Quaternion.Euler(_look.Pitch, 0f, 0f);
        }

        private void ApplyHeight()
        {
            if (_controller == null) return;

            _controller.height = _motor.Height;
            _controller.center = new Vector3(0f, _motor.Height * 0.5f, 0f);

            if (eye != null)
                eye.localPosition = new Vector3(0f, _motor.EyeHeight, 0f);
        }

        /// <summary>
        /// Есть ли над головой перекрытие, которое не даст разогнуться. Луч идёт шаром
        /// от макушки присевшей капсулы на разницу высот; слой игрока в маску не входит,
        /// иначе запрос сразу находит собственный коллайдер.
        /// </summary>
        private bool StandBlocked()
        {
            var gap = _settings.StandHeight - _motor.Height;
            if (gap <= 0.001f) return false;

            var radius = _controller.radius * CeilingProbeShrink;
            var top = transform.position + Vector3.up * Mathf.Max(radius, _motor.Height - radius);

            return Physics.SphereCast(top, radius, Vector3.up, out _, gap + _controller.skinWidth,
                ceilingLayers, QueryTriggerInteraction.Ignore);
        }

        private void StepCursor(in PlayerInputFrame frame)
        {
            if (!captureCursor)
            {
                _captured = true;
                return;
            }

            // Esc отпускает захват средствами системы — и в редакторе, и в браузере.
            // Поэтому состояние читается у курсора, а не держится только своим флагом.
            if (_captured && (frame.CancelPressed || Cursor.lockState != CursorLockMode.Locked))
            {
                Release();
                return;
            }

            if (!_captured && frame.PointerPressed) Capture();
        }

        private void Capture()
        {
            _captured = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Release()
        {
            _captured = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
