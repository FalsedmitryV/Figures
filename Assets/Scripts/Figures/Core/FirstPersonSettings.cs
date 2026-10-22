using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Числа ходьбы и обзора в метрах, секундах и градусах. Значения по умолчанию —
    /// рабочие: игрок бегает в сцене без конфига и без загрузчика, а
    /// <see cref="PlayerConfig"/> их только переопределяет.
    /// </summary>
    public sealed class FirstPersonSettings
    {
        public static FirstPersonSettings Default => new();

        public float WalkSpeed { get; set; } = 3.2f;
        public float SprintSpeed { get; set; } = 5.2f;
        public float CrouchSpeed { get; set; } = 1.5f;

        /// <summary>Набор и сброс скорости, м/с². Сброс больше набора — иначе остановка вязкая.</summary>
        public float Acceleration { get; set; } = 16f;

        public float Deceleration { get; set; } = 22f;

        /// <summary>Доля управляемости в воздухе. 0 — прыжок полностью баллистический.</summary>
        public float AirControl { get; set; } = 0.35f;

        public float Gravity { get; set; } = 18f;

        /// <summary>Высота прыжка, метры. Скорость отрыва считается из неё и тяжести.</summary>
        public float JumpHeight { get; set; } = 0.95f;

        public float FallSpeedLimit { get; set; } = 25f;

        /// <summary>
        /// Скорость прижима к земле. Нулевая вертикаль на стыках коллайдеров роняет
        /// <c>isGrounded</c> на кадр, и шаг по ровному полу читается как падение.
        /// </summary>
        public float GroundStick { get; set; } = 2f;

        public float StandHeight { get; set; } = 1.8f;
        public float CrouchHeight { get; set; } = 1.1f;

        /// <summary>Насколько глаза ниже макушки, метры.</summary>
        public float EyeDrop { get; set; } = 0.17f;

        /// <summary>Скорость смены высоты капсулы, м/с.</summary>
        public float CrouchTransitionSpeed { get; set; } = 6f;

        /// <summary>Приседание переключателем, а не удержанием.</summary>
        public bool CrouchIsToggle { get; set; }

        /// <summary>Градусов на пиксель смещения мыши.</summary>
        public float DegreesPerPixel { get; set; } = 0.11f;

        /// <summary>Градусов в секунду при полностью отклонённом стике.</summary>
        public float DegreesPerSecond { get; set; } = 160f;

        /// <summary>Предел наклона взгляда вверх-вниз, градусы.</summary>
        public float PitchLimit { get; set; } = 85f;

        public bool InvertPitch { get; set; }

        public float JumpSpeed => Mathf.Sqrt(2f * Gravity * JumpHeight);
    }
}
