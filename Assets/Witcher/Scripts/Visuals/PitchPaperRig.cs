using UnityEngine;
using UnityEngine.UI;
using Witcher.Player;

namespace Witcher.Visuals
{
    /// <summary>
    /// L1 vista + L2 side pines that PART outward while walking forward + sword.
    /// Only forward walk (strafe locked on the controller).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PitchPaperRig : MonoBehaviour
    {
        [Header("Parting pines (speed-linked)")]
        [Tooltip("How much parting accumulates per meter walked (lower = slower).")]
        public float partPerMeter = 0.045f;
        public float maxPartPx = 580f;
        public float startInsetPx = -120f; // closer/denser toward player at start

        [Header("L2 parting pines - walk-driven passing")]
        [Tooltip("On: the L2 pines continuously slide outward and grow while walking (like passing trees) and recycle. Off: old one-shot parting.")]
        public bool l2PassingMotion = true;
        [Tooltip("Extra vertical stretch of the L2 pines (taller trees). Scales up from the bottom edge.")]
        public float l2HeightScale = 1.15f;
        [Tooltip("Base scale of the L2 pines when they appear (far).")]
        public float l2BaseScale = 1.0f;
        [Tooltip("Extra scale gained while passing (near row gets the full amount).")]
        public float l2Grow = 0.35f;
        [Tooltip("Sideways offset (px) of a pine row when it appears (far). Ends at maxPartPx x row multiplier.")]
        public float l2StartPx = 100f;
        [Tooltip("How much closer a pine gets during one pass (zFar / zNear). Higher = stronger perspective acceleration at the end.")]
        public float l2DepthRatio = 3f;
        [Tooltip("Metres walked (same speed signal as the near road) for one full pass of the MID row.")]
        public float l2MidPassDistance = 14f;
        [Tooltip("Metres walked for one full pass of the NEAR row (shorter = faster = more parallax).")]
        public float l2NearPassDistance = 9f;
        [Tooltip("Fraction of a pass used to fade a recycled copy in / out.")]
        [Range(0.02f, 0.45f)] public float l2FadeIn = 0.18f;
        [Range(0.02f, 0.45f)] public float l2FadeOut = 0.22f;
        [Tooltip("Speed used when there is no near-road layer: metres of travel per m/s of walk speed.")]
        public float l2TravelPerMeter = 0.55f;
        public float l2Acceleration = 3.5f;

        [Header("Feel")]
        public float headBobPx = 8f;
        public float headBobSpeed = 9.5f;
        public float swordBobPx = 11f;

        [Header("L3 near road (walking layer)")]
        [Tooltip("Scrolling road + dark near pines between the parting pines (L2) and the sword. Tune it on the NearRoadLayer component under PitchUI/BobRoot/L3_RoadNearPines in Play mode.")]
        public bool buildNearRoad = true;

        Canvas _canvas;
        RectTransform _bobRoot;
        RectTransform _vistaRt;
        SideRow[] _rows;
        RectTransform _swordRt;
        NearRoadLayer _nearRoad;
        Vector2 _vistaBase;
        Vector2 _swordBase;
        FirstPersonController _mover;
        float _bobPhase;
        float _part01; // 0 closed … 1 fully parted — driven by walk speed
        float _l2Speed; // smoothed travel speed (m/s) when no near road exists

        struct SideRow
        {
            public RectTransform left;
            public RectTransform right;
            public RawImage leftImg;
            public RawImage rightImg;
            public float depth;     // 0 far … 1 near — near parts faster/more
            public float partMul;
            // second copy of the same row, half a pass apart, for seamless recycling
            public RectTransform leftB;
            public RectTransform rightB;
            public RawImage leftImgB;
            public RawImage rightImgB;
            public float passDistance;
            public float phase;     // 0..1 progress of copy A through its pass
            public float baseY;
        }

        void Awake()
        {
            _mover = GetComponentInParent<FirstPersonController>();
            if (_mover != null)
            {
                _mover.allowStrafe = false;
                _mover.allowTurn = false;
            }
            KillBrokenWorldLayers();
            EnsureBuilt();
            ApplyCleanLook();
        }

        void Start()
        {
            ApplyCleanLook();
            if (_canvas == null) EnsureBuilt();
            if (_mover != null)
            {
                _mover.allowStrafe = false;
                _mover.allowTurn = false;
                _mover.transform.rotation = Quaternion.identity;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoAttachOnPlay()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (cam.GetComponent<PitchPaperRig>() != null) return;
            if (Resources.Load<Texture2D>("Pitch/T_World_Vista") == null) return;
            cam.gameObject.AddComponent<PitchPaperRig>();
        }

        [ContextMenu("Rebuild Paper Layers")]
        public void EnsureBuilt()
        {
            KillBrokenWorldLayers();

            var pixel = GetComponent<SimplePixelCamera>();
            if (pixel != null) pixel.enabled = false;

            foreach (var sw in GetComponentsInChildren<SpriteHeldWeapon>(true))
                sw.gameObject.SetActive(false);

            DestroyChild(transform, "PitchPaper");
            DestroyChild(transform, "PitchUI");

            var vistaTex = Resources.Load<Texture2D>("Pitch/T_World_Vista");
            if (vistaTex == null)
            {
                Debug.LogError("PitchPaperRig: missing Pitch/T_World_Vista");
                return;
            }
            vistaTex.filterMode = FilterMode.Point;

            var sides = Resources.Load<Texture2D>("Pitch/T_L3_NearPines");
            if (sides != null) sides.filterMode = FilterMode.Point;

            var ui = new GameObject("PitchUI");
            ui.transform.SetParent(transform, false);
            _canvas = ui.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            ui.AddComponent<GraphicRaycaster>();

            var bob = new GameObject("BobRoot", typeof(RectTransform));
            bob.transform.SetParent(ui.transform, false);
            _bobRoot = bob.GetComponent<RectTransform>();
            StretchFull(_bobRoot);

            // L1 clean vista
            var vista = MakeFull("L1_Vista", _bobRoot, vistaTex);
            _vistaRt = vista.rectTransform;
            _vistaBase = Vector2.zero;
            Overscan(_vistaRt, 24f);

            // L2 many side-pine rows — start dense, part left/right while walking
            var list = new System.Collections.Generic.List<SideRow>();
            if (sides != null)
            {
                // Fewer rows = fewer overlapping side trees
                list.Add(MakeSideRow("L2_Mid", _bobRoot, sides, depth: 0.45f, partMul: 0.75f, passDistance: l2MidPassDistance, phase: 0.22f));
                list.Add(MakeSideRow("L2_Near", _bobRoot, sides, depth: 0.9f, partMul: 1.05f, passDistance: l2NearPassDistance, phase: 0.05f));
            }
            _rows = list.ToArray();

            // L3 near road + near pines: moves toward the viewer while walking
            _nearRoad = null;
            if (buildNearRoad)
            {
                var roadGo = new GameObject("L3_RoadNearPines", typeof(RectTransform));
                roadGo.transform.SetParent(_bobRoot, false);
                var roadRt = roadGo.GetComponent<RectTransform>();
                StretchFull(roadRt);
                Overscan(roadRt, 24f);
                _nearRoad = roadGo.AddComponent<NearRoadLayer>();
                _nearRoad.mover = _mover;
                _nearRoad.Build();
            }

            BuildSwordUi(ui.transform);
            if (l2PassingMotion) ApplyPassing(0f, false, 0f);
            else ApplyPart(0f);
        }

        void LateUpdate()
        {
            bool moving = _mover != null && _mover.IsMoving;
            bool sprint = _mover != null && _mover.IsSprinting;
            float speed = _mover != null ? _mover.PlanarSpeed : 0f;

            // Parting = integral of walk speed (stop walking → pines stop parting)
            if (moving && speed > 0.05f)
                _part01 = Mathf.Clamp01(_part01 + speed * partPerMeter * Time.deltaTime);

            float bobRate = headBobSpeed * (sprint ? 1.3f : 1f) * (moving ? 1f : 0.2f);
            _bobPhase += Time.deltaTime * bobRate;
            float bobAmp = moving ? headBobPx : headBobPx * 0.2f;
            if (_bobRoot != null)
            {
                _bobRoot.anchoredPosition = new Vector2(
                    Mathf.Sin(_bobPhase) * bobAmp * 0.35f,
                    Mathf.Abs(Mathf.Cos(_bobPhase)) * bobAmp);
            }

            if (_vistaRt != null)
            {
                float zoom = Mathf.Lerp(1f, 1.05f, _part01);
                _vistaRt.localScale = new Vector3(zoom, zoom, 1f);
                _vistaRt.anchoredPosition = _vistaBase;
            }

            if (l2PassingMotion) ApplyPassing(Time.deltaTime, moving, speed);
            else ApplyPart(_part01);

            if (_swordRt != null)
            {
                float samp = moving ? swordBobPx : swordBobPx * 0.25f;
                _swordRt.anchoredPosition = _swordBase + new Vector2(
                    Mathf.Sin(_bobPhase) * samp,
                    Mathf.Abs(Mathf.Cos(_bobPhase)) * samp * 0.65f);
            }
        }

        void ApplyPart(float t)
        {
            // Slow ease — feels like walking through, not snapping open
            float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            if (_rows == null) return;
            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                float local = Mathf.Clamp01(open * row.partMul);
                local = Mathf.SmoothStep(0f, 1f, local);

                // Dense at center → slowly slide left/right with steps
                float x = Mathf.Lerp(startInsetPx, maxPartPx * row.partMul, local);
                float scale = Mathf.Lerp(1.18f, 1.18f + 0.22f * row.depth, local);

                if (row.left != null)
                {
                    row.left.anchoredPosition = new Vector2(-x, 0f);
                    row.left.localScale = new Vector3(scale, scale, 1f);
                }
                if (row.right != null)
                {
                    row.right.anchoredPosition = new Vector2(x, 0f);
                    row.right.localScale = new Vector3(scale, scale, 1f);
                }

                // Stay visible while parting; only soft fade when far aside
                float alpha = local < 0.9f ? 1f : Mathf.Lerp(1f, 0.35f, (local - 0.9f) / 0.1f);
                if (row.leftImg != null) row.leftImg.color = new Color(1f, 1f, 1f, alpha);
                if (row.rightImg != null) row.rightImg.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        /// <summary>
        /// Walk-driven passing: every row has two copies half a pass apart. Each copy travels
        /// from "far" (dense, near the centre, small) to "near" (pushed out past the screen edge,
        /// large) with a 1/z perspective curve, fading in at the start and out at the end, so the
        /// loop is seamless. Progress = metres walked / pass distance, using the same smoothed
        /// speed as the near-road layer, so both layers stop and start together.
        /// </summary>
        void ApplyPassing(float dt, bool moving, float walkSpeed)
        {
            float travel;
            if (_nearRoad != null && _nearRoad.isActiveAndEnabled)
            {
                travel = _nearRoad.CurrentSpeed;
                _l2Speed = travel;
            }
            else
            {
                float target = moving ? walkSpeed * l2TravelPerMeter : 0f;
                float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, l2Acceleration) * dt);
                _l2Speed = Mathf.Lerp(_l2Speed, target, k);
                if (target <= 0f && _l2Speed < 0.002f) _l2Speed = 0f;
                travel = _l2Speed;
            }

            if (_rows == null) return;
            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                if (dt > 0f && travel > 0f)
                    row.phase = Mathf.Repeat(row.phase + travel * dt / Mathf.Max(0.5f, row.passDistance), 1f);
                PlaceCopy(row, row.left, row.right, row.leftImg, row.rightImg, row.phase);
                PlaceCopy(row, row.leftB, row.rightB, row.leftImgB, row.rightImgB, Mathf.Repeat(row.phase + 0.5f, 1f));
                _rows[i] = row;
            }
        }

