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

        public readonly bool GrabPressed;
        public readonly bool GrabHeld;
        public readonly bool GrabReleased;

        /// <summary>Зажат осмотр: деталь поднимается к камере и крутится указателем.</summary>
        public readonly bool InspectHeld;

        /// <summary>Колесо за кадр: приближение детали при осмотре, иначе выбор ячейки.</summary>
        public readonly float Zoom;

        /// <summary>Убрать деталь из руки в ячейку или достать из выбранной.</summary>
        public readonly bool StashPressed;

        public FiguresInputFrame(Vector2 pointer, Vector2 pointerDelta, bool grabPressed,
            bool grabHeld, bool grabReleased, bool inspectHeld, float zoom, bool stashPressed)
        {
            Pointer = pointer;
            PointerDelta = pointerDelta;
            GrabPressed = grabPressed;
            GrabHeld = grabHeld;
            GrabReleased = grabReleased;
            InspectHeld = inspectHeld;
            Zoom = zoom;
            StashPressed = stashPressed;
        }

        public static FiguresInputFrame None => default;
    }
}
