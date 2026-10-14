using UnityEngine;

namespace Witcher.Player
{
    /// <summary>
    /// Keeps a weapon model glued to the camera and adds a soft idle sway.
    /// Optional point light makes a "silver/magic blade" glow like the mood refs.
    /// </summary>
    public sealed class HeldWeapon : MonoBehaviour
    {
        [Header("Pose (local to camera)")]
        public Vector3 localPosition = new Vector3(0.28f, -0.32f, 0.55f);
        public Vector3 localEuler = new Vector3(5f, -10f, -8f);

        [Header("Idle sway")]
        public float swayAmount = 0.012f;
        public float swaySpeed = 1.6f;

        [Header("Glow")]
        public Light bladeGlow;
        public Color glowColor = new Color(0.35f, 0.95f, 1f, 1f);
        public float glowIntensity = 1.8f;
        public float glowRange = 3.5f;

        Transform _camera;

        void Awake()
        {
            var cam = GetComponentInParent<Camera>();
            _camera = cam != null ? cam.transform : transform.parent;

            if (bladeGlow != null)
            {
                bladeGlow.color = glowColor;
                bladeGlow.intensity = glowIntensity;
                bladeGlow.range = glowRange;
                bladeGlow.shadows = LightShadows.None;
            }
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
