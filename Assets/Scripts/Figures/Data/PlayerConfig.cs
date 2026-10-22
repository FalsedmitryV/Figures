using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Числа игрока от первого лица. Логики нет: ассет только отдаёт
    /// <see cref="FirstPersonSettings"/>, которые загрузчик передаёт в сцену.
    /// </summary>
    [CreateAssetMenu(menuName = "Figures/Player", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("Скорости, м/с")]
        [Min(0.1f)][SerializeField] private float walkSpeed = 3.2f;
        [Min(0.1f)][SerializeField] private float sprintSpeed = 5.2f;
        [Min(0.1f)][SerializeField] private float crouchSpeed = 1.5f;

        [Header("Разгон")]
        [Tooltip("Набор скорости, м/с².")]
        [Min(0.1f)][SerializeField] private float acceleration = 16f;

        [Tooltip("Сброс скорости, м/с². Меньше набора — остановка становится вязкой.")]
        [Min(0.1f)][SerializeField] private float deceleration = 22f;

        [Tooltip("Доля управляемости в воздухе. 0 — прыжок полностью баллистический.")]
        [Range(0f, 1f)][SerializeField] private float airControl = 0.35f;

        [Header("Вертикаль")]
        [Tooltip("Больше 9.81 намеренно: земная тяжесть в игре от первого лица читается как вата.")]
        [Min(0.1f)][SerializeField] private float gravity = 18f;

        [Tooltip("Высота прыжка, метры.")]
        [Min(0f)][SerializeField] private float jumpHeight = 0.95f;

        [Min(1f)][SerializeField] private float fallSpeedLimit = 25f;

        [Tooltip("Прижим к земле, м/с. Нулевая вертикаль роняет isGrounded на стыках коллайдеров.")]
        [Min(0f)][SerializeField] private float groundStick = 2f;

        [Header("Габариты, метры")]
        [Min(0.5f)][SerializeField] private float standHeight = 1.8f;
        [Min(0.4f)][SerializeField] private float crouchHeight = 1.1f;

        [Tooltip("Насколько глаза ниже макушки.")]
        [Min(0f)][SerializeField] private float eyeDrop = 0.17f;

        [Min(0.1f)][SerializeField] private float crouchTransitionSpeed = 6f;

        [Tooltip("Приседание переключателем, а не удержанием.")]
        [SerializeField] private bool crouchIsToggle;

        [Header("Обзор")]
        [Tooltip("Градусов на пиксель смещения мыши.")]
        [Min(0.001f)][SerializeField] private float degreesPerPixel = 0.11f;

        [Tooltip("Градусов в секунду при полностью отклонённом стике геймпада.")]
        [Min(1f)][SerializeField] private float degreesPerSecond = 160f;

        [Range(45f, 89f)][SerializeField] private float pitchLimit = 85f;

        [SerializeField] private bool invertPitch;

        public FirstPersonSettings Settings => new()
        {
            WalkSpeed = walkSpeed,
            SprintSpeed = sprintSpeed,
            CrouchSpeed = crouchSpeed,
            Acceleration = acceleration,
            Deceleration = deceleration,
            AirControl = airControl,
            Gravity = gravity,
            JumpHeight = jumpHeight,
            FallSpeedLimit = fallSpeedLimit,
            GroundStick = groundStick,
            StandHeight = standHeight,
            CrouchHeight = Mathf.Min(crouchHeight, standHeight),
            EyeDrop = eyeDrop,
            CrouchTransitionSpeed = crouchTransitionSpeed,
            CrouchIsToggle = crouchIsToggle,
            DegreesPerPixel = degreesPerPixel,
            DegreesPerSecond = degreesPerSecond,
            PitchLimit = pitchLimit,
            InvertPitch = invertPitch
        };
    }
}
