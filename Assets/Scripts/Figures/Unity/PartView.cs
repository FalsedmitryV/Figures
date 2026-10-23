using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Деталь на сцене: знает свой id и тип, умеет подсвечиваться и доезжать до позы.
    /// Доводка живёт здесь, а не в захвате: деталь, которую схватили посреди движения,
    /// должна сама отменить своё движение, иначе два источника спорят за трансформ.
    ///
    /// Деталь откреплена: отпущенная мимо гнезда она падает физикой туда, где её
    /// отпустили. За трансформ при этом спорить нельзя, поэтому телом и доводкой
    /// владеет один класс — этот: пока деталь ведут или доводят, тело кинематическое,
    /// и физика включается ровно в момент отпускания.
    ///
    /// Прикреплённая к другой детали своего тела не имеет: она висит в гнезде держателя,
    /// а её коллайдеры входят в составное тело корня куска. Поэтому собранный вне
    /// подставки кусок падает целиком, а не рассыпается.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartView : MonoBehaviour
    {
        private readonly List<Renderer> _renderers = new();
        private Collider[] _colliders;
        private SocketView[] _ports = Array.Empty<SocketView>();
        private Rigidbody _body;
        private Color _baseTint = Color.white;
        private CancellationTokenSource _moveCts;

        public string PartId { get; private set; }

        public PartSlotKind Kind { get; private set; }

        public string OwnerFigureId { get; private set; }

        /// <summary>Цвет детали без подсветки: им ячейка показывает, что в ней лежит.</summary>
        public Color Tint => _baseTint;

        /// <summary>Гнёзда под детали, которые крепятся к этой.</summary>
        public IReadOnlyList<SocketView> Ports => _ports;

        /// <summary>Поза на подносе: куда деталь возвращается, если её отпустили мимо гнезда.</summary>
        public Vector3 HomePosition { get; private set; }

        public Quaternion HomeRotation { get; private set; }

        public void Bind(PartSpec spec, Color tint)
        {
            PartId = spec.Id;
            Kind = spec.Kind;
            OwnerFigureId = spec.OwnerFigureId;
            _baseTint = tint;

            // Метки гнёзд — не тело детали: их красит гнездо, а не наведение.
            _renderers.Clear();
            foreach (var renderer in GetComponentsInChildren<Renderer>(includeInactive: true))
                if (renderer.GetComponentInParent<SocketView>(includeInactive: true) == null)
                    _renderers.Add(renderer);

            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            _ports = GetComponentsInChildren<SocketView>(includeInactive: true);
            foreach (var port in _ports) port.Holder = this;

            EnsureBody();
            foreach (var renderer in _renderers) GreyBoxBuilder.Tint(renderer, tint);
            MarkHome();
        }

        /// <summary>Видимый объём детали без меток гнёзд: по нему камера решает, попала ли деталь в кадр.</summary>
        public Bounds Bounds
        {
            get
            {
                if (_renderers.Count == 0) return new Bounds(transform.position, Vector3.zero);

                var bounds = _renderers[0].bounds;
                for (int i = 1; i < _renderers.Count; i++) bounds.Encapsulate(_renderers[i].bounds);
                return bounds;
            }
        }

        public SocketView Port(PartSlotKind kind, int index)
        {
            foreach (var port in _ports)
                if (port.Kind == kind && port.Index == index) return port;
            return null;
        }

        /// <summary>
        /// Деталь в руке: телом правит трансформ, а не физика. Кинематическим тело
        /// остаётся и пока деталь доводится до гнезда.
        /// </summary>
        public void Hold()
        {
            StopMove();
            EnsureBody();

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.isKinematic = true;
        }

        /// <summary>Отпустить в воздух: дальше деталь падает сама, без начального толчка.</summary>
        public void Drop()
        {
            StopMove();
            EnsureBody();

            _body.isKinematic = false;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.WakeUp();
        }

        /// <summary>
        /// Встать в гнездо держателя. Своё тело деталь отдаёт: дальше её несёт корень
        /// куска, а её коллайдеры входят в его составное тело.
        /// </summary>
        public void AttachTo(Transform port, float duration)
        {
            StopMove();
            RemoveBody();

            transform.SetParent(port, worldPositionStays: true);
            MoveLocal(Vector3.zero, Quaternion.identity, duration, dropAtEnd: false);
        }

        /// <summary>
        /// Корень куска: встать под parent в заданную позу. Кусок вне подставки после
        /// доводки отпускается в физику, на подставке — остаётся неподвижным.
        /// </summary>
        public void SettleAt(Transform parent, Vector3 position, Quaternion rotation, float duration, bool anchored)
        {
            Hold();

            transform.SetParent(parent, worldPositionStays: true);
            MoveLocal(ToLocal(parent, position), ToLocal(parent, rotation), duration, dropAtEnd: !anchored);
        }

        /// <summary>Запомнить текущую позу как место на подносе.</summary>
        public void MarkHome()
        {
            HomePosition = transform.position;
            HomeRotation = transform.rotation;
        }

        public void SetHighlighted(bool on, float boost)
        {
            var color = on ? _baseTint * boost : _baseTint;
            color.a = _baseTint.a;
            foreach (var renderer in _renderers) GreyBoxBuilder.Tint(renderer, color);
        }

        /// <summary>Деталь в руке не должна мешать лучу, которым её же ведут по поверхности.</summary>
        public void SetPickable(bool on)
        {
            if (_colliders == null) return;
            foreach (var collider in _colliders) collider.enabled = on;
        }

        public void SnapTo(Vector3 position, Quaternion rotation, float duration)
        {
            var parent = transform.parent;
            MoveLocal(ToLocal(parent, position), ToLocal(parent, rotation), duration, dropAtEnd: false);
        }

        public void StopMove() => _moveCts?.Cancel();

        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            StopMove();
            transform.SetPositionAndRotation(position, rotation);
        }

        private void OnDestroy() => StopMove();

        private void EnsureBody()
        {
            if (_body != null) return;

            // Через TryGetComponent, а не GetComponent ?? AddComponent: отсутствующий
            // компонент приходит из движка не настоящим null, `??` его не ловит, и
            // первое же обращение валит спавн целиком на первой же детали.
            if (!TryGetComponent(out _body)) _body = gameObject.AddComponent<Rigidbody>();

            // Пока деталь лежит на подносе, физика ей не нужна: раскладка сетки
            // детерминирована, и просыпаться двум десяткам тел незачем.
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.linearDamping = 0.05f;

            // Шар головы и капсулы рук катались бы по столешнице до края: гашение
            // вращения останавливает их там, куда их положили.
            _body.angularDamping = 4f;
        }

        private void RemoveBody()
        {
            if (_body == null) return;

            Destroy(_body);
            _body = null;
        }

        private void MoveLocal(Vector3 position, Quaternion rotation, float duration, bool dropAtEnd)
        {
            StopMove();

            if (duration <= 0f)
            {
                transform.SetLocalPositionAndRotation(position, rotation);
                if (dropAtEnd) Drop();
                return;
            }

            _moveCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            Glide(position, rotation, duration, dropAtEnd, _moveCts).Forget();
        }

        private static Vector3 ToLocal(Transform parent, Vector3 position) =>
            parent != null ? parent.InverseTransformPoint(position) : position;

        private static Quaternion ToLocal(Transform parent, Quaternion rotation) =>
            parent != null ? Quaternion.Inverse(parent.rotation) * rotation : rotation;

        private async UniTaskVoid Glide(Vector3 position, Quaternion rotation, float duration, bool dropAtEnd,
            CancellationTokenSource cts)
        {
            var token = cts.Token;
            var fromPosition = transform.localPosition;
            var fromRotation = transform.localRotation;
            var arrived = false;

            try
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    transform.SetLocalPositionAndRotation(
                        Vector3.Lerp(fromPosition, position, k),
                        Quaternion.Slerp(fromRotation, rotation, k));

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                transform.SetLocalPositionAndRotation(position, rotation);
                arrived = true;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_moveCts, cts)) _moveCts = null;
                cts.Dispose();
            }

            // В физику отпускается только доехавший кусок: отменённую доводку прервал
            // новый хозяин трансформа.
            if (arrived && dropAtEnd) Drop();
        }
    }
}