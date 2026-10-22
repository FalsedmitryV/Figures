using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Вся ходьба: разгон по земле и в воздухе, тяжесть, прыжок, высота капсулы при
    /// приседании. Чистый C# — ни трансформа, ни коллайдеров. Шаг получает кадр ввода
    /// и два факта о мире (стоит ли на земле, есть ли потолок над головой) и отдаёт
    /// перемещение за кадр; двигать и спрашивать физику — дело сцены.
    /// </summary>
    public sealed class FirstPersonMotor
    {
        private readonly FirstPersonSettings _settings;

        private Vector3 _velocity;
        private bool _crouchWish;

        public FirstPersonMotor(FirstPersonSettings settings)
        {
            _settings = settings ?? FirstPersonSettings.Default;
            Height = _settings.StandHeight;
        }

        /// <summary>Текущая высота капсулы, метры.</summary>
        public float Height { get; private set; }

        /// <summary>Высота глаз над опорой, метры.</summary>
        public float EyeHeight => Mathf.Max(0.1f, Height - _settings.EyeDrop);

        public bool IsCrouching { get; private set; }

        public bool IsGrounded { get; private set; }

        public Vector3 Velocity => _velocity;

        /// <summary>Скорость по полу, м/с: для шагов, тряски камеры и прочего отклика.</summary>
        public float PlanarSpeed => new Vector2(_velocity.x, _velocity.z).magnitude;

        public void Stop() => _velocity = Vector3.zero;

        public Vector3 Step(in PlayerInputFrame input, float yaw, bool grounded, bool standBlocked,
            float deltaTime)
        {
            if (deltaTime <= 0f) return Vector3.zero;

            IsGrounded = grounded;
            StepCrouch(input, standBlocked, deltaTime);
            StepPlanar(input, yaw, grounded, deltaTime);
            StepVertical(input, grounded, deltaTime);

            return _velocity * deltaTime;
        }

        private void StepCrouch(in PlayerInputFrame input, bool standBlocked, float deltaTime)
        {
            if (_settings.CrouchIsToggle)
            {
                if (input.CrouchPressed) _crouchWish = !_crouchWish;
            }
            else
            {
                _crouchWish = input.CrouchHeld;
            }

            // Потолок над головой держит присед, сколько бы игрок ни хотел встать.
            IsCrouching = _crouchWish || standBlocked;

            var target = IsCrouching ? _settings.CrouchHeight : _settings.StandHeight;
            Height = Mathf.MoveTowards(Height, target, _settings.CrouchTransitionSpeed * deltaTime);
        }

        private void StepPlanar(in PlayerInputFrame input, float yaw, bool grounded, float deltaTime)
        {
            var wish = input.Move;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            var yawRad = yaw * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            var right = new Vector3(forward.z, 0f, -forward.x);

            var target = (right * wish.x + forward * wish.y) * TargetSpeed(input, wish);
            var planar = new Vector3(_velocity.x, 0f, _velocity.z);

            var rate = target.sqrMagnitude > planar.sqrMagnitude
                ? _settings.Acceleration
                : _settings.Deceleration;
            if (!grounded) rate *= _settings.AirControl;

            planar = Vector3.MoveTowards(planar, target, rate * deltaTime);

            _velocity.x = planar.x;
            _velocity.z = planar.z;
        }

        private float TargetSpeed(in PlayerInputFrame input, Vector2 wish)
        {
            if (wish.sqrMagnitude <= 0.0001f) return 0f;
            if (IsCrouching) return _settings.CrouchSpeed;
            return input.SprintHeld ? _settings.SprintSpeed : _settings.WalkSpeed;
        }

        private void StepVertical(in PlayerInputFrame input, bool grounded, float deltaTime)
        {
            if (grounded)
            {
                _velocity.y = -_settings.GroundStick;

                // Прыгать из приседа нельзя: под потолком это подбросило бы капсулу
                // в перекрытие, а разгибаться ей там некуда.
                if (input.JumpPressed && !IsCrouching) _velocity.y = _settings.JumpSpeed;
                return;
            }

            _velocity.y = Mathf.Max(_velocity.y - _settings.Gravity * deltaTime, -_settings.FallSpeedLimit);
        }
    }
}
