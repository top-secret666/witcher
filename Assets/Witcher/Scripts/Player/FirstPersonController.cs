using UnityEngine;
using UnityEngine.InputSystem;

namespace Witcher.Player
{
    /// <summary>
    /// Minimal FPS mover. Snaps to ground on start so you don't fall through bad floors.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Move")]
        public float walkSpeed = 4.5f;
        public float sprintSpeed = 7.5f;
        public float gravity = -20f;
        public float jumpHeight = 1.2f;
        [Tooltip("Pitch mode: only W/S, no A/D strafe.")]
        public bool allowStrafe = false;
        [Tooltip("Pitch mode: block turning left/right with mouse.")]
        public bool allowTurn = false;

        [Header("Look")]
        public Transform cameraPivot;
        public float mouseSensitivity = 0.12f;
        public float minPitch = -80f;
        public float maxPitch = 80f;

        [Header("Ground")]
        [Tooltip("How far above the hit floor to place the feet.")]
        public float groundSnapPadding = 0.05f;
        public float groundProbeHeight = 50f;

        CharacterController _controller;
        float _pitch;
        float _verticalVelocity;
        bool _cursorLocked = true;
        Vector3 _planarVelocity;
        bool _sprinting;

        /// <summary>Horizontal move speed (m/s) — used by viewmodel bob.</summary>
        public float PlanarSpeed => _planarVelocity.magnitude;

        public bool IsSprinting => _sprinting && PlanarSpeed > 0.05f;

        public bool IsMoving => PlanarSpeed > 0.05f;

        void Awake()
        {
            EnsureController();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null)
                    cameraPivot = cam.transform.parent != null ? cam.transform.parent : cam.transform;
            }
        }

        void Start()
        {
            SnapToGround();
            SetCursorLock(true);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLock(!_cursorLocked);

            Look();
            Move();
        }

        void EnsureController()
        {
            if (_controller == null)
                _controller = GetComponent<CharacterController>();
            if (_controller == null)
                return;

            _controller.skinWidth = 0.08f;
            _controller.minMoveDistance = 0f;
            _controller.stepOffset = 0.35f;
        }

        [ContextMenu("Snap To Ground Now")]
        public void SnapToGround()
        {
            EnsureController();
            if (_controller == null)
                return;

            Vector3 origin = transform.position + Vector3.up * groundProbeHeight;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundProbeHeight * 2f, ~0, QueryTriggerInteraction.Ignore))
                return;

            // CharacterController bottom is at transform.position when center.y == height/2
            float bottomOffset = _controller.center.y - _controller.height * 0.5f + _controller.skinWidth;
            Vector3 p = transform.position;
            p.y = hit.point.y - bottomOffset + groundSnapPadding;
            transform.position = p;
            _verticalVelocity = -2f;
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
            if (allowTurn)
                transform.Rotate(0f, delta.x * mouseSensitivity, 0f);

            _pitch -= delta.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void Move()
        {
            EnsureController();
            if (_controller == null || Keyboard.current == null)
                return;

            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (allowStrafe)
            {
                if (Keyboard.current.dKey.isPressed) input.x += 1f;
                if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            }
            input = Vector2.ClampMagnitude(input, 1f);

            bool sprint = Keyboard.current.leftShiftKey.isPressed;
            float speed = sprint ? sprintSpeed : walkSpeed;
            _sprinting = sprint && input.sqrMagnitude > 0.01f;

            Vector3 wish = transform.right * input.x + transform.forward * input.y;
            wish *= speed;
            _planarVelocity = new Vector3(wish.x, 0f, wish.z);

            bool grounded = _controller.isGrounded;
            if (grounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            if (grounded && Keyboard.current.spaceKey.wasPressedThisFrame)
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            _verticalVelocity += gravity * Time.deltaTime;
            wish.y = _verticalVelocity;

            _controller.Move(wish * Time.deltaTime);
        }
    }
}
