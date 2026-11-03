using UnityEngine;

namespace Witcher.Visuals
{
    /// <summary>
    /// Wind sway applied after billboard/parallax (runs late in LateUpdate).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class WindSway : MonoBehaviour
    {
        public float angleDegrees = 2.8f;
        public float speed = 1.1f;
        public float secondary = 0.45f;
        public float phaseOffset;

        void OnEnable()
        {
            if (Mathf.Approximately(phaseOffset, 0f))
                phaseOffset = Random.Range(0f, 100f);
        }

        void LateUpdate()
        {
            float t = Time.time * speed + phaseOffset;
            float yaw = Mathf.Sin(t) * angleDegrees;
            float roll = Mathf.Sin(t * 1.37f + 0.6f) * angleDegrees * secondary;
            // Multiply onto current facing (set by BillboardY / ParallaxLayer)
            transform.rotation = transform.rotation * Quaternion.Euler(0f, yaw, roll);
        }
    }
}
