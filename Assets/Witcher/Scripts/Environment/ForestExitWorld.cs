using UnityEngine;

namespace Witcher.Environment
{
    /// <summary>
    /// Marks the 3D forest-exit set piece built by the editor menu.
    /// </summary>
    public sealed class ForestExitWorld : MonoBehaviour
    {
        public Transform vista;
        public bool vistaFollowY = true;

        Camera _cam;

        void LateUpdate()
        {
            if (!vistaFollowY || vista == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            var p = vista.position;
            p.y = _cam.transform.position.y;
            vista.position = p;
        }
    }
}
