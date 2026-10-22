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

        [SerializeField] private List<Shape> shapes = new();

        [Tooltip("Для типов, которых нет в списке.")]
        [SerializeField] private PrimitiveType fallbackPrimitive = PrimitiveType.Cube;

        [SerializeField] private Vector3 fallbackScale = new(0.08f, 0.08f, 0.08f);

        [Tooltip("Необязательно. Пусто — материал примитива по умолчанию.")]
        [SerializeField] private Material material;

        public Material Material => material;

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
