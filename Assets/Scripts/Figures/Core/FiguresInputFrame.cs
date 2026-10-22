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

        /// <summary>Зажата кнопка осмотра: сырое состояние, решает по нему логика.</summary>
        public readonly bool InspectHeld;

        /// <summary>Нажат осмотр в этом кадре: им включается и выключается режим сборки.</summary>
        public readonly bool InspectPressed;

        /// <summary>Колесо за кадр: выбор ячейки, с модификатором — приближение куска в руках.</summary>
        public readonly float Zoom;

        /// <summary>Зажат модификатор колеса: колесо приближает кусок, а не перебирает ячейки.</summary>
        public readonly bool ZoomModifierHeld;

        /// <summary>Убрать деталь из руки в ячейку или достать из выбранной.</summary>
        public readonly bool StashPressed;

        public FiguresInputFrame(Vector2 pointer, Vector2 pointerDelta, bool grabPressed,
            bool grabHeld, bool grabReleased, bool inspectHeld, bool inspectPressed, float zoom,
            bool zoomModifierHeld, bool stashPressed)
        {
            Pointer = pointer;
            PointerDelta = pointerDelta;
            GrabPressed = grabPressed;
            GrabHeld = grabHeld;
            GrabReleased = grabReleased;
            InspectHeld = inspectHeld;
            InspectPressed = inspectPressed;
            Zoom = zoom;
            ZoomModifierHeld = zoomModifierHeld;
            StashPressed = stashPressed;
        }

        public static FiguresInputFrame None => default;
    }
}
