using UnityEngine;

namespace Witcher.Visuals
{
    /// <summary>
    /// Paper-craft backdrop: follows camera X with parallax, stays at eye height.
    /// </summary>
    [ExecuteAlways]
    public sealed class ParallaxLayer : MonoBehaviour
    {
        public Transform follow;
        [Range(0f, 1f)] public float parallaxX = 0.15f;
        [Tooltip("World Y = camera.y + heightOffset (0 = centered on eyes).")]
        public float heightOffset;
        public bool faceCameraY = true;

        Vector3 _origin;
        bool _originSet;

        void OnEnable()
        {
            CaptureOrigin();
        }

        void LateUpdate()
        {
            if (follow == null && Camera.main != null)
                follow = Camera.main.transform;
            if (follow == null)
                return;

            if (!_originSet)
                CaptureOrigin();

            float x = _origin.x + (follow.position.x - _origin.x) * parallaxX;
            float y = follow.position.y + heightOffset;
            transform.position = new Vector3(x, y, _origin.z);

            if (faceCameraY)
            {
                Vector3 toCam = follow.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
            }
        }

        public void CaptureOrigin()
        {
            _origin = transform.position;
            _originSet = true;
        }

        // Back-compat for older builders
        public float lockY
        {
            get => heightOffset;
            set => heightOffset = value;
        }
    }
}
