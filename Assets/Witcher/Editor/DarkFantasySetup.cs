#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Witcher.Player;
using Witcher.Visuals;

namespace Witcher.EditorTools
{
    /// <summary>
    /// Menu helpers so you can stay in the Unity editor while scripts build the mood.
    /// </summary>
    public static class DarkFantasySetup
    {
        const string ScenePath = "Assets/Witcher/Scenes/DarkFantasy_Demo.unity";
        const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

        [MenuItem("Witcher/1. Add Retro Look To URP Renderer")]
        public static void AddRetroLookFeature()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRendererPath);
            if (renderer == null)
            {
                EditorUtility.DisplayDialog("Witcher", "Не найден Assets/Settings/PC_Renderer.asset", "OK");
                return;
            }

            foreach (var existing in renderer.rendererFeatures)
            {
                if (existing is RetroLookFeature)
                {
                    EditorUtility.DisplayDialog("Witcher", "Retro Look уже добавлен в PC_Renderer.", "OK");
                    Selection.activeObject = renderer;
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance<RetroLookFeature>();
            feature.name = "Retro Look";
            AssetDatabase.AddObjectToAsset(feature, renderer);

            var so = new SerializedObject(renderer);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = feature.GetInstanceID();
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            Selection.activeObject = renderer;
            Debug.Log("[Witcher] Retro Look feature added to PC_Renderer.");
        }

        [MenuItem("Witcher/2. Build Dark Fantasy Demo Scene")]
        public static void BuildDemoScene()
        {
            AddRetroLookFeature();

            Directory.CreateDirectory("Assets/Witcher/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Lighting root ---
            var sunGo = new GameObject("Sun_Or_Moon");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;

            var atmosphereGo = new GameObject("DarkFantasyAtmosphere");
            var atmosphere = atmosphereGo.AddComponent<DarkFantasyAtmosphere>();
            atmosphere.mood = DarkFantasyMood.CastleDusk;
            atmosphere.sunOrMoon = sun;

            // --- Ground & path ---
            var ground = CreatePrimitive(PrimitiveType.Plane, "Ground", new Vector3(0, 0, 0), new Vector3(8, 1, 8),
                new Color(0.12f, 0.18f, 0.12f));
            var path = CreatePrimitive(PrimitiveType.Cube, "Path", new Vector3(0, 0.02f, 12), new Vector3(3.2f, 0.05f, 28),
                new Color(0.28f, 0.24f, 0.2f));

            // --- Forest silhouettes ---
            var forest = new GameObject("Forest");
            Random.InitState(42);
            for (int i = 0; i < 40; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float z = 2f + (i / 2) * 1.4f;
                float x = side * (2.4f + Random.Range(0f, 6f));
                CreatePine(forest.transform, new Vector3(x, 0f, z), Random.Range(2.2f, 5.5f));
            }

            // --- Castle blockout (far hill) ---
            var castleRoot = new GameObject("Castle_Blockout");
            castleRoot.transform.position = new Vector3(0f, 0f, 42f);
            BuildCastle(castleRoot.transform);

            // --- Hills ---
            CreatePrimitive(PrimitiveType.Cube, "Hill", new Vector3(0, -2f, 40f), new Vector3(40, 8, 18),
                new Color(0.18f, 0.16f, 0.14f)).transform.SetParent(castleRoot.transform, true);

            // --- Player ---
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 1.0f, 0f);
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
            var additional = camGo.AddComponent<UniversalAdditionalCameraData>();
            additional.renderPostProcessing = true;

            var fps = player.AddComponent<FirstPersonController>();
            fps.cameraPivot = camPivot.transform;

            // Sword viewmodel (simple primitives)
            var weapon = new GameObject("SilverSword");
            weapon.transform.SetParent(camGo.transform, false);
            var hilt = CreatePrimitive(PrimitiveType.Cylinder, "Hilt", Vector3.zero, new Vector3(0.04f, 0.08f, 0.04f),
                new Color(0.25f, 0.15f, 0.1f));
            hilt.transform.SetParent(weapon.transform, false);
            hilt.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            Object.DestroyImmediate(hilt.GetComponent<Collider>());

            var guard = CreatePrimitive(PrimitiveType.Cube, "Guard", Vector3.zero, new Vector3(0.18f, 0.03f, 0.04f),
                new Color(0.7f, 0.7f, 0.75f));
            guard.transform.SetParent(weapon.transform, false);
            guard.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            Object.DestroyImmediate(guard.GetComponent<Collider>());

            var blade = CreatePrimitive(PrimitiveType.Cube, "Blade", Vector3.zero, new Vector3(0.035f, 0.55f, 0.012f),
                new Color(0.75f, 0.95f, 1f));
            blade.transform.SetParent(weapon.transform, false);
            blade.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            Object.DestroyImmediate(blade.GetComponent<Collider>());
            var bladeMat = blade.GetComponent<MeshRenderer>().sharedMaterial;
            // URP Lit: enable emission via material property if present
            if (bladeMat != null && bladeMat.HasProperty("_EmissionColor"))
            {
                bladeMat.EnableKeyword("_EMISSION");
                bladeMat.SetColor("_EmissionColor", new Color(0.2f, 1.2f, 1.5f));
            }

            var glowGo = new GameObject("BladeGlow");
            glowGo.transform.SetParent(blade.transform, false);
            glowGo.transform.localPosition = Vector3.zero;
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;

            var held = weapon.AddComponent<HeldWeapon>();
            held.bladeGlow = glow;
            held.glowColor = new Color(0.35f, 0.95f, 1f);

            atmosphere.mainCamera = cam;
            atmosphere.TryApply();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);
            EditorUtility.DisplayDialog(
                "Witcher — Demo ready",
                "Сцена сохранена:\n" + ScenePath +
                "\n\nPlay (▶):\n• WASD — ходьба\n• Мышь — взгляд\n• Shift — бег\n• Esc — курсор\n\n" +
                "Смени mood на DarkFantasyAtmosphere (Castle Dusk / Manor Night).\n" +
                "Силу пикселей крути в PC_Renderer → Retro Look.",
                "Круто");
        }