        void PlaceCopy(SideRow row, RectTransform l, RectTransform r, RawImage li, RawImage ri, float p)
        {
            // depth goes linearly from zFar (=ratio) to zNear (=1) => screen offset ~ 1/z accelerates at the end
            float ratio = Mathf.Max(1.05f, l2DepthRatio);
            float z = Mathf.Lerp(ratio, 1f, p);
            float persp = (ratio / z - 1f) / (ratio - 1f); // 0 .. 1, perspective-curved

            float x = Mathf.Lerp(l2StartPx, maxPartPx * row.partMul, persp);
            float s = l2BaseScale * (1f + l2Grow * (0.35f + 0.65f * row.depth) * persp);
            float alpha = Mathf.SmoothStep(0f, 1f, p / l2FadeIn)
                        * (1f - Mathf.SmoothStep(0f, 1f, (p - (1f - l2FadeOut)) / l2FadeOut));
            var scale = new Vector3(s, s * Mathf.Max(0.1f, l2HeightScale), 1f);

            if (l != null) { l.anchoredPosition = new Vector2(-x, row.baseY); l.localScale = scale; }
            if (r != null) { r.anchoredPosition = new Vector2(x, row.baseY); r.localScale = scale; }
            var c = new Color(1f, 1f, 1f, alpha);
            if (li != null) { li.color = c; li.enabled = alpha > 0.003f; }
            if (ri != null) { ri.color = c; ri.enabled = alpha > 0.003f; }
        }

