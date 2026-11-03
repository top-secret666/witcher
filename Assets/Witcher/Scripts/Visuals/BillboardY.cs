using UnityEngine;

namespace Witcher.Visuals
{
    /// <summary>
    /// Classic FPS billboard: rotate only around Y so the sprite faces the camera.
    /// </summary>
    [ExecuteAlways]
    public sealed class BillboardY : MonoBehaviour
    {
        public Camera targetCamera;
        public bool flattenX = true;

        void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera == null)
                return;

            Vector3 toCam = targetCamera.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
            if (flattenX)
            {
                var e = transform.eulerAngles;
                e.x = 0f;
                e.z = 0f;
                transform.eulerAngles = e;
            }
        }
    }
}
