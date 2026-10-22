using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Собирает серую комнату из примитивов по <see cref="RoomConfig"/>: пол, четыре
    /// стены с проёмом, перекрытие, свет и блокаут мебели.
    ///
    /// Это заглушка вместо предусловия на уровне сцены: ходить и мерить расстояния
    /// можно до того, как появился хоть один меш. Когда придёт арт, коробка меняется
    /// кусок за куском, и код игрока об этом не знает.
    ///
    /// Пол лежит ровно на нуле, стены стоят снаружи внутренних размеров: внутренний
    /// размер в конфиге — это то, что игрок действительно обходит.
    /// </summary>
    public static class RoomGreyBoxBuilder
    {
        private const string HolderName = "Room";
        private const float SegmentEpsilon = 0.02f;

        /// <param name="layer">Слой коробки и мебели.</param>
        /// <param name="surfaceLayer">
        /// Слой столешницы: по нему ведётся деталь в руке. Отрицательный — тот же, что
        /// у коробки, и тогда витрину на столе вести будет нечем.
        /// </param>
        public static GameObject Build(RoomConfig config, Transform parent, int layer = 0,
            int surfaceLayer = -1)
        {
            if (config == null)
            {
                Debug.LogWarning("[Figures] Конфиг комнаты не задан — комната не собрана.");
                return null;
            }

            var holder = new GameObject(HolderName);
            holder.transform.SetParent(parent, false);

            var inner = config.InnerSize;
            var thickness = config.WallThickness;
            var height = config.WallHeight;

            BuildSlab("Floor", holder.transform, config.FloorMaterial, layer,
                new Vector3(0f, -thickness * 0.5f, 0f),
                new Vector3(inner.x + thickness * 2f, thickness, inner.y + thickness * 2f));

            if (config.BuildCeiling)
                BuildSlab("Ceiling", holder.transform, config.FloorMaterial, layer,
                    new Vector3(0f, height + thickness * 0.5f, 0f),
                    new Vector3(inner.x + thickness * 2f, thickness, inner.y + thickness * 2f));

            BuildWalls(config, holder.transform, layer);

            if (config.BuildLamp) BuildLamp(config, holder.transform);
            if (config.BuildProps)
                BuildProps(config, holder.transform, layer, surfaceLayer >= 0 ? surfaceLayer : layer);
            if (config.BuildComputer) BuildComputer(config, holder.transform, layer);

            return holder;
        }

        // =====================================================================
        // СТЕНЫ
        // =====================================================================

        private static void BuildWalls(RoomConfig config, Transform parent, int layer)
        {
            var inner = config.InnerSize;
            var t = config.WallThickness;

            // Стены по X тянутся на всю длину с запасом на углы, стены по Z — ровно
            // по внутреннему размеру: иначе углы пересекаются и в блокауте это видно.
            BuildWall(config, parent, layer, RoomWall.North, inner.x + t * 2f, true,
                new Vector3(0f, 0f, inner.y * 0.5f + t * 0.5f));
            BuildWall(config, parent, layer, RoomWall.South, inner.x + t * 2f, true,
                new Vector3(0f, 0f, -(inner.y * 0.5f + t * 0.5f)));
            BuildWall(config, parent, layer, RoomWall.East, inner.y, false,
                new Vector3(inner.x * 0.5f + t * 0.5f, 0f, 0f));
            BuildWall(config, parent, layer, RoomWall.West, inner.y, false,
                new Vector3(-(inner.x * 0.5f + t * 0.5f), 0f, 0f));
        }

        private static void BuildWall(RoomConfig config, Transform parent, int layer, RoomWall side,
            float length, bool alongX, Vector3 center)
        {
            var t = config.WallThickness;
            var h = config.WallHeight;
            var name = $"Wall_{side}";

            if (config.DoorwayWall != side)
            {
                BuildWallPiece(name, parent, config.WallMaterial, layer, center, length, h, t, alongX, 0f);
                return;
            }

            var width = Mathf.Min(config.DoorwayWidth, length - SegmentEpsilon * 2f);
            var doorHeight = config.DoorwayHeight;
            var limit = (length - width) * 0.5f;
            var offset = Mathf.Clamp(config.DoorwayOffset, -limit, limit);

            var leftLength = offset - width * 0.5f + length * 0.5f;
            var rightLength = length * 0.5f - (offset + width * 0.5f);

            if (leftLength > SegmentEpsilon)
                BuildWallPiece($"{name}_A", parent, config.WallMaterial, layer, center, leftLength, h, t,
                    alongX, -(length - leftLength) * 0.5f);

            if (rightLength > SegmentEpsilon)
                BuildWallPiece($"{name}_B", parent, config.WallMaterial, layer, center, rightLength, h, t,
                    alongX, (length - rightLength) * 0.5f);

            var lintel = h - doorHeight;
            if (lintel > SegmentEpsilon)
                BuildWallPiece($"{name}_Top", parent, config.WallMaterial, layer,
                    center + Vector3.up * doorHeight, width, lintel, t, alongX, offset);
        }

        private static void BuildWallPiece(string name, Transform parent, Material material, int layer,
            Vector3 center, float length, float height, float thickness, bool alongX, float shift)
        {
            var position = center + new Vector3(alongX ? shift : 0f, height * 0.5f, alongX ? 0f : shift);
            var scale = alongX
                ? new Vector3(length, height, thickness)
                : new Vector3(thickness, height, length);

            BuildSlab(name, parent, material, layer, position, scale);
        }

        // =====================================================================
        // СВЕТ И БЛОКАУТ
        // =====================================================================

        private static void BuildLamp(RoomConfig config, Transform parent)
        {
            var go = new GameObject("Lamp", typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, config.WallHeight - 0.35f, 0f);

            var light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.intensity = config.LampIntensity;
            light.range = config.LampRange;
            light.shadows = LightShadows.Soft;
            light.color = new Color(1f, 0.96f, 0.9f);
        }

        /// <summary>
        /// Мебель нужна не для красоты: в пустой коробке не видно ни масштаба, ни
        /// скорости хода. Стол — не декорация вовсе: это рабочая поверхность, на
        /// которую встаёт витрина с фигурками, поэтому он помечен
        /// <see cref="WorkSurface"/>, а столешница лежит на слое поверхностей.
        /// </summary>
        private static void BuildProps(RoomConfig config, Transform parent, int layer, int surfaceLayer)
        {
            var inner = config.InnerSize;
            var desk = config.DeskSize;
            var material = config.PropMaterial;

            var deskRoot = new GameObject("Desk", typeof(WorkSurface));
            deskRoot.transform.SetParent(parent, false);
            deskRoot.transform.localPosition = new Vector3(0f, 0f, inner.y * 0.5f - desk.z * 0.5f - 0.1f);
            deskRoot.GetComponent<WorkSurface>().Setup(desk.y, new Vector2(desk.x, desk.z));

            BuildSlab("Top", deskRoot.transform, material, surfaceLayer,
                new Vector3(0f, desk.y - 0.03f, 0f), new Vector3(desk.x, 0.06f, desk.z));

            var legHeight = desk.y - 0.06f;
            var legShift = desk.x * 0.5f - 0.12f;
            BuildSlab("Side_L", deskRoot.transform, material, layer,
                new Vector3(-legShift, legHeight * 0.5f, 0f), new Vector3(0.08f, legHeight, desk.z * 0.9f));
            BuildSlab("Side_R", deskRoot.transform, material, layer,
                new Vector3(legShift, legHeight * 0.5f, 0f), new Vector3(0.08f, legHeight, desk.z * 0.9f));

            BuildSlab("Shelf", parent, material, layer,
                new Vector3(-inner.x * 0.5f + 0.2f, 1.5f, 0f), new Vector3(0.36f, 0.06f, 1.8f));

            BuildSlab("Crate_A", parent, material, layer,
                new Vector3(inner.x * 0.5f - 0.5f, 0.3f, -inner.y * 0.5f + 0.5f), Vector3.one * 0.6f);
            BuildSlab("Crate_B", parent, material, layer,
                new Vector3(inner.x * 0.5f - 0.95f, 0.22f, -inner.y * 0.5f + 0.75f), Vector3.one * 0.44f);
        }

        /// <summary>
        /// Компьютер у западной стены: стол, монитор, клавиатура и сам
        /// <see cref="ComputerTerminal"/>. Стоит отдельно от витрины намеренно — заказ
        /// коробок и съёмка это другое занятие, и к нему игрок отходит от стола.
        ///
        /// Повёрнут лицом в комнату: экран смотрит в +X, то есть на подошедшего.
        /// </summary>
        private static void BuildComputer(RoomConfig config, Transform parent, int layer)
        {
            var inner = config.InnerSize;
            var desk = config.ComputerDeskSize;
            var material = config.PropMaterial;

            var root = new GameObject("Computer", typeof(ComputerTerminal));
            root.transform.SetParent(parent, false);
            root.transform.SetLocalPositionAndRotation(
                new Vector3(-inner.x * 0.5f + desk.z * 0.5f + 0.05f, 0f, 0f),
                Quaternion.Euler(0f, 90f, 0f));

            BuildSlab("Top", root.transform, material, layer,
                new Vector3(0f, desk.y - 0.03f, 0f), new Vector3(desk.x, 0.06f, desk.z));

            var legHeight = desk.y - 0.06f;
            var legShift = desk.x * 0.5f - 0.08f;
            BuildSlab("Side_L", root.transform, material, layer,
                new Vector3(-legShift, legHeight * 0.5f, 0f), new Vector3(0.06f, legHeight, desk.z * 0.9f));
            BuildSlab("Side_R", root.transform, material, layer,
                new Vector3(legShift, legHeight * 0.5f, 0f), new Vector3(0.06f, legHeight, desk.z * 0.9f));

            BuildSlab("Stand", root.transform, material, layer,
                new Vector3(0f, desk.y + 0.06f, 0.12f), new Vector3(0.12f, 0.12f, 0.12f));
            BuildSlab("Screen", root.transform, material, layer,
                new Vector3(0f, desk.y + 0.29f, 0.12f), new Vector3(0.52f, 0.34f, 0.03f));
            BuildSlab("Keyboard", root.transform, material, layer,
                new Vector3(0f, desk.y + 0.02f, -0.16f), new Vector3(0.34f, 0.02f, 0.13f));
        }

        private static GameObject BuildSlab(string name, Transform parent, Material material, int layer,
            Vector3 position, Vector3 scale)
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
    }
}
