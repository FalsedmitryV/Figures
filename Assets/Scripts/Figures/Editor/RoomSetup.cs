using UnityEditor;
using UnityEngine;

namespace Figures.EditorTools
{
    /// <summary>
    /// Сборка серой комнаты и игрока от первого лица: конфиги, слой игрока, коробка,
    /// свет и риг с камерой в глазах. Пункты меню живут в <see cref="FiguresMenu"/>.
    ///
    /// Масштаб человеческий — метры, рост 1.8. Стол комнаты помечен
    /// <see cref="WorkSurface"/>: на него встаёт витрина с фигурками, и столешница
    /// лежит на слое поверхностей, иначе деталь в руке по ней не повести.
    /// </summary>
    internal static class RoomSetup
    {
        private const string HolderName = "Figures Room";

        /// <summary>Стол под витрину: 2.0 x 1.2 — ряд подставок и поднос помещаются рядом.</summary>
        private static readonly Vector3 DeskSize = new(2f, 0.78f, 1.2f);

        // =====================================================================
        // КОНФИГИ
        // =====================================================================

        internal static void CreateConfigs(PrototypeConfig root)
        {
            var player = SetupAssets.LoadOrCreate<PlayerConfig>("PlayerConfig.asset");
            var room = CreateRoomConfig();

            SetupAssets.Apply(root, so =>
            {
                so.FindProperty("player").objectReferenceValue = player;
                so.FindProperty("room").objectReferenceValue = room;
            });
        }

        private static RoomConfig CreateRoomConfig()
        {
            var asset = SetupAssets.LoadOrCreate<RoomConfig>("RoomConfig.asset");

            SetupAssets.Apply(asset, so =>
            {
                so.FindProperty("deskSize").vector3Value = DeskSize;

                so.FindProperty("floorMaterial").objectReferenceValue =
                    SetupAssets.EnsureMaterial("GreyBox_Floor", new Color(0.34f, 0.34f, 0.36f));
                so.FindProperty("wallMaterial").objectReferenceValue =
                    SetupAssets.EnsureMaterial("GreyBox_Wall", new Color(0.52f, 0.52f, 0.54f));
                so.FindProperty("propMaterial").objectReferenceValue =
                    SetupAssets.EnsureMaterial("GreyBox_Prop", new Color(0.44f, 0.41f, 0.38f));
            });

            return asset;
        }

        // =====================================================================
        // СЦЕНА
        // =====================================================================

        internal static FirstPersonPlayer BuildRoom()
        {
            var room = SetupAssets.Load<RoomConfig>("RoomConfig.asset");
            if (room == null)
            {
                Debug.LogError("[Figures] Конфиг комнаты не создан — сначала создай конфиги.");
                return null;
            }

            var playerLayer = SetupAssets.EnsureLayer(SetupAssets.PlayerLayerName);
            var surfaceLayer = SetupAssets.EnsureLayer(SetupAssets.SurfaceLayerName);

            var holder = SetupScene.Replace(HolderName, "Собрать комнату");

            var roomRoot = RoomGreyBoxBuilder.Build(room, holder.transform, surfaceLayer: surfaceLayer);

            // Солнце нужно и в закрытой коробке: лампа внутри даёт пятно, а общий
            // наклонный свет — отличимые друг от друга стены.
            SetupScene.Directional("Sun", holder.transform, Quaternion.Euler(50f, -30f, 0f), 0.7f);

            var player = BuildPlayer(room, holder.transform, playerLayer);
            var terminal = roomRoot != null
                ? roomRoot.GetComponentInChildren<ComputerTerminal>(includeInactive: true)
                : null;

            if (terminal != null) BindTerminal(terminal, player);
            BindToScene(player, terminal);

            Selection.activeGameObject = player.gameObject;

            var inner = room.InnerSize;
            SetupScene.Frame(new Bounds(new Vector3(0f, room.WallHeight * 0.5f, 0f),
                new Vector3(inner.x, room.WallHeight, inner.y)));

            return player;
        }

