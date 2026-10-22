using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Собирает примитив вместо отсутствующего префаба детали и красит его в цвет
    /// владельца. Пока цвет — единственный признак принадлежности, он же и предмет
    /// проверки на читаемость.
    /// </summary>
    public static class GreyBoxBuilder
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static GameObject BuildPart(PartSlotKind kind, GreyBoxConfig config, string objectName)
        {
            var go = GameObject.CreatePrimitive(config != null ? config.PrimitiveOf(kind) : PrimitiveType.Cube);
            go.name = objectName;
            go.transform.localScale = config != null ? config.ScaleOf(kind) : Vector3.one * 0.08f;

            if (config != null && config.Material != null && go.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = config.Material;

            return go;
        }

        /// <summary>Перекрасить все рендереры объекта. Материал инстанцируется — это осознанно.</summary>
        public static void Tint(GameObject go, Color color)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (var renderer in renderers) Tint(renderer, color);
        }

        public static void Tint(Renderer renderer, Color color) => Tint(renderer.material, color);

        /// <summary>Перекрасить материал, не зная, какой конвейер его шейдеру родной.</summary>
        public static void Tint(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            else if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
        }

        public static Color ReadTint(Renderer renderer)
        {
            var material = renderer.sharedMaterial;
            if (material == null) return Color.white;
            if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
            return material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
        }
    }
}
