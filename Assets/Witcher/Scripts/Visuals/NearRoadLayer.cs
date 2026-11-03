using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Witcher.Player;

namespace Witcher.Visuals
{
    /// <summary>
    /// L3 "walking" layer that sits between the parting side pines (L2) and the sword.
    /// Draws a perspective dirt road under the viewer that scrolls toward the camera while
    /// the player walks, plus a ring of dark near pines along the road that emerge small near
    /// the vanishing point, grow, slide outward/down past the screen edge and recycle.
    /// Everything is CPU-projected (no custom shader) into UI RawImages, so it works on any
    /// Canvas and keeps the point-filtered pixel-art look.
    /// Built automatically by <see cref="PitchPaperRig"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NearRoadLayer : MonoBehaviour
    {
        [Header("Walk link")]
        public FirstPersonController mover;
        [Tooltip("Road metres travelled per metre/second of player walk speed (lower = slower scroll).")]
        public float travelPerMeter = 0.55f;
        [Tooltip("How quickly the walk motion starts / stops (higher = snappier).")]
        public float acceleration = 3.5f;

        [Header("Road perspective")]
        [Tooltip("Height of the road's vanishing line, as a fraction of the screen height from the bottom.")]
        [Range(0.05f, 0.7f)] public float horizon = 0.16f;
        public float cameraHeight = 0.8f;
        public float focal = 1f;
        [Tooltip("World width covered by the road texture (dirt + grassy verges), metres.")]
        public float roadWorldWidth = 4.2f;
        [Tooltip("World length of one vertical repeat of the road texture, metres.")]
        public float tileLength = 13.3f;
        [Tooltip("Road centre at the bottom of the screen (0..1 of width).")]
        [Range(0f, 1f)] public float nearCenterX = 0.5f;
        [Tooltip("Road centre at the far end, lined up with the painted path in the vista.")]
        [Range(0f, 1f)] public float farCenterX = 0.545f;
        [Tooltip("Road starts fading out at this distance (m)...")]
        public float fadeStart = 14f;
        [Tooltip("...and is fully faded at this distance (m), so it merges into the painted path.")]
        public float fadeEnd = 26f;
        [Tooltip("Horizontal resolution of the generated road image (pixel-art chunkiness).")]
        [Range(120, 960)] public int roadPixelWidth = 400;

        [Header("Near pines")]
        public int pineCount = 8;
        [Tooltip("Depth range the pines cycle through (m). Larger = sparser.")]
        public float span = 18f;
        [Tooltip("Pines recycle to the far end once closer than this (m).")]
        public float nearClip = 0.8f;
        [Tooltip("Distance over which a new pine fades in at the far end (m).")]
        public float emergeLength = 5f;
        [Tooltip("Gap between the road edge and the pines (m).")]
        public float sideMin = 2.6f;
        public float sideMax = 4.6f;
        public float heightMin = 2.4f;
        public float heightMax = 3.4f;
        [Tooltip("Colour multiplier for the near pines (lower = darker).")]
        [Range(0f, 1f)] public float pineTint = 0.78f;
        public int seed = 5;

        const string RoadPath = "Pitch/T_L4_Road";
        static readonly string[] PinePaths =
        {
            "Pitch/T_L4_NearPine_A", "Pitch/T_L4_NearPine_B", "Pitch/T_L4_NearPine_C", "Pitch/T_L4_NearPine_D"
        };

        sealed class Pine
        {
            public RectTransform rt;
            public RawImage img;
            public Texture2D tex;
            public float x;       // lateral world offset from road centre (m), signed
            public float height;  // world height (m)
            public float phase;   // 0..span offset along the loop
            public float lastZ;
        }

        RectTransform _rect;
        RawImage _roadImg;
        Texture2D _roadTex;
        Color32[] _src;
        int _srcW, _srcH;
        Color32[] _buf;
        int[] _uIdx;
        float[] _rowV;
        byte[] _rowFade;
        int _rtW, _rtH;
        Vector2 _builtSize;
        float _builtHorizon = -1f;

        readonly List<Pine> _pines = new List<Pine>();
        readonly List<Pine> _sorted = new List<Pine>();
        Texture2D[] _pineTex;
        System.Random _rng;

        float _speed;
        float _roadPhase;  // wraps at tileLength
        float _treePhase;  // wraps at span
        bool _dirty = true;

        /// <summary>Current smoothed walk speed driving the layer (m/s).</summary>
        public float CurrentSpeed => _speed;

        public void Build()
        {
            _rect = GetComponent<RectTransform>();
            _rng = new System.Random(seed);

            var road = Resources.Load<Texture2D>(RoadPath);
            if (road == null)
            {
                Debug.LogWarning("NearRoadLayer: missing Resources/" + RoadPath);
            }
            else
            {
                _src = ReadPixelsSafe(road, out _srcW, out _srcH);
            }

            var roadGo = new GameObject("Road", typeof(RectTransform));
            roadGo.transform.SetParent(transform, false);
            var rrt = roadGo.GetComponent<RectTransform>();
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;
            _roadImg = roadGo.AddComponent<RawImage>();
            _roadImg.raycastTarget = false;
            _roadImg.color = Color.white;
            _roadImg.enabled = _src != null;

            _pineTex = new Texture2D[PinePaths.Length];
            int found = 0;
            for (int i = 0; i < PinePaths.Length; i++)
            {
                _pineTex[i] = Resources.Load<Texture2D>(PinePaths[i]);
                if (_pineTex[i] != null)
                {
                    _pineTex[i].filterMode = FilterMode.Point;
                    found++;
                }
            }

            if (found > 0)
            {
                int n = Mathf.Max(0, pineCount);
                for (int i = 0; i < n; i++)
                {
                    var p = new Pine();
                    var go = new GameObject("NearPine_" + i, typeof(RectTransform));
                    go.transform.SetParent(transform, false);
                    p.rt = go.GetComponent<RectTransform>();
                    p.rt.anchorMin = Vector2.zero;
                    p.rt.anchorMax = Vector2.zero;
                    p.rt.pivot = new Vector2(0.5f, 0f);
                    p.img = go.AddComponent<RawImage>();
                    p.img.raycastTarget = false;
                    float side = (i % 2 == 0) ? -1f : 1f;
                    p.phase = (i + (float)_rng.NextDouble() * 0.8f) * span / n;
                    Randomize(p, side, i);
                    p.lastZ = float.MaxValue;
                    _pines.Add(p);
                }
            }
            else
            {
                Debug.LogWarning("NearRoadLayer: no Pitch/T_L4_NearPine_* textures found");
            }

            _dirty = true;
        }

        void Randomize(Pine p, float side, int hint)
        {
            p.x = side * (roadWorldWidth * 0.5f + Mathf.Lerp(sideMin, sideMax, (float)_rng.NextDouble()));
            p.height = Mathf.Lerp(heightMin, heightMax, (float)_rng.NextDouble());
            Texture2D t = null;
            for (int k = 0; k < _pineTex.Length && t == null; k++)
                t = _pineTex[(hint + _rng.Next(_pineTex.Length) + k) % _pineTex.Length];
            p.tex = t;
            p.img.texture = t;
        }

        void OnDestroy()
        {
            if (_roadTex != null)
            {
                if (Application.isPlaying) Destroy(_roadTex);
                else DestroyImmediate(_roadTex);
            }
        }

        void LateUpdate()
        {
            if (_rect == null) return;
            Vector2 size = _rect.rect.size;
            if (size.x < 2f || size.y < 2f) return;

            // --- walk signal (same source as the parting pines) ---
            float target = 0f;
            if (mover != null && mover.IsMoving)
                target = mover.PlanarSpeed * travelPerMeter;
            float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, acceleration) * Time.deltaTime);
            _speed = Mathf.Lerp(_speed, target, k);
            if (target <= 0f && _speed < 0.002f) _speed = 0f;

