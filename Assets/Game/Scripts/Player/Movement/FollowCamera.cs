using UnityEngine;

namespace PlanetSurvival.Player.Movement
{
    [DisallowMultipleComponent]
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField, Tooltip("Positions the camera mostly above the player. The small rear offset keeps the player below centre while the steep view preserves square ground tiles.")]
        private Vector3 _offset = new(0f, 17f, -3f);

        [SerializeField, Range(60f, 90f), Tooltip("Fixed downward pitch. A steep angle keeps square ground artwork nearly square on screen.")]
        private float _downwardPitch = 75f;

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
