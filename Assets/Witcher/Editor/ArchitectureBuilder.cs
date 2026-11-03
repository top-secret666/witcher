#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Witcher.Core;
using Witcher.Data;
using Witcher.Environment;
using Witcher.Player;
using Witcher.Visuals;

namespace Witcher.EditorTools
{
    /// <summary>
    /// Builds project architecture assets (materials, SO, prefabs) and a clean demo scene.
    /// Run once from the menu, then edit prefabs/scene in the Editor like a normal Unity project.
    /// </summary>
    public static class ArchitectureBuilder
    {
        const string Root = "Assets/Witcher";
        const string MatPath = Root + "/Art/Materials";
        const string DataPath = Root + "/Data/Atmosphere";
        const string PrefabEnv = Root + "/Prefabs/Environment";
        const string PrefabCastle = PrefabEnv + "/Castle";
        const string PrefabNature = PrefabEnv + "/Nature";
        const string PrefabPlayer = Root + "/Prefabs/Player";
        const string PrefabSystems = Root + "/Prefabs/Systems";
        const string ScenePath = Root + "/Scenes/DarkFantasy_Demo.unity";
        const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

        // Brighter than pure black so moonlight can sculpt silhouettes (see mood refs).
        static readonly Color ColStone = new Color(0.38f, 0.36f, 0.34f);
        static readonly Color ColRoof = new Color(0.22f, 0.14f, 0.16f);
        static readonly Color ColGround = new Color(0.14f, 0.16f, 0.12f);
        static readonly Color ColPath = new Color(0.32f, 0.3f, 0.28f);
        static readonly Color ColBark = new Color(0.16f, 0.1f, 0.07f);
        static readonly Color ColFoliage = new Color(0.08f, 0.16f, 0.1f);
        static readonly Color ColGate = new Color(0.08f, 0.06f, 0.07f);
        static readonly Color ColBlade = new Color(0.75f, 0.95f, 1f);
        static readonly Color ColHilt = new Color(0.35f, 0.22f, 0.12f);
        static readonly Color ColMetal = new Color(0.7f, 0.7f, 0.75f);
        static readonly Color ColMoon = new Color(1f, 0.95f, 0.75f);

        [MenuItem("Witcher/Architecture/1. Generate Materials + Profiles + Prefabs")]
        public static void GenerateAssets()
        {
            EnsureFolders();
            CreateMaterials();
            CreateAtmosphereProfiles();
            CreateModularPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Witcher Architecture",
                "Созданы:\n• Materials\n• Atmosphere profiles (SO)\n• Prefabs (Castle modules, Pine, Player, Atmosphere)\n\n" +
                "Дальше: Witcher → Architecture → 2. Rebuild Demo Scene From Prefabs",
                "OK");
        }

