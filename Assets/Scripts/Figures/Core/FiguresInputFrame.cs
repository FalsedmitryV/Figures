using UnityEngine;

namespace Figures
{
    /// <summary>Сырой кадр ввода. Что он значит, решает логика, а не устройство.</summary>
    public readonly struct FiguresInputFrame
    {
        /// <summary>Положение указателя в пикселях экрана.</summary>
        public readonly Vector2 Pointer;

        /// <summary>Смещение указателя за кадр.</summary>
        public readonly Vector2 PointerDelta;

        public readonly bool PrimaryPressed;
        public readonly bool PrimaryHeld;
        public readonly bool PrimaryReleased;
        public readonly bool SecondaryPressed;
        public readonly float Wheel;
        public readonly bool WheelModifierHeld;
        public readonly bool StashPressed;

        public FiguresInputFrame(Vector2 pointer, Vector2 pointerDelta, bool primaryPressed,
            bool primaryHeld, bool primaryReleased, bool secondaryPressed, float wheel,
            bool wheelModifierHeld, bool stashPressed)
        {
            Pointer = pointer;
            PointerDelta = pointerDelta;
            PrimaryPressed = primaryPressed;
            PrimaryHeld = primaryHeld;
            PrimaryReleased = primaryReleased;
            SecondaryPressed = secondaryPressed;
            Wheel = wheel;
            WheelModifierHeld = wheelModifierHeld;
            StashPressed = stashPressed;
        }

        public static FiguresInputFrame None => default;
    }
}