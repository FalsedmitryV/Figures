using System;

namespace Figures
{
    /// <summary>
    /// Снимок коллекции на момент съёмки: что именно стоит на подставках. Чистые
    /// числа, из которых считаются лайки, — ни камеры, ни кадра, ни сцены.
    /// </summary>
    public readonly struct CollectionShot
    {
        /// <summary>Фигурки, собранные целиком и только из своих деталей.</summary>
        public readonly int Assembled;

        /// <summary>Фигурки, на которых стоит хоть одна деталь.</summary>
        public readonly int Displayed;

        public readonly int PartsPlaced;

        /// <summary>Деталей не от своей фигурки. Чужая рука на витрине заметна.</summary>
        public readonly int Mistakes;

        public CollectionShot(int assembled, int displayed, int partsPlaced, int mistakes)
        {
            Assembled = assembled;
            Displayed = displayed;
            PartsPlaced = partsPlaced;
            Mistakes = mistakes;
        }

        public bool IsEmpty => PartsPlaced == 0;

        /// <summary>
        /// Посчитать снимок по доске сборки. Собранность — по комплекту: левая рука в
        /// правом плече своей же фигурки ошибкой не считается.
        /// </summary>
        public static CollectionShot Of(AssemblyBoard board) => Of(board, null);

        /// <summary>
        /// Снимок того, что попало в кадр. Деталь вне кадра не стоит ни лайка, ни штрафа,
        /// а собранной фигурка считается, только если в кадре она вся.
        /// </summary>
        /// <param name="inFrame">Видна ли деталь. null — в кадре вся витрина.</param>
        public static CollectionShot Of(AssemblyBoard board, Func<string, bool> inFrame)
        {
            if (board == null) return default;

            int assembled = 0, displayed = 0, placed = 0, mistakes = 0;

            foreach (var figure in board.Figures)
            {
                int filled = 0, own = 0;

                for (int slot = 0; slot < figure.SlotCount; slot++)
                {
                    var partId = figure.PartAtSlot(slot);
                    if (partId == null || (inFrame != null && !inFrame(partId))) continue;

                    filled++;
                    if (board.OwnerOf(partId) == figure.FigureId) own++;
                }

                placed += filled;
                mistakes += filled - own;
                if (filled > 0) displayed++;
                if (figure.IsFull && own == figure.SlotCount) assembled++;
            }

            return new CollectionShot(assembled, displayed, placed, mistakes);
        }
    }
}
