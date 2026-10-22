using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Figures.EditorTools
{
    /// <summary>
    /// Служебное для сцены: корневой объект сетапа, серые кубы, свет, рамка вида.
    ///
    /// Каждый сетап живёт в своём корне и пересобирается целиком — повторный запуск
    /// сносит прежний корень, а не кладёт второй рядом. Поэтому корень ищется среди
    /// корневых объектов сцены по имени, а не <c>GameObject.Find</c>: тот обошёл бы
    /// всю иерархию и мог бы наткнуться на одноимённого потомка.
    /// </summary>
    internal static class SetupScene
    {
        /// <summary>Снести прежний корень сетапа и создать пустой новый, с откатом по Ctrl+Z.</summary>
        internal static GameObject Replace(string name, string undoLabel)
        {
            var old = FindRoot(name);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var holder = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(holder, undoLabel);
            return holder;
        }

        internal static GameObject FindRoot(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) return root;

            return null;
        }

        internal static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material = null, int layer = 0)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            if (material != null && go.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;

            return go;
        }

        internal static Light Directional(string name, Transform parent, Quaternion rotation,
            float intensity)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.rotation = rotation;

            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            return light;
        }

        internal static void Frame(Bounds bounds)
        {
            var scene = SceneView.lastActiveSceneView;
            if (scene == null) return;

            scene.Frame(bounds, false);
        }
    }
}