        [MenuItem("Witcher/Architecture/2. Rebuild Demo Scene From Prefabs")]
        public static void RebuildSceneFromPrefabs()
        {
            if (!PrefabsExist())
            {
                if (EditorUtility.DisplayDialog("Witcher", "Сначала сгенерируй ассеты (шаг 1).", "Сгенерировать сейчас", "Отмена"))
                    GenerateAssets();
                else
                    return;
            }

            DarkFantasySetup.AddRetroLookFeature();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Hierarchy roots (Unity architecture: empty organizers) ---
            var systems = new GameObject(SceneRoots.Systems);
            var environment = new GameObject(SceneRoots.Environment);
            var gameplay = new GameObject(SceneRoots.Gameplay);

            // Systems
            var lightingRoot = new GameObject(SceneRoots.Lighting);
            lightingRoot.transform.SetParent(systems.transform, false);

            var sunGo = new GameObject("Directional Light");
            sunGo.transform.SetParent(lightingRoot.transform, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;

            // Soft fill so buildings aren't pure black silhouettes
            var fillGo = new GameObject("Fill Light");
            fillGo.transform.SetParent(lightingRoot.transform, false);
            fillGo.transform.rotation = Quaternion.Euler(25f, 40f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.shadows = LightShadows.None;
            fill.color = new Color(0.35f, 0.55f, 0.75f);
            fill.intensity = 0.25f;

            var moonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSystems + "/PF_Moon.prefab");
            if (moonPrefab != null)
            {
                var moon = (GameObject)PrefabUtility.InstantiatePrefab(moonPrefab);
                moon.transform.SetParent(lightingRoot.transform, false);
            }

            var atmospherePrefab = LoadPrefab(PrefabSystems + "/PF_Atmosphere.prefab");
            var atmosphereGo = (GameObject)PrefabUtility.InstantiatePrefab(atmospherePrefab);
            atmosphereGo.name = SceneRoots.Atmosphere;
            atmosphereGo.transform.SetParent(systems.transform, false);
            var atmosphere = atmosphereGo.GetComponent<DarkFantasyAtmosphere>();
            atmosphere.sunOrMoon = sun;
            // Default mood matches the gothic night reference
            atmosphere.profile = LoadAsset<AtmosphereProfile>(DataPath + "/Atmosphere_ManorNight.asset");

            // Environment — ground (thick box floor — Plane colliders cause fall-through)
            var groundRoot = new GameObject(SceneRoots.Ground);
            groundRoot.transform.SetParent(environment.transform, false);
            InstantiateChild(PrefabEnv + "/PF_Ground.prefab", groundRoot.transform, new Vector3(0f, -0.5f, 20f));
            InstantiateChild(PrefabEnv + "/PF_Path.prefab", groundRoot.transform, new Vector3(0f, 0.05f, 12f));

            // Forest
            var forestRoot = new GameObject(SceneRoots.Forest);
            forestRoot.transform.SetParent(environment.transform, false);
            var pinePrefab = LoadPrefab(PrefabNature + "/PF_Pine.prefab");
            Random.InitState(42);
            for (int i = 0; i < 36; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float z = 2f + (i / 2) * 1.5f;
                float x = side * (2.6f + Random.Range(0f, 7f));
                var pine = (GameObject)PrefabUtility.InstantiatePrefab(pinePrefab);
                pine.transform.SetParent(forestRoot.transform, false);
                pine.transform.position = new Vector3(x, 0f, z);
                float s = Random.Range(0.85f, 1.35f);
                pine.transform.localScale = Vector3.one * s;
            }

            // Castle (composed prefab)
            var castle = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(PrefabCastle + "/PF_Castle.prefab"));
            castle.name = SceneRoots.Castle;
            castle.transform.SetParent(environment.transform, false);
            castle.transform.position = new Vector3(0f, 0f, 38f);

            // Gameplay — player
            var player = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(PrefabPlayer + "/PF_Player.prefab"));
            player.name = SceneRoots.Player;
            player.transform.SetParent(gameplay.transform, false);
            player.transform.position = new Vector3(0f, 0.1f, 2f);

            var cam = player.GetComponentInChildren<Camera>();
            atmosphere.mainCamera = cam;
            var moonDisc = Object.FindAnyObjectByType<MoonDisc>();
            if (moonDisc != null && cam != null)
                moonDisc.followCamera = cam.transform;
            atmosphere.Apply();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Selection.activeGameObject = castle;
            EditorGUIUtility.PingObject(castle);

