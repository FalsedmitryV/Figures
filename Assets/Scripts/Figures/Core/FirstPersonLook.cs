using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Накопленный взгляд: рыскание без границ, наклон с упором. Чистый C# — углы
    /// считаются без трансформа, сцена их только применяет.
    /// </summary>
    public sealed class FirstPersonLook
    {
        private readonly FirstPersonSettings _settings;

        public FirstPersonLook(FirstPersonSettings settings) => _settings = settings ?? FirstPersonSettings.Default;

        /// <summary>Рыскание в градусах: поворот тела вокруг вертикали.</summary>
        public float Yaw { get; private set; }

        /// <summary>Наклон в градусах в системе Unity: вверх — отрицательный.</summary>
        public float Pitch { get; private set; }

        public void Reset(float yaw, float pitch = 0f)
        {
            Yaw = Mathf.Repeat(yaw, 360f);
            Pitch = Mathf.Clamp(pitch, -_settings.PitchLimit, _settings.PitchLimit);
        }

        public void Step(in PlayerInputFrame input, float deltaTime)
        {
            var step = input.LookIsDelta
                ? input.Look * _settings.DegreesPerPixel
                : input.Look * (_settings.DegreesPerSecond * deltaTime);

            if (_settings.InvertPitch) step.y = -step.y;

            Yaw = Mathf.Repeat(Yaw + step.x, 360f);
            Pitch = Mathf.Clamp(Pitch - step.y, -_settings.PitchLimit, _settings.PitchLimit);
        }
    }
}
