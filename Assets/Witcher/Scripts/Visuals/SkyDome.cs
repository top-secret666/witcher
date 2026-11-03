using UnityEngine;

namespace Witcher.Visuals
{
    /// <summary>
    /// Large textured sky sphere that follows the camera (for painted day skies).
    /// </summary>
    [ExecuteAlways]
    public sealed class SkyDome : MonoBehaviour
    {
        public Transform follow;
        public float radius = 180f;

        void LateUpdate()
        {
            if (follow == null && Camera.main != null)
                follow = Camera.main.transform;
            if (follow == null)
                return;

            transform.position = follow.position;
            transform.localScale = Vector3.one * radius;
        }
    }
}
