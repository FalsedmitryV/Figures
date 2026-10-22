using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Рабочая поверхность: столешница, на которую ставится витрина с фигурками.
    ///
    /// Нужна затем, чтобы сборка витрины не искала стол по имени объекта. Стол
    /// принадлежит комнате, витрина — отдельному сетапу, и единственное, что им надо
    /// согласовать, — где верх столешницы и сколько на ней места.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorkSurface : MonoBehaviour
    {
        [Tooltip("Высота верха столешницы над основанием объекта, метры.")]
        [Min(0f)][SerializeField] private float topHeight = 0.78f;

        [Tooltip("Размер столешницы по локальным X и Z, метры.")]
        [SerializeField] private Vector2 size = new(2f, 1.2f);

        public float TopHeight => topHeight;

        public Vector2 Size => size;

        public Vector3 TopCenter => transform.position + transform.up * topHeight;

        /// <summary>Точка на столешнице по локальному смещению от центра.</summary>
        public Vector3 PointOn(Vector2 offset) =>
            TopCenter + transform.right * offset.x + transform.forward * offset.y;

        public void Setup(float top, Vector2 plane)
        {
            topHeight = top;
            size = plane;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(TopCenter, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 0.01f, size.y));
        }
    }
}
