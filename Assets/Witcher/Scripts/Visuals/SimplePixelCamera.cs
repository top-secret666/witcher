using UnityEngine;
using UnityEngine.Rendering;

namespace Witcher.Visuals
{
    /// <summary>
    /// Fallback / extra crunch: renders the camera into a low-res RenderTexture
    /// and stretches it with Point filter. Works even without the URP feature.
    /// Attach to Main Camera. Disable if RetroLookFeature alone is enough.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SimplePixelCamera : MonoBehaviour
    {
        [Tooltip("Internal vertical resolution. 160–240 = strong retro.")]
        [Range(90, 540)] public int verticalResolution = 180;

        [Tooltip("Leave 0 to keep aspect from screen.")]
        public int horizontalResolution;

        Camera _camera;
        RenderTexture _rt;

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            Allocate();
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            if (_camera != null)
                _camera.targetTexture = null;
            Release();
        }

        void OnValidate()
        {
            if (isActiveAndEnabled)
                Allocate();
        }

        void Allocate()
        {
            Release();
            int h = Mathf.Max(64, verticalResolution);
            int w = horizontalResolution > 0
                ? horizontalResolution
                : Mathf.Max(64, Mathf.RoundToInt(h * ((float)Screen.width / Mathf.Max(Screen.height, 1))));

            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
                name = "Witcher_PixelRT"
            };
            _rt.Create();
        }

        void Release()
        {
            if (_rt == null)
                return;
            _rt.Release();
            if (Application.isPlaying)
                Destroy(_rt);
            else
                DestroyImmediate(_rt);
            _rt = null;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _camera || _rt == null)
                return;
            _camera.targetTexture = _rt;
        }

        void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _camera || _rt == null)
                return;

            _camera.targetTexture = null;
            Graphics.Blit(_rt, null as RenderTexture);
        }
    }
}
