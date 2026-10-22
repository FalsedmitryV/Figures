using System.Collections.Generic;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Подставка под одну фигурку: держит свои гнёзда. Список пуст — соберётся из
    /// потомков, чтобы раскладка гнёзд оставалась работой рук в редакторе.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FigureStandView : MonoBehaviour
    {
        [Tooltip("Id фигурки из каталога. Пусто — берётся имя объекта.")]
        [SerializeField] private string figureId;

        [Tooltip("Пусто — гнёзда соберутся из потомков.")]
        [SerializeField] private List<SocketView> sockets = new();

        public string FigureId => string.IsNullOrEmpty(figureId) ? name : figureId;

        public IReadOnlyList<SocketView> Sockets => sockets;

        private void Awake()
        {
            if (sockets.Count == 0)
                sockets.AddRange(GetComponentsInChildren<SocketView>(includeInactive: true));

            foreach (var socket in sockets)
                if (socket != null) socket.Stand = this;
        }

        public SocketView Socket(PartSlotKind kind, int index)
        {
            foreach (var socket in sockets)
                if (socket != null && socket.Kind == kind && socket.Index == index) return socket;
            return null;
        }
    }
}