            EditorUtility.DisplayDialog(
                "Witcher — сцена собрана грамотно",
                "Иерархия:\n" +
                "  _Systems → Lighting, Atmosphere\n" +
                "  _Environment → Ground, Forest, Castle\n" +
                "  _Gameplay → Player\n\n" +
                "Замок — префаб PF_Castle из модулей.\n" +
                "Mood — ScriptableObject в Atmosphere.\n\n" +
                "Прави: префабы в Project, не «сырые» кубы в сцене.",
                "Поняла");
        }

        [MenuItem("Witcher/Architecture/3. Apply Atmosphere/Castle Dusk")]
        public static void ApplyDusk()
        {
            ApplyProfile(DataPath + "/Atmosphere_CastleDusk.asset");
        }

        [MenuItem("Witcher/Architecture/3. Apply Atmosphere/Manor Night")]
        public static void ApplyNight()
        {
            ApplyProfile(DataPath + "/Atmosphere_ManorNight.asset");
        }

        [MenuItem("Witcher/Fix/Soften Retro Look (less noise)")]
        public static void SoftenRetroLook()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRendererPath);
            if (renderer == null)
            {
                EditorUtility.DisplayDialog("Witcher", "PC_Renderer не найден.", "OK");
                return;
            }

            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is not RetroLookFeature retro)
                    continue;

                retro.settings.pixelSize = 2f;
                retro.settings.colorSteps = 18f;
                retro.settings.ditherStrength = 0.18f;
                retro.settings.contrast = 1.08f;
                retro.settings.saturation = 0.95f;
                retro.settings.vignette = 0.28f;
                EditorUtility.SetDirty(retro);
                EditorUtility.SetDirty(renderer);
                AssetDatabase.SaveAssets();
                Selection.activeObject = renderer;
                EditorUtility.DisplayDialog("Witcher", "Retro Look смягчён: меньше пикселя и дизера.\nКартинка станет читаемее.", "OK");
                return;
            }

            EditorUtility.DisplayDialog("Witcher", "Retro Look ещё не добавлен. Сначала Architecture → 2.", "OK");
        }

        [MenuItem("Witcher/Fix/Repair Ground (stop falling)")]
        public static void RepairGroundInCurrentScene()
        {
            EnsureFolders();
            CreateMaterials();

            var groundRoot = GameObject.Find(SceneRoots.Environment + "/" + SceneRoots.Ground);
            if (groundRoot == null)
            {
                var env = GameObject.Find(SceneRoots.Environment) ?? new GameObject(SceneRoots.Environment);
                groundRoot = new GameObject(SceneRoots.Ground);
                groundRoot.transform.SetParent(env.transform, false);
            }

            // Remove old thin/plane floors
            for (int i = groundRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = groundRoot.transform.GetChild(i).gameObject;
                if (child.name.Contains("Ground") || child.name.Contains("PF_Ground") || child.name.Contains("Plane"))
                    Object.DestroyImmediate(child);
            }

            // Thick box: top surface at y=0
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor_Solid";
            floor.transform.SetParent(groundRoot.transform, false);
            floor.transform.position = new Vector3(0f, -0.5f, 20f);
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = LoadMat("M_Ground");

            var player = Object.FindAnyObjectByType<FirstPersonController>();
            if (player != null)
            {
                player.transform.position = new Vector3(0f, 0.2f, 2f);
                player.SnapToGround();
            }

            EditorSceneManager.MarkAllScenesDirty();
            Selection.activeGameObject = floor;
            EditorUtility.DisplayDialog(
                "Witcher Fix",
                "Поставила толстый пол Floor_Solid (Cube + BoxCollider).\n" +
                "Игрок привязан к земле.\n\nPlay снова — не должна проваливаться.",
                "OK");
        }

        static void ApplyProfile(string path)
        {
            var atmosphere = Object.FindAnyObjectByType<DarkFantasyAtmosphere>();
            var profile = LoadAsset<AtmosphereProfile>(path);
            if (atmosphere == null || profile == null)
            {
                EditorUtility.DisplayDialog("Witcher", "Нужны сцена с Atmosphere и сгенерированные profiles.", "OK");
                return;
            }

            atmosphere.profile = profile;
            atmosphere.Apply();
            EditorUtility.SetDirty(atmosphere);
        }

        // -------------------------------------------------------------------------
        // Asset generation
        // -------------------------------------------------------------------------

        static void EnsureFolders()
        {
            CreateFolder("Assets", "Witcher");
            CreateFolder(Root, "Art");
            CreateFolder(Root + "/Art", "Materials");
            CreateFolder(Root + "/Art", "Reference");
            CreateFolder(Root, "Data");
            CreateFolder(Root + "/Data", "Atmosphere");
            CreateFolder(Root, "Prefabs");
            CreateFolder(Root + "/Prefabs", "Environment");
            CreateFolder(PrefabEnv, "Castle");
            CreateFolder(PrefabEnv, "Nature");
            CreateFolder(Root + "/Prefabs", "Player");
            CreateFolder(Root + "/Prefabs", "Systems");
            CreateFolder(Root, "Scenes");
            CreateFolder(Root, "Scripts");
        }

        static void CreateFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        static void CreateMaterials()
        {
            SaveMat("M_Stone", ColStone);
            SaveMat("M_Roof", ColRoof);
            SaveMat("M_Ground", ColGround);
            SaveMat("M_Path", ColPath);
            SaveMat("M_Bark", ColBark);
            SaveMat("M_Foliage", ColFoliage);
            SaveMat("M_Gate", ColGate);
            SaveMat("M_Blade", ColBlade, emissive: new Color(0.4f, 1.4f, 1.8f));
            SaveMat("M_Hilt", ColHilt);
            SaveMat("M_Metal", ColMetal);
            SaveMat("M_Moon", ColMoon, emissive: new Color(2f, 1.8f, 1.2f));
        }

        static Material SaveMat(string name, Color color, Color? emissive = null)
        {
            var path = $"{MatPath}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = existing != null ? existing : new Material(shader != null ? shader : Shader.Find("Standard"));

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            mat.color = color;

            if (emissive.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissive.Value);
            }

            if (existing == null)
                AssetDatabase.CreateAsset(mat, path);
            else
                EditorUtility.SetDirty(mat);

            return mat;
        }

        static void CreateAtmosphereProfiles()
        {
            WriteProfile("Atmosphere_CastleDusk", p =>
            {
                p.displayName = "Castle Dusk";
                p.fogColor = new Color(0.28f, 0.16f, 0.22f);
                p.fogDensity = 0.014f;
                p.ambientLight = new Color(0.28f, 0.2f, 0.3f);
                p.shadowColor = new Color(0.08f, 0.05f, 0.1f);
                p.lightColor = new Color(1f, 0.5f, 0.32f);
                p.lightIntensity = 1.4f;
                p.lightEuler = new Vector3(12f, -35f, 0f);
                p.cameraBackground = new Color(0.22f, 0.12f, 0.2f);
            });

            // Tuned toward the gothic night reference: teal sky, readable stone, cool moon
            WriteProfile("Atmosphere_ManorNight", p =>
            {
                p.displayName = "Manor Night";
                p.fogColor = new Color(0.12f, 0.22f, 0.28f);
                p.fogDensity = 0.012f;
                p.ambientLight = new Color(0.18f, 0.28f, 0.36f);
                p.shadowColor = new Color(0.04f, 0.06f, 0.1f);
                p.lightColor = new Color(0.7f, 0.85f, 1f);
                p.lightIntensity = 1.1f;
                p.lightEuler = new Vector3(35f, 150f, 0f);
                p.cameraBackground = new Color(0.1f, 0.18f, 0.26f);
            });
        }

        static void WriteProfile(string name, System.Action<AtmosphereProfile> fill)
        {
            var path = $"{DataPath}/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<AtmosphereProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            fill(profile);
            EditorUtility.SetDirty(profile);
        }

        static void CreateModularPrefabs()
        {
            var stone = LoadMat("M_Stone");
            var roof = LoadMat("M_Roof");
            var gate = LoadMat("M_Gate");

            // Modular pieces
            SavePrefab(MakeBox("PF_CastleKeep", new Vector3(8f, 10f, 6f), stone), PrefabCastle + "/PF_CastleKeep.prefab");
            SavePrefab(MakeBox("PF_CastleWing", new Vector3(5f, 7f, 5f), stone), PrefabCastle + "/PF_CastleWing.prefab");
            SavePrefab(MakeBox("PF_CastleTower", new Vector3(2.8f, 14f, 2.8f), stone), PrefabCastle + "/PF_CastleTower.prefab");
            SavePrefab(MakeBox("PF_CastleRoof", new Vector3(3.4f, 2.2f, 3.4f), roof), PrefabCastle + "/PF_CastleRoof.prefab");
            SavePrefab(MakeBox("PF_CastleGate", new Vector3(2.6f, 4.5f, 0.8f), gate), PrefabCastle + "/PF_CastleGate.prefab");
            SavePrefab(MakeBox("PF_CastleStairs", new Vector3(4.5f, 1.4f, 5f), stone), PrefabCastle + "/PF_CastleStairs.prefab");
            SavePrefab(MakeBox("PF_Hill", new Vector3(36f, 6f, 16f), LoadMat("M_Ground")), PrefabCastle + "/PF_Hill.prefab");

            // Composed castle
            var castleRoot = new GameObject("PF_Castle");
            var landmark = castleRoot.AddComponent<CastleLandmark>();

            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_Hill.prefab", new Vector3(0f, -2.5f, 2f));
            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_CastleKeep.prefab", new Vector3(0f, 5f, 0f));
            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_CastleWing.prefab", new Vector3(-7f, 3.5f, 1f));
            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_CastleWing.prefab", new Vector3(7f, 3.5f, 1f));
            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_CastleTower.prefab", new Vector3(-10f, 7f, -1.5f));
            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_CastleTower.prefab", new Vector3(10f, 7f, -1.5f));
            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_CastleTower.prefab", new Vector3(0f, 9f, -2.5f));
            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_CastleRoof.prefab", new Vector3(-10f, 14.5f, -1.5f));
            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_CastleRoof.prefab", new Vector3(10f, 14.5f, -1.5f));
            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_CastleRoof.prefab", new Vector3(0f, 16.5f, -2.5f));
            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_CastleStairs.prefab", new Vector3(0f, 0.7f, -6f));
            PlaceModule(castleRoot.transform, PrefabCastle + "/PF_CastleGate.prefab", new Vector3(0f, 2.2f, -3.5f));

            var approach = new GameObject("ApproachPoint");
            approach.transform.SetParent(castleRoot.transform, false);
            approach.transform.localPosition = new Vector3(0f, 0f, -10f);
            landmark.approachPoint = approach.transform;

            SavePrefab(castleRoot, PrefabCastle + "/PF_Castle.prefab");

            // Thick cube floor (top at local y=+0.5 when scale.y=1; place at y=-0.5 in scene)
            SavePrefab(MakeScaled("PF_Ground", PrimitiveType.Cube, new Vector3(100f, 1f, 100f), LoadMat("M_Ground")), PrefabEnv + "/PF_Ground.prefab");
            var path = MakeScaled("PF_Path", PrimitiveType.Cube, new Vector3(3.2f, 0.08f, 32f), LoadMat("M_Path"));
            Object.DestroyImmediate(path.GetComponent<Collider>()); // visual only — floor carries collision
            SavePrefab(path, PrefabEnv + "/PF_Path.prefab");

            // Pine
            var pine = new GameObject("PF_Pine");
            var trunk = MakeScaled("Trunk", PrimitiveType.Cylinder, new Vector3(0.2f, 1.2f, 0.2f), LoadMat("M_Bark"));
            trunk.transform.SetParent(pine.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Object.DestroyImmediate(trunk.GetComponent<Collider>());
            var crown = MakeScaled("Crown", PrimitiveType.Cylinder, new Vector3(1.2f, 2f, 1.2f), LoadMat("M_Foliage"));
            crown.transform.SetParent(pine.transform, false);
            crown.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            Object.DestroyImmediate(crown.GetComponent<Collider>());
            SavePrefab(pine, PrefabNature + "/PF_Pine.prefab");

            // Atmosphere system prefab
            var atm = new GameObject("PF_Atmosphere");
            atm.AddComponent<DarkFantasyAtmosphere>();
            SavePrefab(atm, PrefabSystems + "/PF_Atmosphere.prefab");

            // Moon disc
            var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "PF_Moon";
            Object.DestroyImmediate(moon.GetComponent<Collider>());
            moon.GetComponent<MeshRenderer>().sharedMaterial = LoadMat("M_Moon");
            moon.AddComponent<MoonDisc>();
            SavePrefab(moon, PrefabSystems + "/PF_Moon.prefab");

            // Player prefab
            CreatePlayerPrefab();
        }

        static void CreatePlayerPrefab()
        {
            var player = new GameObject("PF_Player");
            player.tag = "Player";
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            var camPivot = new GameObject("CameraPivot");
            camPivot.transform.SetParent(player.transform, false);
            camPivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(camPivot.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

            var fps = player.AddComponent<FirstPersonController>();
            fps.cameraPivot = camPivot.transform;

            // 3D sword removed — use Witcher/Art/Install Sprite Sword (SpriteHeldWeapon).
            var swordGo = new GameObject("View_SpriteSword");
            swordGo.transform.SetParent(camGo.transform, false);
            swordGo.AddComponent<SpriteHeldWeapon>();

            SavePrefab(player, PrefabPlayer + "/PF_Player.prefab");
            Object.DestroyImmediate(player);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        static bool PrefabsExist() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCastle + "/PF_Castle.prefab") != null;

        static GameObject MakeBox(string name, Vector3 scale, Material mat) =>
            MakeScaled(name, PrimitiveType.Cube, scale, mat);

        static GameObject MakeScaled(string name, PrimitiveType type, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject PlaceModule(Transform parent, string prefabPath, Vector3 localPos)
        {
            var prefab = LoadPrefab(prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPos;
            return instance;
        }

        static void InstantiateChild(string prefabPath, Transform parent, Vector3 localPos)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(prefabPath));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
        }

        static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new System.InvalidOperationException("Missing prefab: " + path);
            return prefab;
        }

        static Material LoadMat(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{MatPath}/{name}.mat");

        static T LoadAsset<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
#endif
