using System;
using System.Collections.Generic;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Форма и размер примитивов для деталей без префаба. Это заглушка вместо
    /// предусловия: прототип играбелен до того, как появилась первая модель.
    /// </summary>
    [CreateAssetMenu(menuName = "Figures/Grey Box", fileName = "GreyBoxConfig")]
    public sealed class GreyBoxConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Shape
        {
            [SerializeField] private PartSlotKind kind;
            [SerializeField] private PrimitiveType primitive = PrimitiveType.Cube;
            [SerializeField] private Vector3 scale = new(0.08f, 0.08f, 0.08f);

            public PartSlotKind Kind => kind;

            public PrimitiveType Primitive => primitive;

            public Vector3 Scale => scale;
        }

        [Serializable]
        public sealed class Port
        {
            [Tooltip("Деталь, на которой гнездо.")]
            [SerializeField] private PartSlotKind holder;

            [Tooltip("Деталь, которая в гнездо встаёт.")]
            [SerializeField] private PartSlotKind kind;

            [Tooltip("Номер среди гнёзд того же типа на держателе. Пара рук — 0 и 1.")]
            [Min(0)][SerializeField] private int index;

            [Tooltip("Где центр вставленной детали относительно центра держателя, юниты.")]
            [SerializeField] private Vector3 offset;

            public PartSlotKind Holder => holder;

            public PartSlotKind Kind => kind;

            public int Index => index;

            public Vector3 Offset => offset;
        }

        [SerializeField] private List<Shape> shapes = new();

        [Tooltip("Гнёзда на деталях-заглушках: куда крепятся другие детали.")]
        [SerializeField] private List<Port> ports = new();

        [Tooltip("Для типов, которых нет в списке.")]
        [SerializeField] private PrimitiveType fallbackPrimitive = PrimitiveType.Cube;

        [SerializeField] private Vector3 fallbackScale = new(0.08f, 0.08f, 0.08f);

        [Tooltip("Необязательно. Пусто — материал примитива по умолчанию.")]
        [SerializeField] private Material material;

        public Material Material => material;
        public IReadOnlyList<Port> Ports => ports;

        public PrimitiveType PrimitiveOf(PartSlotKind kind) => Find(kind)?.Primitive ?? fallbackPrimitive;

        public Vector3 ScaleOf(PartSlotKind kind) => Find(kind)?.Scale ?? fallbackScale;

        private Shape Find(PartSlotKind kind)
        {
            foreach (var shape in shapes)
                if (shape != null && shape.Kind == kind) return shape;
            return null;
        }
    }
}
