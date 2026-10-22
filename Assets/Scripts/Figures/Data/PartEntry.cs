using System;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Деталь в описании фигурки. Префаб необязателен: пусто — рантайм соберёт
    /// примитив по GreyBoxConfig, и механика играбельна до появления арта.
    /// </summary>
    [Serializable]
    public sealed class PartEntry
    {
        [SerializeField] private PartSlotKind kind;

        [Tooltip("Необязательно. Пусто — деталь рисуется примитивом по GreyBoxConfig.")]
        [SerializeField] private GameObject prefab;

        public PartSlotKind Kind => kind;

        public GameObject Prefab => prefab;
    }
}
