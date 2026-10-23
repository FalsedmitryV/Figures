using UnityEngine;

namespace Figures
{
    /// <summary>Сырой кадр ввода игрока. Что он значит, решает логика, а не устройство.</summary>
    public readonly struct PlayerInputFrame
    {
        /// <summary>Ход: x — вбок, y — вперёд. Длина не больше 1.</summary>
        public readonly Vector2 Move;

        /// <summary>
        /// Обзор. Пиксели за кадр, когда <see cref="LookIsDelta"/>; иначе отклонение
        /// стика в -1..1, которое домножается на время.
        /// </summary>
        public readonly Vector2 Look;

        /// <summary>Обзор пришёл с мыши: это смещение за кадр, а не величина наклона.</summary>
        public readonly bool LookIsDelta;

        public readonly bool SprintHeld;
        public readonly bool CrouchHeld;
        public readonly bool CrouchPressed;
        public readonly bool JumpPressed;
        public readonly bool InteractPressed;

        /// <summary>Нажатие основной кнопки: им курсор возвращается в захват.</summary>
        public readonly bool PointerPressed;

        /// <summary>Отмена: курсор отпускается.</summary>
        public readonly bool CancelPressed;

        /// <summary>Поднять или опустить фотокамеру.</summary>
        public readonly bool PhotoPressed;

        public PlayerInputFrame(Vector2 move, Vector2 look, bool lookIsDelta, bool sprintHeld,
            bool crouchHeld, bool crouchPressed, bool jumpPressed, bool interactPressed,
            bool pointerPressed, bool cancelPressed, bool photoPressed)
        {
            Move = move;
            Look = look;
            LookIsDelta = lookIsDelta;
            SprintHeld = sprintHeld;
            CrouchHeld = crouchHeld;
            CrouchPressed = crouchPressed;
            JumpPressed = jumpPressed;
            InteractPressed = interactPressed;
            PointerPressed = pointerPressed;
            CancelPressed = cancelPressed;
            PhotoPressed = photoPressed;
        }

        public static PlayerInputFrame None => default;

        /// <summary>Тот же кадр без обзора и действий: курсор отпущен, ходить нельзя.</summary>
        public PlayerInputFrame WithoutControl() =>
            new(Vector2.zero, Vector2.zero, LookIsDelta, false, CrouchHeld, false, false, false,
                PointerPressed, CancelPressed, PhotoPressed);

        /// <summary>
        /// Тот же кадр без обзора: ходить можно, головой не вертеть. Нужен, когда мышь
        /// занята чем-то другим — например, осмотром детали в руках.
        /// </summary>
        public PlayerInputFrame WithoutLook() =>
            new(Move, Vector2.zero, LookIsDelta, SprintHeld, CrouchHeld, CrouchPressed, JumpPressed,
                InteractPressed, PointerPressed, CancelPressed, PhotoPressed);
    }
}