        SideRow MakeSideRow(string name, Transform parent, Texture2D tex, float depth, float partMul,
                            float passDistance, float phase)
        {
            var row = new SideRow { depth = depth, partMul = partMul, passDistance = passDistance, phase = phase };
            if (l2PassingMotion)
            {
                // copy B first so copy A (the one that starts nearer) draws on top initially
                var lb = MakeHalf(name + "_L_B", parent, tex, leftHalf: true);
                var rb = MakeHalf(name + "_R_B", parent, tex, leftHalf: false);
                PrepSide(lb.rectTransform); PrepSide(rb.rectTransform);
                row.leftB = lb.rectTransform; row.rightB = rb.rectTransform;
                row.leftImgB = lb; row.rightImgB = rb;
            }
            var leftImg = MakeHalf(name + "_L", parent, tex, leftHalf: true);
            var rightImg = MakeHalf(name + "_R", parent, tex, leftHalf: false);
            if (l2PassingMotion)
            {
                PrepSide(leftImg.rectTransform); PrepSide(rightImg.rectTransform);
            }
            else
            {
                // Keep full tree tips/bases inside frame (less crop)
                Overscan(leftImg.rectTransform, 48f);
                Overscan(rightImg.rectTransform, 48f);
                leftImg.rectTransform.localScale = new Vector3(1.18f, 1.18f, 1f);
                rightImg.rectTransform.localScale = new Vector3(1.18f, 1.18f, 1f);
            }
            row.left = leftImg.rectTransform;
            row.right = rightImg.rectTransform;
            row.leftImg = leftImg;
            row.rightImg = rightImg;
            row.baseY = leftImg.rectTransform.anchoredPosition.y;
            return row;
        }

