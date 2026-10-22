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
        /// <summary>Размер метки готового гнезда, юниты.</summary>
        private const float MarkerSize = 0.03f;

        /// <summary>
        /// Корень детали — пустой объект, примитив — его потомок. Масштаб примитива
        /// неравномерный, и деталь, прикреплённая прямо к нему, унаследовала бы его и
        /// перекосилась; поэтому гнёзда висят на корне.
        /// </summary>
        public static GameObject BuildPart(PartSlotKind kind, GreyBoxConfig config, string objectName)
        {
            var root = new GameObject(objectName);

            var shape = GameObject.CreatePrimitive(config != null ? config.PrimitiveOf(kind) : PrimitiveType.Cube);
            shape.name = "Shape";
            shape.transform.SetParent(root.transform, worldPositionStays: false);
            shape.transform.localScale = config != null ? config.ScaleOf(kind) : Vector3.one * 0.08f;

            var material = config != null ? config.Material : null;
            if (material != null && shape.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = material;

            if (config == null) return root;

            foreach (var port in config.Ports)
                if (port != null && port.Holder == kind) BuildPort(port, material, root.transform);

            return root;
        }

        private static void BuildPort(GreyBoxConfig.Port port, Material material, Transform holder)
        {
            var go = new GameObject($"Port_{port.Kind}_{port.Index}");
            go.transform.SetParent(holder, worldPositionStays: false);
            go.transform.localPosition = port.Offset;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Ready";
            marker.transform.SetParent(go.transform, worldPositionStays: false);
            marker.transform.localScale = Vector3.one * MarkerSize;

            // Сразу, а не в конце кадра: деталь собирает свои коллайдеры в этом же кадре,
            // и коллайдер метки стал бы частью детали.
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            var markerRenderer = marker.GetComponent<Renderer>();
            if (material != null) markerRenderer.sharedMaterial = material;

            go.AddComponent<SocketView>().Setup(port.Kind, port.Index, markerRenderer);
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
