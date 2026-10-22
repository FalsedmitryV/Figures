using TMPro;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Счётчик собранного и вердикт проверки. Необязателен: без Canvas прототип
    /// играбелен, вердикт уходит в консоль через загрузчик.
    ///
    /// Вердикт показывается только когда все гнёзда заполнены. Постоянный индикатор
    /// ошибок превратил бы опознание деталей в перебор.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text progress;
        [SerializeField] private TMP_Text verdict;

        [Tooltip("{0} — собрано, {1} — всего.")]
        [SerializeField] private string progressFormat = "Собрано {0} из {1}";

        [SerializeField] private string solvedText = "Всё по своим местам";

        [Tooltip("{0} — число деталей не на своей фигурке.")]
        [SerializeField] private string mistakesFormat = "Чужих деталей: {0}";

        private AssemblyBoard _board;

        public void Bind(AssemblyBoard board)
        {
            if (_board != null) _board.Changed -= Redraw;

            _board = board;
            if (_board != null) _board.Changed += Redraw;

            Redraw();
        }

        private void OnDestroy()
        {
            if (_board != null) _board.Changed -= Redraw;
        }

        private void Redraw()
        {
            if (_board == null) return;

            if (progress != null)
                progress.text = string.Format(progressFormat, _board.PlacedCount, _board.SlotTotal);

            if (verdict == null) return;

            if (!_board.IsFull)
            {
                verdict.text = string.Empty;
                return;
            }

            var mistakes = _board.MistakeCount;
            verdict.text = mistakes == 0 ? solvedText : string.Format(mistakesFormat, mistakes);
        }
    }
}
