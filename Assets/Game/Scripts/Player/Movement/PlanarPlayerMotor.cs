using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Player.Movement
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlanarPlayerMotor : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _moveSpeed = 5f;

        private CharacterController _controller;
        private Camera _camera;
        private WorldSpriteView _view;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _view = GetComponentInChildren<WorldSpriteView>();
        }

        private void Update()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            Vector2 input = Vector2.ClampMagnitude(
                new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
            Vector3 forward = _camera != null ? _camera.transform.forward : Vector3.forward;
            Vector3 right = _camera != null ? _camera.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 movement = forward * input.y + right * input.x;
            _controller.Move(movement * (_moveSpeed * Time.deltaTime));

            if (_view != null)
            {
                _view.SetMovement(input);
            }
        }
    }
}
