using UnityEngine;

namespace Witcher.Visuals
{
    /// <summary>
    /// Simple emissive moon in the sky (billboard sphere). Attach under Lighting.
    /// </summary>
    [ExecuteAlways]
    public sealed class MoonDisc : MonoBehaviour
    {
        public Transform followCamera;
        public Vector3 localOffsetFromCamera = new Vector3(-40f, 35f, 80f);
        public float scale = 8f;

        void LateUpdate()
        {
            if (followCamera == null)
                followCamera = Camera.main != null ? Camera.main.transform : null;
            if (followCamera == null)
                return;

            transform.position = followCamera.position + followCamera.rotation * localOffsetFromCamera;
            transform.localScale = Vector3.one * scale;
        }
    }
}
