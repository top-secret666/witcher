#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Witcher.Core;
using Witcher.Data;
using Witcher.Visuals;

namespace Witcher.EditorTools
{
    /// <summary>
    /// Wires the player's day-style textures into materials, atmosphere, and sky.
    /// </summary>
    public static class ApplyVideoArtStyle
    {
        const string TexPath = "Assets/Witcher/Art/Textures";
        const string MatPath = "Assets/Witcher/Art/Materials";
        const string DataPath = "Assets/Witcher/Data/Atmosphere";
        const string PrefabSystems = "Assets/Witcher/Prefabs/Systems";

        [MenuItem("Witcher/Art/Apply Day Video Style (textures + sky + light)")]
        public static void ApplyAll()
        {
            EnsureFolders();
            if (LoadTex("T_Stone_Cream") == null && LoadTex("T_Sky_Day") == null)
            {
                EditorUtility.DisplayDialog("Witcher", "В Art/Textures нет T_Stone_Cream / T_Sky_Day.", "OK");
                return;
            }

            ConfigureTextureImports();
            AssignTexturesToMaterials();
            CreateBrightDayProfile();
            EnsureSkyPrefab();
            ApplyToOpenScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Witcher — Day Style",
                "Готово:\n" +
                "• текстуры на материалах\n" +
                "• Atmosphere_BrightDay\n" +
                "• Sky Dome в сцене (если открыта)\n\n" +
                "Play ▶ и скинь скрин Game view.",
                "OK");
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Witcher/Art/Textures"))
                AssetDatabase.CreateFolder("Assets/Witcher/Art", "Textures");
            if (!AssetDatabase.IsValidFolder(MatPath))
                AssetDatabase.CreateFolder("Assets/Witcher/Art", "Materials");
            if (!AssetDatabase.IsValidFolder(DataPath))
                AssetDatabase.CreateFolder("Assets/Witcher/Data", "Atmosphere");
            if (!AssetDatabase.IsValidFolder(PrefabSystems))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Witcher/Prefabs"))
                    AssetDatabase.CreateFolder("Assets/Witcher", "Prefabs");
                AssetDatabase.CreateFolder("Assets/Witcher/Prefabs", "Systems");
            }
        }

        static Texture2D LoadTex(string baseName)
        {
            var png = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexPath}/{baseName}.png");
            if (png != null) return png;
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexPath}/{baseName}.jpg");
        }

        static void ConfigureTextureImports()
        {
            string[] names =
            {
                "T_Sky_Day", "T_Stone_Cream", "T_Roof_Red", "T_Cobble", "T_Hedge", "T_Carpet_Red"
            };

            foreach (var name in names)
            {
                foreach (var ext in new[] { ".png", ".jpg" })
                {
                    var path = $"{TexPath}/{name}{ext}";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.mipmapEnabled = true;
                    importer.filterMode = name == "T_Sky_Day" ? FilterMode.Bilinear : FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    if (name == "T_Sky_Day")
                        importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }
            }
        }

        static Material EnsureLit(string matName, Color fallback)
        {
            var path = $"{MatPath}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(shader != null ? shader : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", fallback);
            mat.color = fallback;
            return mat;
        }

        static Material EnsureUnlit(string matName)
        {
            var path = $"{MatPath}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Texture")
                         ?? Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            return mat;
        }

        static void SetBaseMap(Material mat, Texture2D tex, Vector2 tiling)
        {
            if (mat == null || tex == null) return;

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", tiling);
            }

            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tex);
                mat.SetTextureScale("_MainTex", tiling);
            }

            // Keep albedo near white so texture color reads
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            mat.color = Color.white;
            EditorUtility.SetDirty(mat);
        }

        static void AssignTexturesToMaterials()
        {
            SetBaseMap(EnsureLit("M_Stone", new Color(0.9f, 0.85f, 0.75f)), LoadTex("T_Stone_Cream"), new Vector2(2f, 2f));
            SetBaseMap(EnsureLit("M_Roof", new Color(0.6f, 0.15f, 0.15f)), LoadTex("T_Roof_Red"), new Vector2(2f, 2f));
            SetBaseMap(EnsureLit("M_Path", new Color(0.5f, 0.4f, 0.35f)), LoadTex("T_Cobble"), new Vector2(4f, 8f));
            SetBaseMap(EnsureLit("M_Ground", new Color(0.35f, 0.45f, 0.3f)), LoadTex("T_Cobble"), new Vector2(8f, 8f));
            SetBaseMap(EnsureLit("M_Foliage", new Color(0.2f, 0.5f, 0.2f)), LoadTex("T_Hedge"), new Vector2(2f, 2f));
            SetBaseMap(EnsureLit("M_Carpet", new Color(0.6f, 0.1f, 0.1f)), LoadTex("T_Carpet_Red"), new Vector2(1f, 3f));

            var skyMat = EnsureUnlit("M_Sky");
            var skyTex = LoadTex("T_Sky_Day");
            SetBaseMap(skyMat, skyTex, Vector2.one);
            // Unlit sphere: render inside
            skyMat.SetInt("_Cull", (int)CullMode.Front);
            if (skyMat.HasProperty("_Surface"))
                skyMat.SetFloat("_Surface", 0);
            EditorUtility.SetDirty(skyMat);
        }

        static void CreateBrightDayProfile()
        {
            var path = $"{DataPath}/Atmosphere_BrightDay.asset";
            var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<AtmosphereProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.displayName = "Bright Day (Video)";
            profile.fog = true;
            profile.fogMode = FogMode.ExponentialSquared;
            profile.fogColor = new Color(0.65f, 0.78f, 0.95f);
            profile.fogDensity = 0.006f;
            profile.ambientLight = new Color(0.55f, 0.65f, 0.85f);
            profile.shadowColor = new Color(0.25f, 0.28f, 0.45f); // cool purple-ish shadows like the video
            profile.lightColor = new Color(1f, 0.95f, 0.85f);
            profile.lightIntensity = 1.6f;
            profile.lightEuler = new Vector3(42f, -25f, 0f);
            profile.cameraBackground = new Color(0.45f, 0.7f, 0.95f);
            EditorUtility.SetDirty(profile);
        }

        static void EnsureSkyPrefab()
        {
            var path = $"{PrefabSystems}/PF_SkyDome.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "PF_SkyDome";
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            var renderer = sphere.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatPath}/M_Sky.mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            sphere.AddComponent<SkyDome>();
            PrefabUtility.SaveAsPrefabAsset(sphere, path);
            Object.DestroyImmediate(sphere);
        }

        static void ApplyToOpenScene()
        {
            // Atmosphere
            var atmosphere = Object.FindAnyObjectByType<DarkFantasyAtmosphere>();
            var day = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>($"{DataPath}/Atmosphere_BrightDay.asset");
            if (atmosphere != null && day != null)
            {
                atmosphere.profile = day;
                if (atmosphere.sunOrMoon == null)
                    atmosphere.sunOrMoon = Object.FindAnyObjectByType<Light>();
                atmosphere.Apply();
                EditorUtility.SetDirty(atmosphere);
            }

            // Sky dome under _Systems/Lighting if possible
            var skyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabSystems}/PF_SkyDome.prefab");
            if (skyPrefab != null && Object.FindAnyObjectByType<SkyDome>() == null)
            {
                Transform parent = null;
                var systems = GameObject.Find(SceneRoots.Systems);
                if (systems != null)
                {
                    var lighting = systems.transform.Find(SceneRoots.Lighting);
                    parent = lighting != null ? lighting : systems.transform;
                }

                var sky = (GameObject)PrefabUtility.InstantiatePrefab(skyPrefab);
                sky.name = "SkyDome";
                if (parent != null)
                    sky.transform.SetParent(parent, false);

                var cam = Camera.main;
                var dome = sky.GetComponent<SkyDome>();
                if (dome != null && cam != null)
                    dome.follow = cam.transform;

                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.45f, 0.7f, 0.95f);
                }
            }

            // Hide moon in daytime if present
            var moon = Object.FindAnyObjectByType<MoonDisc>();
            if (moon != null)
                moon.gameObject.SetActive(false);

            EditorSceneManager.MarkAllScenesDirty();
        }
    }
}
#endif
