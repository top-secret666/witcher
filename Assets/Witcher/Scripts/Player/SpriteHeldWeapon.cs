using UnityEngine;

namespace Witcher.Player
{
    /// <summary>
    /// Flat pixel sword locked to the camera (classic FPS viewmodel).
    /// Bob intensifies when the player walks / sprints.
    /// </summary>
    public sealed class SpriteHeldWeapon : MonoBehaviour
    {
        [Header("Pose (local to camera)")]
        public Vector3 localPosition = new Vector3(0.36f, -0.38f, 0.62f);
        public Vector3 localEuler = new Vector3(5f, -5f, -12f);
        public float localScale = 0.38f;

        [Header("Idle sway")]
        public float idleSwayAmount = 0.012f;
        public float idleSwaySpeed = 1.35f;

        [Header("Walk bob")]
        public float walkBobAmount = 0.055f;
        public float walkBobSpeed = 9.5f;
        public float sprintBobMultiplier = 1.35f;
        public float bobSmooth = 10f;

        [Header("Sprite")]
        public SpriteRenderer spriteRenderer;
        public Sprite weaponSprite;

        Transform _camera;
        FirstPersonController _mover;
        float _bobPhase;
        float _bobWeight;

        // Back-compat for older setup menus
        public float swayAmount
        {
            get => idleSwayAmount;
            set => idleSwayAmount = value;
        }

        public float swaySpeed
        {
            get => idleSwaySpeed;
            set => idleSwaySpeed = value;
        }

        void Awake()
        {
            var cam = GetComponentInParent<Camera>();
            _camera = cam != null ? cam.transform : transform.parent;
            _mover = GetComponentInParent<FirstPersonController>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (spriteRenderer == null)
            {
                var go = new GameObject("SwordSprite");
                go.transform.SetParent(transform, false);
                spriteRenderer = go.AddComponent<SpriteRenderer>();
            }

            if (weaponSprite != null)
                spriteRenderer.sprite = weaponSprite;

            spriteRenderer.sortingOrder = 100;
        }

        void LateUpdate()
        {
            if (_camera == null)
                return;

            float speed01 = 0f;
            bool sprinting = false;
            if (_mover != null)
            {
                speed01 = Mathf.Clamp01(_mover.PlanarSpeed / Mathf.Max(0.01f, _mover.walkSpeed));
                sprinting = _mover.IsSprinting;
            }

            float targetWeight = speed01;
            _bobWeight = Mathf.MoveTowards(_bobWeight, targetWeight, bobSmooth * Time.deltaTime);

            float bobRate = walkBobSpeed * (sprinting ? sprintBobMultiplier : 1f);
            if (_bobWeight > 0.05f)
                _bobPhase += Time.deltaTime * bobRate * Mathf.Lerp(0.55f, 1f, _bobWeight);
            else
                _bobPhase += Time.deltaTime * idleSwaySpeed;

            // Classic FPS figure-8: side + up when feet plant
            float walkX = Mathf.Sin(_bobPhase) * walkBobAmount * _bobWeight;
            float walkY = Mathf.Abs(Mathf.Cos(_bobPhase)) * walkBobAmount * 0.7f * _bobWeight;
            float idleX = Mathf.Sin(Time.time * idleSwaySpeed * 0.7f) * idleSwayAmount * (1f - _bobWeight);
            float idleY = Mathf.Cos(Time.time * idleSwaySpeed) * idleSwayAmount * 0.55f * (1f - _bobWeight);

            Vector3 sway = new Vector3(walkX + idleX, walkY + idleY, 0f);
            float roll = -walkX * 18f;

            transform.position = _camera.TransformPoint(localPosition + sway);
            transform.rotation = _camera.rotation * Quaternion.Euler(localEuler + new Vector3(0f, 0f, roll));
            transform.localScale = Vector3.one * localScale;
        }

        public void SetSprite(Sprite sprite)
        {
            weaponSprite = sprite;
            if (spriteRenderer != null)
                spriteRenderer.sprite = sprite;
        }
    }
}
