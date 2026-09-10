using UnityEngine;

namespace PlanetSurvival.Player.Movement
{
    [DisallowMultipleComponent]
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField, Tooltip("A distant fixed perspective aligned to the world Z axis. The longer lens reduces distortion while preserving a visible band of sky.")]
        private Vector3 _offset = new(0f, 11f, -20f);

        [SerializeField, Min(0f), Tooltip("Raises the framing so roughly the upper quarter of a 16:9 view can show the sky and distant landmarks.")]
        private float _lookAtHeight = 5.5f;

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
