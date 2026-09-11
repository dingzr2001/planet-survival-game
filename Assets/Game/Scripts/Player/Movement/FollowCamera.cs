using UnityEngine;

namespace PlanetSurvival.Player.Movement
{
    [DisallowMultipleComponent]
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField, Tooltip("Positions the camera above and behind the player for a three-quarter overhead view.")]
        private Vector3 _offset = new(0f, 15f, -8.66f);

        [SerializeField, Range(45f, 90f), Tooltip("Fixed downward pitch. Keeping this independent of position makes the orthographic composition predictable.")]
        private float _downwardPitch = 55f;

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
            transform.rotation = Quaternion.Euler(_downwardPitch, 0f, 0f);
        }
    }
}
