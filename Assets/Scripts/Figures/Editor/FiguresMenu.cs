using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Figures.EditorTools
{
    /// <summary>
    /// Все пункты меню сборки — в одном месте, чтобы порядок шагов читался целиком.
    ///
    /// Сборка идёт в одну сторону: сначала комната — она даёт пол, свет, стол и игрока
    /// с камерой в глазах, потом витрина — она встаёт на найденную столешницу. Обратный
    /// порядок работал бы тоже, но витрина, собранная первой, успела бы поставить
    /// собственный стол и забрать камеру себе.
    ///
    /// Каждый шаг идемпотентен: он сносит свой прежний корень и собирает заново, а не
    /// кладёт второй рядом. Откат — обычный Ctrl+Z.
    /// </summary>
    public static class FiguresMenu
    {
        [MenuItem("Figures/1. Создать конфиги", priority = 10)]
        public static void CreateConfigs()
        {
            var root = PrototypeSetup.CreateConfigs();
            RoomSetup.CreateConfigs(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Figures] Конфиги созданы в {SetupAssets.ConfigFolder}.");
        }

        [MenuItem("Figures/2. Собрать сцену", priority = 11)]
        public static void BuildScene()
        {
            if (RoomSetup.BuildRoom() == null) return;
            if (!PrototypeSetup.BuildShowcase()) return;

            Finish();
            Debug.Log("[Figures] Сцена собрана. WASD — ход, мышь — обзор, Shift — бег, C — присед, " +
                      "Space — прыжок, ЛКМ — взять и отпустить деталь, ПКМ — осмотр, " +
                      "Esc — отпустить курсор.");
        }

        [MenuItem("Figures/Пересобрать/Комнату", priority = 30)]
        public static void RebuildRoom()
        {
            if (RoomSetup.BuildRoom() == null) return;

            Finish();
            Debug.Log("[Figures] Комната пересобрана. Витрина осталась на месте.");
        }

        [MenuItem("Figures/Пересобрать/Витрину", priority = 31)]
        public static void RebuildShowcase()
        {
            if (!PrototypeSetup.BuildShowcase()) return;

            Finish();
            Debug.Log("[Figures] Витрина пересобрана на столешнице.");
        }

        private static void Finish() => EditorSceneManager.MarkAllScenesDirty();
    }
}
