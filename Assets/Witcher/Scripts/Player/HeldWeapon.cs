using UnityEngine;

namespace Witcher.Player
{
    /// <summary>
    /// Viewmodel sword glued to the camera + cyan blade glow (reference style).
    /// </summary>
    public sealed class HeldWeapon : MonoBehaviour
    {
        [Header("Pose (local to camera)")]
        public Vector3 localPosition = new Vector3(0.35f, -0.28f, 0.55f);
        public Vector3 localEuler = new Vector3(8f, 95f, -10f);

        [Header("Idle sway")]
        public float swayAmount = 0.01f;
        public float swaySpeed = 1.4f;

        [Header("Glow")]
        public Light bladeGlow;
        public Color glowColor = new Color(0.35f, 0.95f, 1f, 1f);
        [Range(0f, 12f)] public float glowIntensity = 4.5f;
        [Range(1f, 20f)] public float glowRange = 8f;

        Transform _camera;

        void Awake()
        {
            var cam = GetComponentInParent<Camera>();
            _camera = cam != null ? cam.transform : transform.parent;
            ApplyGlow();
        }

        void OnValidate() => ApplyGlow();

        void ApplyGlow()
        {
            if (bladeGlow == null)
                return;

            bladeGlow.color = glowColor;
            bladeGlow.intensity = glowIntensity;
            bladeGlow.range = glowRange;
            bladeGlow.shadows = LightShadows.None;
        }

        void LateUpdate()
        {
            if (_camera == null)
                return;

            float t = Time.time * swaySpeed;
            Vector3 sway = new Vector3(
                Mathf.Sin(t * 0.7f) * swayAmount,
                Mathf.Cos(t) * swayAmount * 0.6f,
                0f);

            transform.position = _camera.TransformPoint(localPosition + sway);
            transform.rotation = _camera.rotation * Quaternion.Euler(localEuler);
        }
    }
}