        [MenuItem("Witcher/3. Apply Mood/Castle Dusk")]
        public static void MoodDusk()
        {
            var a = Object.FindFirstObjectByType<DarkFantasyAtmosphere>();
            if (a == null) { EditorUtility.DisplayDialog("Witcher", "Нет DarkFantasyAtmosphere в сцене.", "OK"); return; }
            a.mood = DarkFantasyMood.CastleDusk;
            a.TryApply();
        }

        [MenuItem("Witcher/3. Apply Mood/Manor Night")]
        public static void MoodNight()
        {
            var a = Object.FindFirstObjectByType<DarkFantasyAtmosphere>();
            if (a == null) { EditorUtility.DisplayDialog("Witcher", "Нет DarkFantasyAtmosphere в сцене.", "OK"); return; }
            a.mood = DarkFantasyMood.ManorNight;
            a.TryApply();
        }

        static GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            renderer.sharedMaterial = mat;
            return go;
        }

        static void CreatePine(Transform parent, Vector3 pos, float height)
        {
            var root = new GameObject("Pine");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            var trunk = CreatePrimitive(PrimitiveType.Cylinder, "Trunk", pos, new Vector3(0.18f, height * 0.25f, 0.18f),
                new Color(0.12f, 0.08f, 0.05f));
            trunk.transform.SetParent(root.transform, true);
            trunk.transform.position = pos + Vector3.up * (height * 0.25f);
            Object.DestroyImmediate(trunk.GetComponent<Collider>());

            var crown = CreatePrimitive(PrimitiveType.Cylinder, "Crown", pos, new Vector3(1.1f, height * 0.45f, 1.1f),
                new Color(0.05f, 0.12f, 0.07f));
            crown.transform.SetParent(root.transform, true);
            crown.transform.position = pos + Vector3.up * (height * 0.7f);
            // Fake cone by stretching — good enough for silhouette
            Object.DestroyImmediate(crown.GetComponent<Collider>());
        }

        static void BuildCastle(Transform root)
        {
            var stone = new Color(0.35f, 0.32f, 0.3f);
            var roof = new Color(0.45f, 0.12f, 0.1f);

            CreateChildCube(root, "Keep", new Vector3(0, 6f, 0), new Vector3(10, 12, 8), stone);
            CreateChildCube(root, "WingL", new Vector3(-8, 4f, 1), new Vector3(6, 8, 6), stone);
            CreateChildCube(root, "WingR", new Vector3(8, 4f, 1), new Vector3(6, 8, 6), stone);
            CreateChildCube(root, "TowerL", new Vector3(-11, 8f, -2), new Vector3(3.2f, 16, 3.2f), stone);
            CreateChildCube(root, "TowerR", new Vector3(11, 8f, -2), new Vector3(3.2f, 16, 3.2f), stone);
            CreateChildCube(root, "TowerMid", new Vector3(0, 11f, -3), new Vector3(3.5f, 18, 3.5f), stone);

            CreateChildCube(root, "RoofL", new Vector3(-11, 17f, -2), new Vector3(3.8f, 2.5f, 3.8f), roof);
            CreateChildCube(root, "RoofR", new Vector3(11, 17f, -2), new Vector3(3.8f, 2.5f, 3.8f), roof);
            CreateChildCube(root, "RoofMid", new Vector3(0, 21f, -3), new Vector3(4.2f, 3f, 4.2f), roof);

            CreateChildCube(root, "Gate", new Vector3(0, 2.5f, -4.2f), new Vector3(3f, 5f, 1f), new Color(0.08f, 0.06f, 0.05f));
            CreateChildCube(root, "Stairs", new Vector3(0, 0.8f, -7f), new Vector3(5f, 1.6f, 6f), new Color(0.3f, 0.28f, 0.26f));
        }

        static void CreateChildCube(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = CreatePrimitive(PrimitiveType.Cube, name, parent.position + localPos, scale, color);
            go.transform.SetParent(parent, true);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
#endif
