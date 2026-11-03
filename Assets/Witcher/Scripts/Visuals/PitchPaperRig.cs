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

        struct SideRow
        {
            public RectTransform left;
            public RectTransform right;
            public RawImage leftImg;
            public RawImage rightImg;
            public float depth;     // 0 far … 1 near — near parts faster/more
            public float partMul;
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
                list.Add(MakeSideRow("L2_Mid", _bobRoot, sides, depth: 0.45f, partMul: 0.75f));
                list.Add(MakeSideRow("L2_Near", _bobRoot, sides, depth: 0.9f, partMul: 1.05f));
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
            ApplyPart(0f);
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

            ApplyPart(_part01);

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

        SideRow MakeSideRow(string name, Transform parent, Texture2D tex, float depth, float partMul)
        {
            var leftImg = MakeHalf(name + "_L", parent, tex, leftHalf: true);
            var rightImg = MakeHalf(name + "_R", parent, tex, leftHalf: false);
            // Keep full tree tips/bases inside frame (less crop)
            // Closer to player — larger side pines
            Overscan(leftImg.rectTransform, 48f);
            Overscan(rightImg.rectTransform, 48f);
            leftImg.rectTransform.localScale = new Vector3(1.18f, 1.18f, 1f);
            rightImg.rectTransform.localScale = new Vector3(1.18f, 1.18f, 1f);
            return new SideRow
            {
                left = leftImg.rectTransform,
                right = rightImg.rectTransform,
                leftImg = leftImg,
                rightImg = rightImg,
                depth = depth,
                partMul = partMul
            };
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
