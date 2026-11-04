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
        [Range(0.05f, 0.7f)] public float horizon = 0.22f;
        public float cameraHeight = 0.8f;
        public float focal = 1f;
        [Tooltip("World width covered by the road texture (dirt + grassy verges), metres.")]
        public float roadWorldWidth = 6.4f;
        [Tooltip("World length of one vertical repeat of the road texture, metres.")]
        public float tileLength = 18.4f;
        [Tooltip("Road centre at the bottom of the screen (0..1 of width).")]
        [Range(0f, 1f)] public float nearCenterX = 0.5f;
        [Tooltip("Road centre at the far end, lined up with the painted path in the vista.")]
        [Range(0f, 1f)] public float farCenterX = 0.545f;
        [Tooltip("Road starts fading out at this distance (m)...")]
        public float fadeStart = 16f;
        [Tooltip("...and is fully faded at this distance (m), so it merges into the painted path.")]
        public float fadeEnd = 30f;
        [Tooltip("Horizontal resolution of the generated road image (pixel-art chunkiness).")]
        [Range(120, 1920)] public int roadPixelWidth = 960;
        [Tooltip("Draw the road as a GPU perspective mesh (smooth sub-pixel scrolling, mipmaps + trilinear + anisotropic filtering; no shimmer). Off = old CPU-rendered image.")]
        public bool gpuRoad = true;
        [Tooltip("Horizontal strips of the GPU road mesh (more = more exact perspective).")]
        [Range(16, 400)] public int roadMeshRows = 160;
        [Tooltip("Anisotropic filtering level for the road texture (GPU road).")]
        [Range(0, 16)] public int roadAniso = 8;
        [Tooltip("Mip bias for the GPU road (negative = crisper detail while walking, 0 = softest).")]
        [Range(-1.5f, 1f)] public float roadMipBias = -0.5f;
        [Tooltip("Bilinear filtering of the generated road image (smoother, less pixelated). Off = hard pixels.")]
        public bool smoothRoad = true;

        [Header("Near pines (dense rows along both road edges)")]
        [Tooltip("Rows of pines on each side of the road (row 0 hugs the road edge).")]
        [Range(1, 6)] public int pineRows = 3;
        [Tooltip("Pines per row per side (total = rows x this x 2).")]
        [Range(2, 40)] public int pinesPerRow = 12;
        [Tooltip("Depth range the pines cycle through (m). Larger = sparser.")]
        public float span = 18f;
        [Tooltip("Pines recycle to the far end once closer than this (m).")]
        public float nearClip = 0.6f;
        [Tooltip("Distance over which a new pine fades in at the far end (m).")]
        public float emergeLength = 5f;
        [Tooltip("Extra gap between the road texture's dirt edge and row 0 (m). 0 = trees stand right at the edge.")]
        public float sideMin = 0f;
        [Tooltip("Where the dirt ends inside the road texture, as a fraction of its half width.")]
        [Range(0f, 1f)] public float dirtEdge = 0.42f;
        [Tooltip("Lateral distance between rows (m).")]
        public float rowSpacing = 0.9f;
        [Tooltip("Random lateral jitter inside a row (m).")]
        public float rowJitter = 0.45f;
        [Tooltip("Real eye height (m) that cameraHeight stands for. All plant heights below are in REAL metres and are converted with cameraHeight / eyeHeightMeters, so they read correctly against the player's eye line.")]
        public float eyeHeightMeters = 1.7f;
        [Tooltip("Smallest spruce/fir height (real m).")]
        public float heightMin = 4f;
        [Tooltip("Largest spruce/fir height (real m).")]
        public float heightMax = 12f;
        [Tooltip(">1 = more small trees with a few big ones, <1 = mostly big trees.")]
        [Range(0.3f, 3f)] public float sizeSkew = 1.0f;
        [Tooltip("Chance that a pine is an extra-tall 'giant'.")]
        [Range(0f, 1f)] public float giantChance = 0.08f;
        [Tooltip("Height multiplier for giant pines.")]
        public float giantScale = 1.1f;
        [Tooltip("Chance that a roadside tree is a dead spruce.")]
        [Range(0f, 1f)] public float deadChance = 0.08f;
        [Tooltip("Dead spruce height range (real m).")]
        public Vector2 deadHeight = new Vector2(5f, 10f);
        [Tooltip("Height range for WIDE sprites (wider than tall, e.g. the wide fir), real m.")]
        public Vector2 wideFirHeight = new Vector2(3f, 6f);
        [Tooltip("Bilinear filtering for the roadside sprites (smooth like the road). Off = hard pixels.")]
        public bool smoothSprites = true;
        [Tooltip("Soft cap on the on-screen height of the TALLEST pines (fraction of screen height); close trees run off the top of the screen.")]
        [Range(0.1f, 3f)] public float maxScreenHeight = 2.5f;
        [Tooltip("On-screen cap of the smallest pine, relative to maxScreenHeight.")]
        [Range(0.1f, 1f)] public float minCapFraction = 0.7f;
        [Tooltip("Height cap multiplier for pines in the middle of the screen (keeps the castle hill / painted path visible).")]
        [Range(0.1f, 1f)] public float centerCapFraction = 0.6f;
        [Tooltip("Within this distance from the screen centre (fraction of width) the centre cap applies fully...")]
        [Range(0f, 0.5f)] public float clearZoneInner = 0.12f;
        [Tooltip("...and beyond this distance pines get the full cap.")]
        [Range(0f, 0.5f)] public float clearZoneOuter = 0.4f;
        [Tooltip("Each row further out is this much smaller (fraction).")]
        [Range(0f, 0.5f)] public float rowShrink = 0.08f;
        [Tooltip("Colour multiplier for the near pines (lower = darker).")]
        [Range(0f, 1f)] public float pineTint = 0.4f;
        [Tooltip("Cool moonlight shade multiplied over the near trees and undergrowth (close = silhouette-like; blue keeps a faint moonlit rim).")]
        public Color nearShade = new Color(0.82f, 0.9f, 1.05f, 1f);
        [Tooltip("Each row further out is this much lighter.")]
        [Range(0f, 0.3f)] public float rowLighten = 0.08f;
        [Tooltip("Random brightness variation per tree.")]
        [Range(0f, 0.3f)] public float tintJitter = 0.06f;
        public int seed = 5;

        [Header("Hill-climb reveal (set by PitchPaperRig from distance walked)")]
        [Tooltip("0 = start of the walk, 1 = top of the hill (view fully open). Driven by PitchPaperRig.revealDistance.")]
        [Range(0f, 1f)] public float reveal01 = 0f;
        [Tooltip("Extra eye height at full reveal (x cameraHeight): road and tree bases drop lower on screen, like looking down from higher up.")]
        public float revealCameraRise = 0.3f;
        [Tooltip("Share of the eye rise applied to the ROAD (0 = the road keeps its full width/framing for the whole reveal; 1 = physically consistent, but the road shrinks into a thin strip far below).")]
        [Range(0f, 1f)] public float revealRoadRise = 0f;
        [Tooltip("Fraction of the roadside trees removed at full reveal (1 = every tree has faded out by the top of the hill).")]
        [Range(0f, 1f)] public float revealThin = 1f;
        [Tooltip("Fraction of the undergrowth removed at full reveal (0 = bushes, boulders, juniper, fern and grass all stay).")]
        [Range(0f, 1f)] public float revealUndergrowthThin = 1f;
        [Tooltip("Trees get this much shorter at full reveal (fraction).")]
        [Range(0f, 0.9f)] public float revealTreeShrink = 0.3f;

        [Header("Undergrowth (bushes, juniper, fern, grass, boulders on the verge)")]
        public bool buildUndergrowth = true;
        [Tooltip("Undergrowth pieces per side of the road (pooled).")]
        [Range(0, 80)] public int undergrowthPerSide = 32;
        [Tooltip("Inner edge of the undergrowth band as a fraction of the dirt-edge distance (1 = exactly where the dirt ends).")]
        [Range(0.3f, 1.5f)] public float undergrowthInner = 0.75f;
        [Tooltip("Outer edge of the band: metres (layer units) beyond the outermost pine row.")]
        public float undergrowthOuter = 0.6f;
        [Tooltip("Height ranges in REAL metres.")]
        public Vector2 bushHeight = new Vector2(0.6f, 1.2f);
        public Vector2 juniperHeight = new Vector2(0.8f, 1.5f);
        public Vector2 fernHeight = new Vector2(0.4f, 0.8f);
        public Vector2 grassHeight = new Vector2(0.4f, 0.9f);
        public Vector2 boulderHeight = new Vector2(0.5f, 1.2f);
        [Tooltip("Relative spawn weights: bush, juniper, fern, grass, boulder.")]
        public float[] undergrowthWeights = { 1f, 0.8f, 1.2f, 1.5f, 0.6f };
        [Tooltip("Colour multiplier for the undergrowth (lower = darker).")]
        [Range(0f, 1f)] public float undergrowthTint = 0.5f;

        const string RoadPath = "Pitch/T_L4_Road";
        static readonly string[] PinePaths =
        {
            "Pitch/T_L4_NearPine_A", "Pitch/T_L4_NearPine_B", "Pitch/T_L4_NearPine_C", "Pitch/T_L4_NearPine_D",
            "Pitch/T_L4_NearPine_E", "Pitch/T_L4_NearPine_F"
        };
        const string DeadPath = "Pitch/T_L4_NearPine_G";
        static readonly string[] UnderPaths =
        {
            "Pitch/T_L4_Bush", "Pitch/T_L4_Juniper", "Pitch/T_L4_Fern", "Pitch/T_L4_Grass", "Pitch/T_L4_Boulder"
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
            public int row;
            public float side;
            public float tint;
            public float sortKey;
            public float size01;  // 0 smallest .. 1 tallest (drives the per-tree screen cap)
            public bool dead;
            public bool under;    // undergrowth piece (no screen cap)
            public int kind;      // undergrowth type index
            public float thin;    // 0..1 random key: removed when reveal passes it
        }

        RectTransform _rect;
        RawImage _roadImg;
        NearRoadMesh _roadMesh;
        Texture2D _roadSrcTex;
        float _meshPhase = -1f, _meshCam = -1f;
        Vector2 _meshSize;
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
        float _builtCam = -1f;

        /// <summary>Eye height after the hill-climb rise.</summary>
        float CamEff => cameraHeight * (1f + Mathf.Max(0f, revealCameraRise) * Mathf.Clamp01(reveal01));

        /// <summary>Eye height used for the road. Kept at cameraHeight by default so the road never shrinks away.</summary>
        float RoadCam => cameraHeight * (1f + Mathf.Max(0f, revealCameraRise) * Mathf.Clamp01(revealRoadRise) * Mathf.Clamp01(reveal01));

        readonly List<Pine> _pines = new List<Pine>();
        readonly List<Pine> _sorted = new List<Pine>();
        Texture2D[] _pineTex;
        Texture2D _deadTex;
        Texture2D[] _underTex;
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
            _roadSrcTex = road;
            if (road == null)
            {
                Debug.LogWarning("NearRoadLayer: missing Resources/" + RoadPath);
            }
            else if (gpuRoad)
            {
                // GPU path: let the sampler do the work (mips from the importer, trilinear, aniso)
                road.wrapModeU = TextureWrapMode.Clamp;
                road.wrapModeV = TextureWrapMode.Repeat;
                road.filterMode = road.mipmapCount > 1 ? FilterMode.Trilinear : FilterMode.Bilinear;
                road.anisoLevel = roadAniso;
                road.mipMapBias = roadMipBias;
                Debug.Log("NearRoadLayer: GPU road active (" + road.width + "x" + road.height + ", mips " + road.mipmapCount
                          + ", filter " + road.filterMode + ", wrapV " + road.wrapModeV + ")");
            }
            else
            {
                _src = ReadPixelsSafe(road, out _srcW, out _srcH);
            }

            if (gpuRoad && road != null)
            {
                var meshGo = new GameObject("RoadMesh", typeof(RectTransform));
                meshGo.transform.SetParent(transform, false);
                var mrt = meshGo.GetComponent<RectTransform>();
                mrt.anchorMin = Vector2.zero;
                mrt.anchorMax = Vector2.one;
                mrt.offsetMin = Vector2.zero;
                mrt.offsetMax = Vector2.zero;
                _roadMesh = meshGo.AddComponent<NearRoadMesh>();
                _roadMesh.raycastTarget = false;
                _roadMesh.owner = this;
                _roadMesh.texture = road;
                _roadMesh.color = Color.white;
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
            _roadImg.enabled = _src != null && _roadMesh == null;

            _pineTex = new Texture2D[PinePaths.Length];
            int found = 0;
            for (int i = 0; i < PinePaths.Length; i++)
            {
                _pineTex[i] = Resources.Load<Texture2D>(PinePaths[i]);
                if (_pineTex[i] != null)
                {
                    _pineTex[i].filterMode = SpriteFilter;
                    found++;
                }
            }
            _deadTex = Resources.Load<Texture2D>(DeadPath);
            if (_deadTex != null) _deadTex.filterMode = SpriteFilter;

            if (found > 0)
            {
                int rows = Mathf.Max(1, pineRows);
                int per = Mathf.Max(1, pinesPerRow);
                int idx = 0;
                for (int r = 0; r < rows; r++)
                {
                    for (int sd = 0; sd < 2; sd++)
                    {
                        float side = sd == 0 ? -1f : 1f;
                        // stagger rows/sides so trunks don't line up
                        float offset = ((r * 0.37f + sd * 0.53f) % 1f) * span / per;
                        for (int k = 0; k < per; k++)
                        {
                            var p = new Pine();
                            var go = new GameObject("NearPine_r" + r + (sd == 0 ? "L" : "R") + k, typeof(RectTransform));
                            go.transform.SetParent(transform, false);
                            p.rt = go.GetComponent<RectTransform>();
                            p.rt.anchorMin = Vector2.zero;
                            p.rt.anchorMax = Vector2.zero;
                            p.rt.pivot = new Vector2(0.5f, 0f);
                            p.img = go.AddComponent<RawImage>();
                            p.img.raycastTarget = false;
                            p.img.enabled = false;
                            p.row = r;
                            p.side = side;
                            p.phase = offset + (k + (float)_rng.NextDouble() * 0.6f) * span / per;
                            Randomize(p, idx++);
                            p.lastZ = float.MaxValue;
                            _pines.Add(p);
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("NearRoadLayer: no Pitch/T_L4_NearPine_* textures found");
            }

            // undergrowth pool, scattered on the verge between the pine rows
            _underTex = new Texture2D[UnderPaths.Length];
            int underFound = 0;
            for (int i = 0; i < UnderPaths.Length; i++)
            {
                _underTex[i] = Resources.Load<Texture2D>(UnderPaths[i]);
                if (_underTex[i] != null) { _underTex[i].filterMode = SpriteFilter; underFound++; }
            }
            if (buildUndergrowth && underFound > 0)
            {
                int per = Mathf.Max(1, undergrowthPerSide);
                int idx = 0;
                for (int sd = 0; sd < 2; sd++)
                {
                    float side = sd == 0 ? -1f : 1f;
                    for (int k = 0; k < per; k++)
                    {
                        var p = new Pine();
                        var go = new GameObject("Under_" + (sd == 0 ? "L" : "R") + k, typeof(RectTransform));
                        go.transform.SetParent(transform, false);
                        p.rt = go.GetComponent<RectTransform>();
                        p.rt.anchorMin = Vector2.zero;
                        p.rt.anchorMax = Vector2.zero;
                        p.rt.pivot = new Vector2(0.5f, 0f);
                        p.img = go.AddComponent<RawImage>();
                        p.img.raycastTarget = false;
                        p.img.enabled = false;
                        p.under = true;
                        p.side = side;
                        p.phase = (k + (float)_rng.NextDouble() * 0.9f + sd * 0.41f) * span / per;
                        Randomize(p, idx++);
                        p.lastZ = float.MaxValue;
                        _pines.Add(p);
                    }
                }
            }

            _dirty = true;
        }

        FilterMode SpriteFilter => smoothSprites ? FilterMode.Bilinear : FilterMode.Point;

        /// <summary>Real metres to layer units (cameraHeight = eye height).</summary>
        float M(float meters) => meters * cameraHeight / Mathf.Max(0.1f, eyeHeightMeters);

        float Rand(float a, float b) => Mathf.Lerp(a, b, (float)_rng.NextDouble());

        void Randomize(Pine p, int hint)
        {
            p.thin = (float)_rng.NextDouble();
            if (p.under) { RandomizeUnder(p); return; }
            float edge = roadWorldWidth * 0.5f * dirtEdge + sideMin;
            p.x = p.side * (edge + p.row * rowSpacing + (float)_rng.NextDouble() * rowJitter);
            float shrink = Mathf.Max(0.2f, 1f - rowShrink * p.row);
            float r01 = Mathf.Pow((float)_rng.NextDouble(), Mathf.Max(0.05f, sizeSkew));
            if (_rng.NextDouble() < giantChance) r01 = Mathf.Min(1f, r01 + 0.5f);
            p.size01 = r01;
            p.dead = _deadTex != null && _rng.NextDouble() < deadChance;
            if (p.dead)
            {
                p.height = M(Rand(deadHeight.x, deadHeight.y)) * shrink;
            }
            else
            {
                p.height = M(Mathf.Lerp(heightMin, heightMax, r01)) * shrink;
                if (r01 >= 0.999f || _rng.NextDouble() < giantChance * 0.5f) p.height *= Mathf.Max(1f, giantScale);
            }
            p.tint = Mathf.Clamp01(pineTint * (1f + rowLighten * p.row) + ((float)_rng.NextDouble() * 2f - 1f) * tintJitter);
            Texture2D t = p.dead ? _deadTex : null;
            for (int k = 0; k < _pineTex.Length && t == null; k++)
                t = _pineTex[(hint + _rng.Next(_pineTex.Length) + k) % _pineTex.Length];
            p.tex = t;
            if (!p.dead && t != null && t.width > t.height)
                p.height = M(Rand(wideFirHeight.x, wideFirHeight.y)) * shrink; // wide fir: short and broad
            p.img.texture = t;
        }

        void RandomizeUnder(Pine p)
        {
            float edge = roadWorldWidth * 0.5f * dirtEdge + sideMin;
            float outer = edge + Mathf.Max(0, pineRows - 1) * rowSpacing + rowJitter + undergrowthOuter;
            p.x = p.side * Rand(edge * undergrowthInner, outer);

            float total = 0f;
            for (int i = 0; i < _underTex.Length; i++)
                if (_underTex[i] != null) total += Weight(i);
            float pick = (float)_rng.NextDouble() * Mathf.Max(0.0001f, total);
            int kind = -1;
            for (int i = 0; i < _underTex.Length; i++)
            {
                if (_underTex[i] == null) continue;
                kind = i;
                pick -= Weight(i);
                if (pick <= 0f) break;
            }
            p.kind = kind;
            p.tex = kind >= 0 ? _underTex[kind] : null;
            p.img.texture = p.tex;
            Vector2 hr = kind == 0 ? bushHeight : kind == 1 ? juniperHeight : kind == 2 ? fernHeight
                       : kind == 3 ? grassHeight : boulderHeight;
            p.height = M(Rand(hr.x, hr.y));
            p.size01 = 1f;
            p.tint = Mathf.Clamp01(undergrowthTint + ((float)_rng.NextDouble() * 2f - 1f) * tintJitter);
        }

        float Weight(int i) =>
            undergrowthWeights != null && i < undergrowthWeights.Length ? Mathf.Max(0f, undergrowthWeights[i]) : 1f;

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

            if (_roadMesh != null)
            {
                // GPU road: just rebuild the (tiny) strip mesh when something moved
                // rebuild every frame while walking (continuous fractional scroll), otherwise only on change
                if (step > 0f || _meshPhase != _roadPhase || !Mathf.Approximately(_meshCam, RoadCam) || _meshSize != size)
                {
                    _meshPhase = _roadPhase;
                    _meshCam = RoadCam;
                    _meshSize = size;
                    _roadMesh.SetVerticesDirty();
                }
                _dirty = false;
            }
            else
            {
                if (size != _builtSize || !Mathf.Approximately(_builtHorizon, horizon) || _uIdx == null
                    || Mathf.Abs(_builtCam - RoadCam) > 0.0015f)
                    RebuildTables(size);

                if (_dirty)
                {
                    DrawRoad();
                    _dirty = false;
                }
            }

            UpdatePines(size);
        }

        /// <summary>
        /// Builds the perspective road as horizontal strips. Each strip edge is a row of constant depth z,
        /// so u is linear across it; v = (z + travelled) / tileLength is continuous (no pixel snapping), and the
        /// GPU samples the texture with mipmaps/trilinear/aniso, which removes the shimmer while walking.
        /// </summary>
        internal void FillRoadMesh(UnityEngine.UI.VertexHelper vh, Rect rect, Color32 col)
        {
            vh.Clear();
            if (_roadSrcTex == null || rect.width < 2f || rect.height < 2f) return;
            float aspect = rect.width / rect.height;
            float cam = RoadCam;
            float camRatio = cam / Mathf.Max(0.01f, cameraHeight);
            float zEnd = fadeEnd * camRatio + 0.5f;                       // nothing visible beyond this
            float yTop = Mathf.Clamp(horizon - cam * focal / zEnd, 0f, horizon);
            int rows = Mathf.Clamp(roadMeshRows, 4, 1000);
            float halfW = roadWorldWidth * 0.5f;
            float tl = Mathf.Max(0.01f, tileLength);
            var v = UIVertex.simpleVert;
            for (int i = 0; i <= rows; i++)
            {
                float t = i / (float)rows;
                // denser rows toward the far end, where depth changes fastest
                float y = yTop * (1f - (1f - t) * (1f - t));
                float sy = Mathf.Max(0.0005f, horizon - y);
                float z = cam * focal / sy;
                float fade = Mathf.Clamp01((fadeEnd - z / camRatio) / Mathf.Max(0.01f, fadeEnd - fadeStart));
                float cx = CenterX(y);
                float dx = (halfW * focal / z) / aspect;
                float vv = (z + _roadPhase) / tl;
                byte a = (byte)Mathf.RoundToInt(col.a * fade);
                var c = new Color32(col.r, col.g, col.b, a);
                float py = rect.yMin + y * rect.height;
                v.color = c;
                v.position = new Vector3(rect.xMin + (cx - dx) * rect.width, py, 0f);
                v.uv0 = new Vector4(0f, vv, 0f, 0f);
                vh.AddVert(v);
                v.position = new Vector3(rect.xMin + (cx + dx) * rect.width, py, 0f);
                v.uv0 = new Vector4(1f, vv, 0f, 0f);
                vh.AddVert(v);
                if (i > 0)
                {
                    int b = (i - 1) * 2;
                    vh.AddTriangle(b, b + 2, b + 3);
                    vh.AddTriangle(b, b + 3, b + 1);
                }
            }
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
            _builtCam = RoadCam;
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
                _roadTex.filterMode = smoothRoad ? FilterMode.Bilinear : FilterMode.Point;
                _roadTex.wrapMode = TextureWrapMode.Clamp;
                _roadImg.texture = _roadTex;
            }

            int n = _rtW * _rtH;
            if (_buf == null || _buf.Length != n) _buf = new Color32[n];
            if (_uIdx == null || _uIdx.Length != n) _uIdx = new int[n];
            if (_rowV == null || _rowV.Length != _rtH) _rowV = new float[_rtH];
            if (_rowFade == null || _rowFade.Length != _rtH) _rowFade = new byte[_rtH];
            float cam = _builtCam;

            float texPerMeter = _srcH / Mathf.Max(0.01f, tileLength);
            for (int r = 0; r < _rtH; r++)
            {
                float y = (r + 0.5f) / _rtH * horizon;        // fraction of screen height from bottom
                float sy = Mathf.Max(0.0005f, horizon - y);   // distance below the vanishing line
                float z = cam * focal / sy;                   // world depth of this row
                _rowV[r] = z * texPerMeter;
                float fade = Mathf.Clamp01((fadeEnd - z * cameraHeight / Mathf.Max(0.01f, cam)) / Mathf.Max(0.01f, fadeEnd - fadeStart));
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
                    Randomize(p, i); // just recycled to the far end - new look
                p.lastZ = z;

                float yb = horizon - CamEff * focal / z;                 // base height (fraction from bottom)
                float rv = Mathf.Clamp01(reveal01);
                float hFrac = p.height * (p.under ? 1f : 1f - revealTreeShrink * rv) * focal / z; // height in screen heights
                float xPx = (CenterX(Mathf.Max(0f, yb)) + (p.x * focal / z) / aspect) * size.x;
                float dxc = Mathf.Abs(xPx / size.x - 0.5f);
                float clear = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clearZoneInner, Mathf.Max(clearZoneInner + 0.001f, clearZoneOuter), dxc));
                if (!p.under)
                {
                    float cap = maxScreenHeight * Mathf.Lerp(minCapFraction, 1f, p.size01) * Mathf.Lerp(centerCapFraction, 1f, clear);
                    cap = Mathf.Max(0.05f, cap);
                    hFrac = cap * (float)System.Math.Tanh(hFrac / cap);  // soft cap: close trees stay low, centre stays open
                }
                float alpha = Mathf.Clamp01((nearClip + spanSafe - z) / Mathf.Max(0.01f, emergeLength));
                float thr = rv * (p.under ? revealUndergrowthThin : revealThin) * 1.08f; // x1.08: amount 1 => all gone
                alpha *= Mathf.Clamp01(1f + (p.thin - thr) / 0.08f);    // hill-climb thinning (soft)
                float hPx = hFrac * size.y;
                float wPx = p.tex != null ? hPx * p.tex.width / (float)p.tex.height : hPx * 0.4f;

                bool visible = p.tex != null && alpha > 0.003f && hPx > 1.5f
                               && xPx + wPx * 0.5f > 0f && xPx - wPx * 0.5f < size.x
                               && yb * size.y < size.y;
                p.img.enabled = visible;
                if (!visible) continue;

                p.rt.sizeDelta = new Vector2(wPx, hPx);
                p.rt.anchoredPosition = new Vector2(xPx, yb * size.y);
                p.img.color = new Color(Mathf.Clamp01(p.tint * nearShade.r), Mathf.Clamp01(p.tint * nearShade.g),
                                        Mathf.Clamp01(p.tint * nearShade.b), alpha);
                p.sortKey = z * z + p.x * p.x; // painter order by true distance
                _sorted.Add(p);
            }

            // far first, near last; index 0 is the road
            _sorted.Sort(FarFirst);
            for (int i = 0; i < _sorted.Count; i++)
                _sorted[i].rt.SetSiblingIndex(1 + i);
        }

        static int FarFirst(Pine a, Pine b) => b.sortKey.CompareTo(a.sortKey);

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
