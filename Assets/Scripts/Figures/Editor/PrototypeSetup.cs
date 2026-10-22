using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Figures.EditorTools
{
    /// <summary>
    /// Сборка витрины этапа 1: конфиги сборки фигурок, слои и сцена — поднос, подставки
    /// с гнёздами и загрузчик. Пункты меню живут в <see cref="FiguresMenu"/>.
    ///
    /// Витрина встаёт на <see cref="WorkSurface"/>, если такая в сцене есть: тогда стол
    /// принадлежит комнате, а витрина — её гостю, и своего стола она не строит. Нет
    /// поверхности — собирается автономный вариант со своим столом и неподвижной
    /// камерой, то есть этап 1 в исходном виде.
    /// </summary>
    internal static class PrototypeSetup
    {
        private const string HolderName = "Figures Prototype";

        private const float StandSpacing = 0.5f;

        /// <summary>Своя столешница автономного варианта: её высота и есть «ноль» витрины.</summary>
        private const float TableTop = 0.02f;

        // Раскладка на столешнице: подставки у дальнего края, детали по центру, ближе
        // к игроку. Поднос по центру намеренно: до него тянуться с любой стороны.
        private const float StandsOffsetZ = 0.3f;
        private const float TrayOffsetZ = -0.22f;

        /// <summary>
        /// Зазор подноса над столешницей. Малый намеренно: детали откреплены, и каждый
        /// лишний сантиметр здесь — это сантиметр падения при первом же отпускании.
        /// </summary>
        private const float TrayLift = 0.002f;

        /// <summary>
        /// Гнездо лица смотрит в локальный +Z подставки, поэтому подставка развёрнута:
        /// фигурка стоит лицом к тому, кто подошёл к столу, а не затылком.
        /// </summary>
        private const float StandYaw = 180f;

        private static readonly Vector2 TraySize = new(1.1f, 0.42f);

        // Комплект фигурки: по две руки и ноги. Повторы типа — это парные гнёзда,
        // и деталь входит в любое из них.
        private static readonly PartSlotKind[] SlotLayout =
        {
            PartSlotKind.Head, PartSlotKind.Hair, PartSlotKind.Face, PartSlotKind.Torso,
            PartSlotKind.Pelvis, PartSlotKind.Arm, PartSlotKind.Arm, PartSlotKind.Leg, PartSlotKind.Leg
        };

        private static readonly PartSlotKind[] AllKinds =
        {
            PartSlotKind.Head, PartSlotKind.Hair, PartSlotKind.Face,
            PartSlotKind.Torso, PartSlotKind.Pelvis, PartSlotKind.Arm, PartSlotKind.Leg
        };

        // Раскладка гнёзд на подставке: грубый гуманоид высотой ~0.27 юнита. Торс и таз
        // разведены, поэтому торс стал короче, а не фигурка выше.
        private static readonly (PartSlotKind kind, int index, Vector3 offset)[] SocketLayout =
        {
            (PartSlotKind.Leg, 0, new Vector3(-0.025f, 0.032f, 0f)),
            (PartSlotKind.Leg, 1, new Vector3(0.025f, 0.032f, 0f)),
            (PartSlotKind.Pelvis, 0, new Vector3(0f, 0.084f, 0f)),
            (PartSlotKind.Torso, 0, new Vector3(0f, 0.139f, 0f)),
            (PartSlotKind.Arm, 0, new Vector3(-0.06f, 0.15f, 0f)),
            (PartSlotKind.Arm, 1, new Vector3(0.06f, 0.15f, 0f)),
            (PartSlotKind.Head, 0, new Vector3(0f, 0.214f, 0f)),
            (PartSlotKind.Face, 0, new Vector3(0f, 0.214f, 0.04f)),
            (PartSlotKind.Hair, 0, new Vector3(0f, 0.247f, 0f))
        };

        private static readonly Dictionary<PartSlotKind, (PrimitiveType primitive, Vector3 scale)> Shapes = new()
        {
            { PartSlotKind.Head, (PrimitiveType.Sphere, new Vector3(0.08f, 0.08f, 0.08f)) },
            { PartSlotKind.Hair, (PrimitiveType.Cube, new Vector3(0.095f, 0.045f, 0.095f)) },
            { PartSlotKind.Face, (PrimitiveType.Cube, new Vector3(0.055f, 0.045f, 0.012f)) },
            { PartSlotKind.Torso, (PrimitiveType.Cube, new Vector3(0.09f, 0.07f, 0.06f)) },
            { PartSlotKind.Pelvis, (PrimitiveType.Cube, new Vector3(0.075f, 0.04f, 0.055f)) },
            { PartSlotKind.Arm, (PrimitiveType.Capsule, new Vector3(0.028f, 0.033f, 0.028f)) },
            { PartSlotKind.Leg, (PrimitiveType.Capsule, new Vector3(0.032f, 0.032f, 0.032f)) }
        };

        // Коробки магазина: цена против содержимого. Малая берётся «добить» один тип,
        // ящик — рывком, когда накопил на нём же.
        private static readonly (string id, string title, int price, int count)[] Boxes =
        {
            ("small", "Малая посылка", 60, 3),
            ("standard", "Стандартная коробка", 140, 8),
            ("collector", "Ящик коллекционера", 260, 16)
        };

        // Одна серия: цвета намеренно близкие. Разведи их — и опознание станет
        // тривиальным, а проверять будет нечего.
        private static readonly (string id, string series, Color tint)[] Figures =
        {
            ("Rin", "Koharu", new Color(0.92f, 0.46f, 0.52f)),
            ("Ami", "Koharu", new Color(0.88f, 0.55f, 0.42f)),
            ("Yuki", "Koharu", new Color(0.82f, 0.44f, 0.64f))
        };

        // =====================================================================
        // КОНФИГИ
        // =====================================================================

        internal static PrototypeConfig CreateConfigs()
        {
            var greyBox = CreateGreyBox();
            var interaction = CreateInteraction();
            var catalog = CreateCatalog();
            var economy = CreateEconomy();

            var root = SetupAssets.LoadOrCreate<PrototypeConfig>("PrototypeConfig.asset");
            SetupAssets.Apply(root, so =>
            {
                so.FindProperty("catalog").objectReferenceValue = catalog;
                so.FindProperty("greyBox").objectReferenceValue = greyBox;
                so.FindProperty("interaction").objectReferenceValue = interaction;
                so.FindProperty("economy").objectReferenceValue = economy;
            });

            return root;
        }

        private static EconomyConfig CreateEconomy()
        {
            var asset = SetupAssets.LoadOrCreate<EconomyConfig>("EconomyConfig.asset");

            SetupAssets.Apply(asset, so =>
            {
                // Старт подобран под первый оборот: 10 деталей на витрине дают фото
                // примерно на 60 монет, и вместе с начальным счётом хватает на
                // стандартную коробку.
                so.FindProperty("startBalance").intValue = 150;
                so.FindProperty("starterPartCount").intValue = 10;
                so.FindProperty("seed").intValue = 1;

                so.FindProperty("likesPerAssembled").intValue = 80;
                so.FindProperty("likesPerPlacedPart").intValue = 12;
                so.FindProperty("likesPerMistake").intValue = 20;
                so.FindProperty("coinsPerLike").floatValue = 1.5f;
                so.FindProperty("useInventory").boolValue = true;

                var list = so.FindProperty("boxes");
                list.arraySize = Boxes.Length;

                for (int i = 0; i < Boxes.Length; i++)
                {
                    var element = list.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("id").stringValue = Boxes[i].id;
                    element.FindPropertyRelative("title").stringValue = Boxes[i].title;
                    element.FindPropertyRelative("price").intValue = Boxes[i].price;
                    element.FindPropertyRelative("partCount").intValue = Boxes[i].count;
                }
            });

            return asset;
        }

        private static GreyBoxConfig CreateGreyBox()
        {
            var asset = SetupAssets.LoadOrCreate<GreyBoxConfig>("GreyBoxConfig.asset");

            SetupAssets.Apply(asset, so =>
            {
                var shapes = so.FindProperty("shapes");
                shapes.arraySize = AllKinds.Length;

                for (int i = 0; i < AllKinds.Length; i++)
                {
                    var kind = AllKinds[i];
                    var element = shapes.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("kind").enumValueIndex = (int)kind;
                    element.FindPropertyRelative("primitive").enumValueIndex = (int)Shapes[kind].primitive;
                    element.FindPropertyRelative("scale").vector3Value = Shapes[kind].scale;
                }
            });

            return asset;
        }

        private static InteractionConfig CreateInteraction()
        {
            var asset = SetupAssets.LoadOrCreate<InteractionConfig>("InteractionConfig.asset");

            SetupAssets.Apply(asset, so =>
            {
                // 0.12 было почти в полроста фигурки: деталь защёлкивалась откуда угодно
                // рядом, и от постановки на место не оставалось самого действия. 0.06 —
                // целиться надо, но подъём по нормали (0.02) прощается.
                so.FindProperty("snapRadius").floatValue = 0.06f;
                so.FindProperty("carryLift").floatValue = 0.02f;
                so.FindProperty("snapDuration").floatValue = 0.12f;
                so.FindProperty("returnDuration").floatValue = 0.2f;
                so.FindProperty("inspectDistance").floatValue = 0.3f;
                so.FindProperty("inspectZoomRange").vector2Value = new Vector2(0.14f, 0.5f);
                so.FindProperty("inspectRotateSpeed").floatValue = 0.4f;

                // Дотянуться можно до стола, а не через комнату: игрок больше не сидит
                // перед витриной, он к ней подходит.
                so.FindProperty("reachDistance").floatValue = 2.2f;
                so.FindProperty("dropOnRelease").boolValue = true;
            });

            return asset;
        }

        private static FigureCatalog CreateCatalog()
        {
            var defs = new List<FigureDef>(Figures.Length);

            foreach (var (id, series, tint) in Figures)
            {
                var def = SetupAssets.LoadOrCreate<FigureDef>($"Figure_{id}.asset");

                SetupAssets.Apply(def, so =>
                {
                    so.FindProperty("id").stringValue = id;
                    so.FindProperty("seriesId").stringValue = series;
                    so.FindProperty("tint").colorValue = tint;

                    var parts = so.FindProperty("parts");
                    parts.arraySize = SlotLayout.Length;
                    for (int i = 0; i < SlotLayout.Length; i++)
                        parts.GetArrayElementAtIndex(i).FindPropertyRelative("kind").enumValueIndex =
                            (int)SlotLayout[i];
                });

                defs.Add(def);
            }

            var catalog = SetupAssets.LoadOrCreate<FigureCatalog>("FigureCatalog.asset");
            SetupAssets.Apply(catalog, so =>
            {
                var list = so.FindProperty("figures");
                list.arraySize = defs.Count;
                for (int i = 0; i < defs.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            });

            return catalog;
        }

        private static void ApplyLayers(InteractionConfig interaction, int partLayer, int surfaceLayer) =>
            SetupAssets.Apply(interaction, so =>
            {
                so.FindProperty("partLayers").intValue = 1 << partLayer;
                so.FindProperty("surfaceLayers").intValue = 1 << surfaceLayer;
            });

        // =====================================================================
        // СЦЕНА
        // =====================================================================

        internal static bool BuildShowcase()
        {
            var root = SetupAssets.Load<PrototypeConfig>("PrototypeConfig.asset");
            if (root == null || !root.IsComplete)
            {
                Debug.LogError("[Figures] Конфиги сборки фигурок не готовы — сначала создай конфиги.");
                return false;
            }

            var partLayer = SetupAssets.EnsureLayer(SetupAssets.PartLayerName);
            var surfaceLayer = SetupAssets.EnsureLayer(SetupAssets.SurfaceLayerName);
            ApplyLayers(root.Interaction, partLayer, surfaceLayer);

            var holder = SetupScene.Replace(HolderName, "Собрать витрину");

            var surface = Object.FindFirstObjectByType<WorkSurface>(FindObjectsInactive.Exclude);
            var player = Object.FindFirstObjectByType<FirstPersonPlayer>(FindObjectsInactive.Include);

            // Стол и свет витрина строит только тогда, когда их некому дать, а камеру —
            // только когда в сцене нет игрока: две камеры с тегом MainCamera это
            // молчаливая лотерея, какая из них рисует кадр.
            if (surface == null)
            {
                BuildTable(holder.transform, surfaceLayer);
                SetupScene.Directional("Directional Light", holder.transform,
                    Quaternion.Euler(48f, -28f, 0f), 1.1f);
            }

            if (player == null) BuildFixedCamera(holder.transform);

            var tray = BuildTray(holder.transform, partLayer, TrayAnchor(surface));
            var stands = BuildStands(holder.transform, root.Catalog, surfaceLayer, surface);
            var terminal = Object.FindFirstObjectByType<ComputerTerminal>(FindObjectsInactive.Include);
            BuildBootstrap(holder.transform, root, tray, stands, player, terminal);

            Selection.activeGameObject = holder;
            if (player == null)
                SetupScene.Frame(new Bounds(TrayAnchor(surface), new Vector3(2f, 0.6f, 1.2f)));

            return true;
        }

        private static Vector3 TrayAnchor(WorkSurface surface) =>
            surface != null
                ? surface.PointOn(new Vector2(0f, TrayOffsetZ)) + Vector3.up * TrayLift
                : new Vector3(0f, TableTop + 0.01f, TrayOffsetZ);

        private static Vector3 StandAnchor(WorkSurface surface, float shift) =>
            surface != null
                ? surface.PointOn(new Vector2(shift, StandsOffsetZ))
                : new Vector3(shift, TableTop, 0.24f);

        private static void BuildTable(Transform parent, int layer) =>
            SetupScene.Cube("Table", parent, new Vector3(0f, TableTop * 0.5f, 0f),
                new Vector3(1.9f, TableTop, 1.1f), null, layer);

        /// <summary>
        /// Неподвижная камера автономной витрины: фигурка высотой 0.25 юнита с
        /// дефолтной камеры в 10 юнитах — пустой экран, и читается это как «камеры нет».
        /// </summary>
        private static void BuildFixedCamera(Transform parent)
        {
            var camera = Camera.main ?? Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);

            if (camera == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                go.transform.SetParent(parent, false);
                camera = go.GetComponent<Camera>();
            }
            else
            {
                Undo.RecordObject(camera.transform, "Поставить камеру витрины");
                Undo.RecordObject(camera, "Настроить камеру витрины");
            }

            if (!camera.CompareTag("MainCamera")) camera.tag = "MainCamera";

            var eye = new Vector3(0f, 0.55f, -1.0f);
            camera.transform.SetPositionAndRotation(eye,
                Quaternion.LookRotation(new Vector3(0f, 0.12f, 0.05f) - eye, Vector3.up));

            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            camera.backgroundColor = new Color(0.14f, 0.13f, 0.16f);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }

        private static PartSpawner BuildTray(Transform parent, int layer, Vector3 position)
        {
            var go = new GameObject("Tray", typeof(PartSpawner)) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var spawner = go.GetComponent<PartSpawner>();
            SetupAssets.Apply(spawner, so =>
            {
                // Поднос подобран под столешницу: расширишь — детали поедут за край и
                // упадут на пол, потому что они откреплены.
                so.FindProperty("traySize").vector2Value = TraySize;
                so.FindProperty("seed").intValue = 1;
            });

            return spawner;
        }

        private static List<FigureStandView> BuildStands(Transform parent, FigureCatalog catalog,
            int surfaceLayer, WorkSurface surface)
        {
            var stands = new List<FigureStandView>();
            var ids = catalog.FigureIds;
            var offset = -(ids.Count - 1) * 0.5f * StandSpacing;
            var yaw = StandYaw + (surface != null ? surface.transform.eulerAngles.y : 0f);

            for (int i = 0; i < ids.Count; i++)
            {
                var stand = BuildStand(ids[i], parent, surfaceLayer);
                stand.transform.SetPositionAndRotation(
                    StandAnchor(surface, offset + i * StandSpacing),
                    Quaternion.Euler(0f, yaw, 0f));

                stands.Add(stand);
            }

            return stands;
        }

        private static FigureStandView BuildStand(string figureId, Transform parent, int surfaceLayer)
        {
            var go = new GameObject($"Stand_{figureId}", typeof(FigureStandView));
            go.transform.SetParent(parent, false);

            SetupAssets.Apply(go.GetComponent<FigureStandView>(),
                so => so.FindProperty("figureId").stringValue = figureId);

            SetupScene.Cube("Base", go.transform, new Vector3(0f, 0.005f, 0f),
                new Vector3(0.16f, 0.01f, 0.16f), null, surfaceLayer);

            // Деталь в руке ведётся по поверхности, а гнёзда головы и рук висят в
            // воздухе. Без вертикальной плоскости в плоскости гнёзд до них нельзя
            // дотянуться указателем вообще — невидимая, только коллайдер.
            var board = SetupScene.Cube("DragPlane", go.transform, new Vector3(0f, 0.15f, 0f),
                new Vector3(0.22f, 0.32f, 0.01f), null, surfaceLayer);
            board.GetComponent<Renderer>().enabled = false;

            foreach (var (kind, index, offset) in SocketLayout) BuildSocket(kind, index, offset, go.transform);

            return go.GetComponent<FigureStandView>();
        }

        private static void BuildSocket(PartSlotKind kind, int index, Vector3 offset, Transform stand)
        {
            var go = new GameObject($"Socket_{kind}_{index}", typeof(SocketView));
            go.transform.SetParent(stand, false);
            go.transform.localPosition = offset;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Ready";
            marker.transform.SetParent(go.transform, false);
            marker.transform.localScale = Vector3.one * 0.03f;
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            SetupAssets.Apply(go.GetComponent<SocketView>(), so =>
            {
                so.FindProperty("kind").enumValueIndex = (int)kind;
                so.FindProperty("index").intValue = index;
                so.FindProperty("readyMarker").objectReferenceValue = marker.GetComponent<Renderer>();
            });
        }

        private static void BuildBootstrap(Transform parent, PrototypeConfig root, PartSpawner spawner,
            List<FigureStandView> stands, FirstPersonPlayer player, ComputerTerminal terminal)
        {
            var go = new GameObject("Bootstrap",
                typeof(FiguresInputReader), typeof(PartGrabber), typeof(FiguresBootstrap));
            go.transform.SetParent(parent, false);

            var reader = go.GetComponent<FiguresInputReader>();
            SetupAssets.Apply(reader, so => so.FindProperty("actions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Object>(SetupAssets.InputAssetPath));

            var grabber = go.GetComponent<PartGrabber>();
            SetupAssets.Apply(grabber, so =>
            {
                so.FindProperty("input").objectReferenceValue = reader;

                // Камера не прописывается: её берёт Camera.main — глаза игрока, когда он
                // в сцене есть, и камера витрины, когда нет.
                so.FindProperty("player").objectReferenceValue = player;
            });

            SetupAssets.Apply(go.GetComponent<FiguresBootstrap>(), so =>
            {
                so.FindProperty("config").objectReferenceValue = root;
                so.FindProperty("spawner").objectReferenceValue = spawner;
                so.FindProperty("grabber").objectReferenceValue = grabber;
                so.FindProperty("player").objectReferenceValue = player;
                so.FindProperty("terminal").objectReferenceValue = terminal;

                var list = so.FindProperty("stands");
                list.arraySize = stands.Count;
                for (int i = 0; i < stands.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = stands[i];
            });
        }
    }
}
