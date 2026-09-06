using UnityEngine;

namespace PlanetSurvival.Player.Movement
{
    [DisallowMultipleComponent]
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField, Tooltip("A fixed elevated perspective that keeps a narrow band of horizon visible.")]
        private Vector3 _offset = new(5.5f, 8.5f, -8.5f);

        [SerializeField, Min(0f), Tooltip("Raises the framing so the upper portion of the screen can show the sky and distant landmarks.")]
        private float _lookAtHeight = 4.85f;

        private Transform _target;

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

            transform.position = _target.position + _offset;
        }

        private void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            transform.position = _target.position + _offset;
            transform.LookAt(_target.position + Vector3.up * _lookAtHeight);
        }
    }
}
