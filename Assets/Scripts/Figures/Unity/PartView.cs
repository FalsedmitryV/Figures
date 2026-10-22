using System;
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
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartView : MonoBehaviour
    {
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Rigidbody _body;
        private Color _baseTint = Color.white;
        private CancellationTokenSource _moveCts;

        public string PartId { get; private set; }

        public PartSlotKind Kind { get; private set; }

        public string OwnerFigureId { get; private set; }

        /// <summary>Поза на подносе: куда деталь возвращается, если её отпустили мимо гнезда.</summary>
        public Vector3 HomePosition { get; private set; }

        public Quaternion HomeRotation { get; private set; }

        public void Bind(PartSpec spec, Color tint)
        {
            PartId = spec.Id;
            Kind = spec.Kind;
            OwnerFigureId = spec.OwnerFigureId;
            _baseTint = tint;

            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            _body = EnsureBody();

            GreyBoxBuilder.Tint(gameObject, tint);
            MarkHome();
        }

        /// <summary>
        /// Деталь в руке: телом правит трансформ, а не физика. Кинематическим тело
        /// остаётся и пока деталь доводится до гнезда.
        /// </summary>
        public void Hold()
        {
            StopMove();
            if (_body == null) return;

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.isKinematic = true;
        }

        /// <summary>Отпустить в воздух: дальше деталь падает сама, без начального толчка.</summary>
        public void Drop()
        {
            StopMove();
            if (_body == null) return;

            _body.isKinematic = false;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.WakeUp();
        }

        private Rigidbody EnsureBody()
        {
            // Через TryGetComponent, а не GetComponent ?? AddComponent: отсутствующий
            // компонент приходит из движка не настоящим null, `??` его не ловит, и
            // первое же обращение валит спавн целиком на первой же детали.
            if (!TryGetComponent(out Rigidbody body)) body = gameObject.AddComponent<Rigidbody>();

            // Пока деталь лежит на подносе, физика ей не нужна: раскладка сетки
            // детерминирована, и просыпаться двум десяткам тел незачем.
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearDamping = 0.05f;

            // Шар головы и капсулы рук катались бы по столешнице до края: гашение
            // вращения останавливает их там, куда их положили.
            body.angularDamping = 4f;

            return body;
        }

        /// <summary>Запомнить текущую позу как место на подносе.</summary>
        public void MarkHome()
        {
            HomePosition = transform.position;
            HomeRotation = transform.rotation;
        }

        public void SetHighlighted(bool on, float boost)
        {
            if (_renderers == null) return;

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
            StopMove();

            if (duration <= 0f)
            {
                transform.SetPositionAndRotation(position, rotation);
                return;
            }

            _moveCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            Glide(position, rotation, duration, _moveCts).Forget();
        }

        public void StopMove() => _moveCts?.Cancel();

        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            StopMove();
            transform.SetPositionAndRotation(position, rotation);
        }

        private void OnDestroy() => StopMove();

        private async UniTaskVoid Glide(Vector3 position, Quaternion rotation, float duration,
            CancellationTokenSource cts)
        {
            var token = cts.Token;
            var fromPosition = transform.position;
            var fromRotation = transform.rotation;

            try
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    transform.SetPositionAndRotation(
                        Vector3.Lerp(fromPosition, position, k),
                        Quaternion.Slerp(fromRotation, rotation, k));

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                transform.SetPositionAndRotation(position, rotation);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_moveCts, cts)) _moveCts = null;
                cts.Dispose();
            }
        }
    }
}
