using System.Collections.Generic;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Одна фигурка: её гнёзда и палитра. В сером прототипе палитра — единственный
    /// признак, по которому игрок опознаёт, чья деталь, поэтому у фигурок одной серии
    /// цвета должны быть близкими: без похожести ошибка не выглядит правдоподобной,
    /// а без правдоподобной ошибки проверять нечего.
    /// </summary>
    [CreateAssetMenu(menuName = "Figures/Figure", fileName = "Figure")]
    public sealed class FigureDef : ScriptableObject
    {
        [Tooltip("Пусто — берётся имя ассета.")]
        [SerializeField] private string id;

        [Tooltip("Серия. Фигурки одной серии делят базу тела и близкие цвета.")]
        [SerializeField] private string seriesId;

        [Tooltip("Цвет деталей этой фигурки в сером прототипе.")]
        [SerializeField] private Color tint = Color.white;

        [SerializeField] private List<PartEntry> parts = new();

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public string SeriesId => seriesId;

        public Color Tint => tint;

        public IReadOnlyList<PartEntry> Parts => parts;

        /// <summary>
        /// Id детали собирается из фигурки, типа и номера: вручную их не вводят и не
        /// путают. Номер различает парные детали одного комплекта — на то, куда они
        /// входят, он не влияет.
        /// </summary>
        public static string PartId(string figureId, PartSlotKind kind, int index) =>
            $"{figureId}:{kind}:{index}";
    }
}
