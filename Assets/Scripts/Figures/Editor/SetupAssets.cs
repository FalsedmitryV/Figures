using System;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Figures.EditorTools
{
    /// <summary>
    /// Служебное для сборки: папки, ассеты, слои, материалы. Вынесено из сетапов,
    /// потому что их стало два, а дрожащий дубликат «найти или создать» в каждом —
    /// первый способ получить два разных поведения из одного замысла.
    /// </summary>
    internal static class SetupAssets
    {
        internal const string ConfigFolder = "Assets/Config/Figures";
        internal const string MaterialFolder = "Assets/Config/Figures/Materials";
        internal const string FontFolder = "Assets/Config/Figures/Fonts";
        internal const string InputAssetPath = "Assets/InputSystem_Actions.inputactions";
        internal const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";

        internal const string PartLayerName = "FigurePart";
        internal const string SurfaceLayerName = "FigureSurface";
        internal const string PlayerLayerName = "Player";

        internal static T Load<T>(string fileName) where T : ScriptableObject =>
            AssetDatabase.LoadAssetAtPath<T>($"{ConfigFolder}/{fileName}");

        internal static T LoadOrCreate<T>(string fileName) where T : ScriptableObject
        {
            EnsureFolder(ConfigFolder);

            var path = $"{ConfigFolder}/{fileName}";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// Правка сериализованных полей одним куском. Через SerializedObject, а не
        /// через публичные сеттеры: ассеты и компоненты настраиваются теми же полями,
        /// которые потом правятся руками в инспекторе, и заводить ради сборки сцены
        /// публичное API у каждого компонента незачем.
        /// </summary>
        internal static void Apply(UnityEngine.Object target, Action<SerializedObject> edit)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            var current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>Найти слой по имени или занять первый свободный пользовательский.</summary>
        internal static int EnsureLayer(string layerName)
        {
            var existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning($"[Figures] Не удалось открыть TagManager — слой '{layerName}' не создан.");
                return 0;
            }

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return i;
            }

            Debug.LogWarning($"[Figures] Свободных слоёв нет — '{layerName}' не создан.");
            return 0;
        }

        /// <summary>
        /// Шрифт с кириллицей для экранного интерфейса.
        ///
        /// Встроенный «LiberationSans SDF» собран статически и держит только ASCII:
        /// русский текст в нём не отображается вовсе. Поэтому из того же файла шрифта
        /// делается динамический ассет — глифы растеризуются по мере надобности, и
        /// кириллица появляется сама.
        /// </summary>
        internal static TMP_FontAsset EnsureFont()
        {
            EnsureFolder(FontFolder);

            var path = $"{FontFolder}/GreyBox SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogWarning($"[Figures] Нет файла шрифта {SourceFontPath} — экран будет без кириллицы.");
                return null;
            }

            var asset = TMP_FontAsset.CreateFontAsset(source);
            if (asset == null)
            {
                Debug.LogWarning("[Figures] Не удалось собрать шрифт — экран будет без кириллицы.");
                return null;
            }

            asset.name = "GreyBox SDF";
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            asset.isMultiAtlasTexturesEnabled = true;

            AssetDatabase.CreateAsset(asset, path);

            // Материал и атлас — подобъекты ассета: оставленные снаружи, они теряются
            // при первом же импорте, и текст пропадает целиком.
            if (asset.material != null)
            {
                asset.material.name = "GreyBox SDF Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            if (asset.atlasTextures != null && asset.atlasTextures.Length > 0)
            {
                asset.atlasTextures[0].name = "GreyBox SDF Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            }

            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// Общий материал, а не перекраска каждого объекта: примитив, которому меняют
        /// цвет через <c>renderer.material</c>, получает собственный инстанс, и серая
        /// коробка из двадцати кубов превращается в двадцать материалов.
        /// </summary>
        internal static Material EnsureMaterial(string name, Color color)
        {
            EnsureFolder(MaterialFolder);

            var path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogWarning($"[Figures] Шейдер не найден — материал {name} не создан.");
                return null;
            }

            var material = new Material(shader) { name = name };
            GreyBoxBuilder.Tint(material, color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.08f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