        private static FirstPersonPlayer BuildPlayer(RoomConfig room, Transform parent, int playerLayer)
        {
            var settings = FirstPersonSettings.Default;
            var config = SetupAssets.Load<PlayerConfig>("PlayerConfig.asset");
            if (config != null) settings = config.Settings;

            var go = new GameObject("Player",
                typeof(CharacterController), typeof(PlayerInputReader), typeof(FirstPersonPlayer),
                typeof(CrosshairView))
            {
                layer = playerLayer
            };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(room.SpawnPosition, Quaternion.Euler(0f, room.SpawnYaw, 0f));

            var controller = go.GetComponent<CharacterController>();
            controller.height = settings.StandHeight;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, settings.StandHeight * 0.5f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.02f;
            controller.minMoveDistance = 0f;

            var eye = BuildEye(go.transform, settings);
            var reader = go.GetComponent<PlayerInputReader>();

            SetupAssets.Apply(reader, so => so.FindProperty("actions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Object>(SetupAssets.InputAssetPath));

            SetupAssets.Apply(go.GetComponent<FirstPersonPlayer>(), so =>
            {
                so.FindProperty("input").objectReferenceValue = reader;
                so.FindProperty("eye").objectReferenceValue = eye;

                // Слой игрока из маски исключён: иначе проверка потолка находит
                // собственный коллайдер и встать уже нельзя никогда.
                so.FindProperty("ceilingLayers").intValue = ~(1 << playerLayer);
            });

            return go.GetComponent<FirstPersonPlayer>();
        }

        /// <summary>
        /// Камера сцены переезжает в глаза, а не создаётся вторая: две камеры с тегом
        /// MainCamera — это молчаливая лотерея, какая из них рисует кадр.
        /// </summary>
        private static Transform BuildEye(Transform player, FirstPersonSettings settings)
        {
            var camera = Camera.main ?? Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);

            if (camera == null)
            {
                var go = new GameObject("Eye", typeof(Camera), typeof(AudioListener));
                go.transform.SetParent(player, false);
                camera = go.GetComponent<Camera>();
            }
            else
            {
                Undo.RecordObject(camera, "Настроить камеру игрока");
                Undo.SetTransformParent(camera.transform, player, "Перенести камеру в глаза");
            }

            if (!camera.CompareTag("MainCamera")) camera.tag = "MainCamera";
            if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();

            camera.transform.localPosition = new Vector3(0f, settings.StandHeight - settings.EyeDrop, 0f);
            camera.transform.localRotation = Quaternion.identity;

            camera.orthographic = false;
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);

            return camera.transform;
        }

        /// <summary>
        /// Пересобранному игроку возвращают его связи: загрузчик даёт ему числа из
        /// конфига, захват деталей — гасит его обзор на время осмотра. Витрины в сцене
        /// может и не быть, тогда игрок работает на встроенных числах.
        /// </summary>
        /// <summary>
        /// Терминал получает ввод и игрока: пока игрок за компьютером, ему отдают
        /// курсор и отнимают ход. Шрифт — с кириллицей: во встроенном её нет.
        /// </summary>
        private static void BindTerminal(ComputerTerminal terminal, FirstPersonPlayer player)
        {
            SetupAssets.Apply(terminal, so =>
            {
                so.FindProperty("input").objectReferenceValue = player.GetComponent<PlayerInputReader>();
                so.FindProperty("player").objectReferenceValue = player;
                so.FindProperty("font").objectReferenceValue = SetupAssets.EnsureFont();
            });
        }

        private static void BindToScene(FirstPersonPlayer player, ComputerTerminal terminal)
        {
            var bootstrap = Object.FindFirstObjectByType<FiguresBootstrap>(FindObjectsInactive.Include);
            if (bootstrap == null)
            {
                DevLog.Info("[Figures] Витрины в сцене нет — игрок пойдёт на встроенных числах.");
                return;
            }

            SetupAssets.Apply(bootstrap, so =>
            {
                so.FindProperty("player").objectReferenceValue = player;
                so.FindProperty("terminal").objectReferenceValue = terminal;
            });

            var grabber = Object.FindFirstObjectByType<PartGrabber>(FindObjectsInactive.Include);
            SetupAssets.Apply(grabber, so => so.FindProperty("player").objectReferenceValue = player);
        }
    }
}