        static void PrepSide(RectTransform rt)
        {
            // pivot on the bottom edge so extra height / growth goes UP (trunks stay planted)
            rt.pivot = new Vector2(0.5f, 0f);
            Overscan(rt, 48f);
        }

        void BuildSwordUi(Transform parent)
        {
            var tex = Resources.Load<Texture2D>("Pitch/T_Sword_Pixel");
            if (tex == null)
            {
                Debug.LogWarning("PitchPaperRig: missing Pitch/T_Sword_Pixel");
                return;
            }
            tex.filterMode = FilterMode.Point;

            var go = new GameObject("L4_Sword", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            _swordRt = go.GetComponent<RectTransform>();
            _swordRt.anchorMin = new Vector2(1f, 0f);
            _swordRt.anchorMax = new Vector2(1f, 0f);
            _swordRt.pivot = new Vector2(1f, 0f);
            // Bottom-right, classic Doom viewmodel size
            _swordRt.sizeDelta = new Vector2(380f, 480f);
            _swordBase = new Vector2(-16f, -10f);
            _swordRt.anchoredPosition = _swordBase;

            var raw = go.AddComponent<RawImage>();
            raw.texture = tex;
            raw.color = Color.white;
            raw.raycastTarget = false;
        }

        static RawImage MakeFull(string name, Transform parent, Texture2D tex)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            StretchFull(rt);
            var raw = go.AddComponent<RawImage>();
            raw.texture = tex;
            raw.color = Color.white;
            raw.raycastTarget = false;
            return raw;
        }

        static RawImage MakeHalf(string name, Transform parent, Texture2D tex, bool leftHalf)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            StretchFull(rt);
            var raw = go.AddComponent<RawImage>();
            raw.texture = tex;
            raw.color = Color.white;
            raw.raycastTarget = false;
            raw.uvRect = leftHalf
                ? new Rect(0f, 0f, 0.52f, 1f)
                : new Rect(0.48f, 0f, 0.52f, 1f);
            return raw;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        static void Overscan(RectTransform rt, float px)
        {
            rt.offsetMin = new Vector2(-px, -px);
            rt.offsetMax = new Vector2(px, px);
        }

        static void DestroyChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) DestroySafe(t.gameObject);
        }

        static void KillBrokenWorldLayers()
        {
            var env = GameObject.Find("_Environment");
            if (env == null) return;
            var old = env.transform.Find("LayeredBackdrop");
            if (old != null) DestroySafe(old.gameObject);
            var forest = env.transform.Find("Forest");
            if (forest != null) forest.gameObject.SetActive(false);
            var castle = env.transform.Find("Castle");
            if (castle != null) castle.gameObject.SetActive(false);
        }

        static void ApplyCleanLook()
        {
            RenderSettings.fog = false;
            RenderSettings.ambientLight = new Color(0.6f, 0.5f, 0.55f);
            var cam = Camera.main;
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.05f, 0.1f);
        }

        static void DestroySafe(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}
