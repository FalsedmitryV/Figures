using System.Collections.Generic;

namespace Figures
{
    /// <summary>
    /// Всё, что ядро спрашивает у данных. Реализуется конфигом в Data, поэтому ядро
    /// остаётся без зависимости от ScriptableObject и прогоняется тестом без сцены.
    /// </summary>
    public interface IFigureCatalog
    {
        IReadOnlyList<string> FigureIds { get; }

        IReadOnlyList<string> PartIds { get; }

        IReadOnlyList<PartSlotKind> SlotsOf(string figureId);

        PartSpec PartOf(string partId);
    }
}
