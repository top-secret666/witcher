using UnityEngine;
using UnityEngine.InputSystem;

namespace Witcher.Player
{
    /// <summary>
    /// Minimal FPS mover for exploring the dark-fantasy scene.
    /// Uses the new Input System (WASD + mouse). Esc unlocks the cursor.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Move")]
        public float walkSpeed = 4.5f;
        public float sprintSpeed = 7.5f;
        public float gravity = -18f;
        public float jumpHeight = 1.2f;

        [Header("Look")]
        public Transform cameraPivot;
        public float mouseSensitivity = 0.12f;
        public float minPitch = -80f;
        public float maxPitch = 80f;

        CharacterController _controller;
        float _pitch;
        float _verticalVelocity;
        bool _cursorLocked = true;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null)
                    cameraPivot = cam.transform;
            }
        }

        void Start() => SetCursorLock(true);

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLock(!_cursorLocked);

            Look();
            Move();
        }

        void SetCursorLock(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Look()
        {
            if (!_cursorLocked || Mouse.current == null || cameraPivot == null)
                return;

            Vector2 delta = Mouse.current.delta.ReadValue();
            transform.Rotate(0f, delta.x * mouseSensitivity, 0f);

            _pitch -= delta.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void Move()
        {
            if (Keyboard.current == null)
                return;

            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            bool sprint = Keyboard.current.leftShiftKey.isPressed;
            float speed = sprint ? sprintSpeed : walkSpeed;

            Vector3 wish = transform.right * input.x + transform.forward * input.y;
            wish *= speed;

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            if (_controller.isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            _verticalVelocity += gravity * Time.deltaTime;
            wish.y = _verticalVelocity;

            _controller.Move(wish * Time.deltaTime);
        }
    }
}
