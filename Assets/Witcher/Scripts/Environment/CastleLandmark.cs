using UnityEngine;

namespace Witcher.Environment
{
    /// <summary>
    /// Marker on the castle root prefab. Lets systems find the landmark
    /// without searching by name strings.
    /// </summary>
    public sealed class CastleLandmark : MonoBehaviour
    {
        [Tooltip("World-facing approach point (stairs / path end).")]
        public Transform approachPoint;
    }
}