            float step = _speed * Time.deltaTime;
            if (step > 0f)
            {
                _roadPhase = Mathf.Repeat(_roadPhase + step, Mathf.Max(0.01f, tileLength));
                _treePhase = Mathf.Repeat(_treePhase + step, Mathf.Max(0.01f, span));
                _dirty = true;
            }

            if (size != _builtSize || !Mathf.Approximately(_builtHorizon, horizon) || _uIdx == null)
                RebuildTables(size);

            if (_dirty)
            {
                DrawRoad();
                _dirty = false;
            }

            UpdatePines(size);
        }

        float CenterX(float yFrac)
        {
            float t = Mathf.Clamp01(yFrac / Mathf.Max(0.0001f, horizon));
            t = t * Mathf.Sqrt(t); // t^1.5 - bends toward the painted path only near the far end
            return Mathf.Lerp(nearCenterX, farCenterX, t);
        }

        void RebuildTables(Vector2 size)
        {
            _builtSize = size;
            _builtHorizon = horizon;
            float aspect = size.x / size.y;

            var rrt = _roadImg.rectTransform;
            rrt.anchorMin = new Vector2(0f, 0f);
            rrt.anchorMax = new Vector2(1f, horizon);
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;

            if (_src == null) return;

            _rtW = Mathf.Clamp(roadPixelWidth, 32, 2048);
            _rtH = Mathf.Max(4, Mathf.RoundToInt(horizon * _rtW / aspect));

            if (_roadTex == null || _roadTex.width != _rtW || _roadTex.height != _rtH)
            {
                if (_roadTex != null) Destroy(_roadTex);
                _roadTex = new Texture2D(_rtW, _rtH, TextureFormat.RGBA32, false);
                _roadTex.name = "NearRoad_Runtime";
                _roadTex.filterMode = FilterMode.Point;
                _roadTex.wrapMode = TextureWrapMode.Clamp;
                _roadImg.texture = _roadTex;
            }

            int n = _rtW * _rtH;
            _buf = new Color32[n];
            _uIdx = new int[n];
            _rowV = new float[_rtH];
            _rowFade = new byte[_rtH];

            float texPerMeter = _srcH / Mathf.Max(0.01f, tileLength);
            for (int r = 0; r < _rtH; r++)
            {
                float y = (r + 0.5f) / _rtH * horizon;        // fraction of screen height from bottom
                float sy = Mathf.Max(0.0005f, horizon - y);   // distance below the vanishing line
                float z = cameraHeight * focal / sy;          // world depth of this row
                _rowV[r] = z * texPerMeter;
                float fade = Mathf.Clamp01((fadeEnd - z) / Mathf.Max(0.01f, fadeEnd - fadeStart));
                _rowFade[r] = (byte)Mathf.RoundToInt(fade * 255f);

                float cx = CenterX(y);
                for (int c = 0; c < _rtW; c++)
                {
                    float sx = (c + 0.5f) / _rtW;
                    float xw = (sx - cx) * aspect * z / focal;
                    float u = xw / Mathf.Max(0.01f, roadWorldWidth) + 0.5f;
                    _uIdx[r * _rtW + c] = (u >= 0f && u < 1f) ? Mathf.Min(_srcW - 1, (int)(u * _srcW)) : -1;
                }
            }
            _dirty = true;
        }

        void DrawRoad()
        {
            if (_src == null || _roadTex == null || _uIdx == null) return;
            float scroll = _roadPhase / Mathf.Max(0.01f, tileLength) * _srcH;
            var clear = new Color32(0, 0, 0, 0);
            for (int r = 0; r < _rtH; r++)
            {
                int row = r * _rtW;
                byte fa = _rowFade[r];
                if (fa == 0)
                {
                    for (int c = 0; c < _rtW; c++) _buf[row + c] = clear;
                    continue;
                }
                int v = (int)Mathf.Floor(_rowV[r] + scroll) % _srcH;
                if (v < 0) v += _srcH;
                int vOff = v * _srcW;
                for (int c = 0; c < _rtW; c++)
                {
                    int ui = _uIdx[row + c];
                    if (ui < 0) { _buf[row + c] = clear; continue; }
                    Color32 s = _src[vOff + ui];
                    if (fa != 255) s.a = (byte)(s.a * fa / 255);
                    _buf[row + c] = s;
                }
            }
            _roadTex.SetPixels32(_buf);
            _roadTex.Apply(false);
        }

        void UpdatePines(Vector2 size)
        {
            if (_pines.Count == 0) return;
            float aspect = size.x / size.y;
            float spanSafe = Mathf.Max(1f, span);

            _sorted.Clear();
            for (int i = 0; i < _pines.Count; i++)
            {
                var p = _pines[i];
                float z = nearClip + Mathf.Repeat(p.phase - _treePhase, spanSafe);
                if (z > p.lastZ + spanSafe * 0.5f)
                    Randomize(p, Mathf.Sign(p.x), i); // just recycled to the far end - new look
                p.lastZ = z;

                float yb = horizon - cameraHeight * focal / z;           // base height (fraction from bottom)
                float hFrac = p.height * focal / z;                      // tree height in screen heights
                float alpha = Mathf.Clamp01((nearClip + spanSafe - z) / Mathf.Max(0.01f, emergeLength));
                float hPx = hFrac * size.y;
                float wPx = p.tex != null ? hPx * p.tex.width / (float)p.tex.height : hPx * 0.4f;
                float xPx = (CenterX(Mathf.Max(0f, yb)) + (p.x * focal / z) / aspect) * size.x;

                bool visible = p.tex != null && alpha > 0.003f && hPx > 1.5f
                               && xPx + wPx * 0.5f > 0f && xPx - wPx * 0.5f < size.x
                               && yb * size.y < size.y;
                p.img.enabled = visible;
                if (!visible) continue;

                p.rt.sizeDelta = new Vector2(wPx, hPx);
                p.rt.anchoredPosition = new Vector2(xPx, yb * size.y);
                p.img.color = new Color(pineTint, pineTint, pineTint, alpha);
                _sorted.Add(p);
            }

            // far first, near last; index 0 is the road
            _sorted.Sort((a, b) => b.lastZ.CompareTo(a.lastZ));
            for (int i = 0; i < _sorted.Count; i++)
                _sorted[i].rt.SetSiblingIndex(1 + i);
        }

        static Color32[] ReadPixelsSafe(Texture2D src, out int w, out int h)
        {
            w = src.width;
            h = src.height;
            if (src.isReadable)
                return src.GetPixels32();

            // Fallback for non-readable imports: copy through a temporary RenderTexture.
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tmp.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tmp.GetPixels32();
            if (Application.isPlaying) Destroy(tmp);
            else DestroyImmediate(tmp);
            return px;
        }
    }
}
