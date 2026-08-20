using UnityEngine;

namespace PlanetSurvival.Player.Movement
{
    [DisallowMultipleComponent]
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 _offset = new(8f, 10f, -8f);
        [SerializeField, Min(0f)] private float _smoothTime = 0.15f;

        private Transform _target;
        private Vector3 _velocity;

        public void SetTarget(Transform target)
        {
            _target = target;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Vector3 targetPosition = _target.position + _offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, _smoothTime);
            transform.LookAt(_target.position + Vector3.up);
        }

        private void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            transform.position = _target.position + _offset;
            transform.LookAt(_target.position + Vector3.up);
        }
    }
}
