using PlanetSurvival.World.Presentation;
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

        [SerializeField, Min(0f), Tooltip("Extra metres an orthographic camera is pulled back along its view. The picture does not change, but mountain meshes, built tall so walls read at sprite scale, no longer reach past the near clip plane.")]
        private float _orthographicClearance = 60f;

        private Transform _target;
        private Camera _camera;
        private IGroundSurface _surface;

        /// <summary>
        /// The drawn ground. In a crater the target is drawn below its gameplay position, so the camera
        /// follows it down to keep it framed. Null is flat ground.
        /// </summary>
        public void SetGroundSurface(IGroundSurface surface)
        {
            _surface = surface;
        }

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

            transform.position = DesiredPosition();
        }

        private void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            transform.rotation = Quaternion.Euler(_downwardPitch, 0f, 0f);
            transform.position = DesiredPosition();
        }

        private Vector3 DesiredPosition()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            Vector3 target = _target.position;
            if (_surface != null)
            {
                target.y += _surface.HeightAt(target.x, target.z);
            }

            Vector3 position = target + _offset;
            // Moving an orthographic camera along its own view leaves the image unchanged; a perspective
            // camera would zoom out, so it keeps the authored offset.
            return _camera != null && _camera.orthographic
                ? position - transform.forward * _orthographicClearance
                : position;
        }
    }
}
