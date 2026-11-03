#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Witcher.Core;
using Witcher.Data;
using Witcher.Player;
using Witcher.Visuals;

namespace Witcher.EditorTools
{
    /// <summary>
    /// One-click Witcher/Berserk pitch scene for the Minsk startup demo GIF.
    /// </summary>
    public static class PitchPresentationSetup
    {
        const string TexPath = "Assets/Witcher/Art/Textures";
        const string MatPath = "Assets/Witcher/Art/Materials";
        const string SpritePath = "Assets/Witcher/Art/Sprites";
        const string DuskPath = "Assets/Witcher/Data/Atmosphere/Atmosphere_CastleDusk.asset";

        [MenuItem("Witcher/Pitch/1. Apply Witcher Dark Fantasy Look")]
        public static void ApplyWitcherLook()
        {
            AssetDatabase.Refresh();
            TuneCastleDusk();
            AssignWitcherMaterials();
            HybridRetroSetup.MakeTexturesCrunchy();
            HybridRetroSetup.ApplyPixelRenderScale();
            ApplySkyDusk();
            ApplyDuskToScene();
            SoftenRetroForDusk();

            EditorUtility.DisplayDialog(
                "Witcher Pitch",
                "Dark fantasy look применён (dusk sky, grey stone, dark path, fog).\n\nДальше:\nWitcher → Pitch → 2. Build Pitch Scene (sword)\nили 3. Build Pitch Scene (Roach)",
                "OK");
        }

        [MenuItem("Witcher/Pitch/2. Build Pitch Scene (Geralt sword)")]
        public static void BuildPitchSword()
        {
            EnsureSceneReady();
            ApplyWitcherLookSilent();
            LayeredBackdropBuilder.BuildLayers();
        }

        static void ApplyWitcherLookSilent()
        {
            AssetDatabase.Refresh();
            TuneCastleDusk();
            AssignWitcherMaterials();
            HybridRetroSetup.MakeTexturesCrunchy();
            HybridRetroSetup.ApplyPixelRenderScale();
            ApplySkyDusk();
            ApplyDuskToScene();
            SoftenRetroForDusk();
        }

        [MenuItem("Witcher/Pitch/3. Build Pitch Scene (Roach horse)")]
        public static void BuildPitchRoach()
        {
            EnsureSceneReady();
            HybridRetroSetup.ConvertForestToBillboards();
            InstallRoachViewmodel();
            ApplyDuskToScene();
            FrameCameraForPitch();
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            EditorUtility.DisplayDialog(
                "Pitch ready — Roach",
                "Сцена для гифки:\n• dusk небо\n• billboard-сосны\n• Плотва (2D) от 1-го лица\n\nPlay ▶ → скачи к замку → запись GIF.",
                "OK");
        }

        [MenuItem("Witcher/Pitch/Apply Full Pitch Bundle (sword)")]
        public static void FullBundleSword()
        {
            ApplyWitcherLook();
            BuildPitchSword();
        }

        static void EnsureSceneReady()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path) || !scene.path.Contains("DarkFantasy"))
            {
                var demo = "Assets/Witcher/Scenes/DarkFantasy_Demo.unity";
                if (System.IO.File.Exists(demo.Replace("Assets/", Application.dataPath + "/").Replace('/', System.IO.Path.DirectorySeparatorChar))
                    || AssetDatabase.LoadAssetAtPath<Object>(demo) != null)
                {
                    EditorSceneManager.OpenScene(demo);
                }
                else
                {
                    ArchitectureBuilder.RebuildSceneFromPrefabs();
                }
            }
        }

        static void TuneCastleDusk()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(DuskPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<AtmosphereProfile>();
                AssetDatabase.CreateAsset(profile, DuskPath);
            }

            profile.displayName = "Witcher Blood Dusk";
            profile.fog = true;
            profile.fogMode = FogMode.ExponentialSquared;
            profile.fogColor = new Color(0.35f, 0.12f, 0.22f);
            profile.fogDensity = 0.016f;
            profile.ambientLight = new Color(0.28f, 0.14f, 0.22f);
            profile.shadowColor = new Color(0.08f, 0.04f, 0.12f);
            profile.lightColor = new Color(1f, 0.35f, 0.22f);
            profile.lightIntensity = 1.55f;
            profile.lightEuler = new Vector3(8f, -50f, 0f);
            profile.cameraBackground = new Color(0.18f, 0.08f, 0.16f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        static void AssignWitcherMaterials()
        {
            SetMatMap("M_Stone", $"{TexPath}/T_Stone_Grey.png", new Vector2(2.5f, 2.5f));
            SetMatMap("M_Path", $"{TexPath}/T_Path_Dark.png", new Vector2(3f, 6f));
            SetMatMap("M_Ground", $"{TexPath}/T_Path_Dark.png", new Vector2(6f, 6f));
            SetMatMap("M_Roof", $"{TexPath}/T_Roof_Red.png", new Vector2(2f, 2f));
            SetMatMap("M_Foliage", $"{TexPath}/T_Hedge.png", new Vector2(2f, 2f));

            var skyMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatPath}/M_Sky.mat");
            var skyTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexPath}/T_Sky_Ash.png");
            if (skyMat != null && skyTex != null)
            {
                if (skyMat.HasProperty("_BaseMap"))
                {
                    skyMat.SetTexture("_BaseMap", skyTex);
                    skyMat.SetColor("_BaseColor", Color.white);
                }
                if (skyMat.HasProperty("_MainTex"))
                    skyMat.SetTexture("_MainTex", skyTex);
                skyMat.color = Color.white;
                EditorUtility.SetDirty(skyMat);
            }
        }

        static void SetMatMap(string matName, string texPath, Vector2 tiling)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatPath}/{matName}.mat");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (mat == null || tex == null) return;
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", tiling);
                mat.SetColor("_BaseColor", Color.white);
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tex);
                mat.SetTextureScale("_MainTex", tiling);
            }
            mat.color = Color.white;
            EditorUtility.SetDirty(mat);
        }

        static void ApplySkyDusk()
        {
            ConfigureSpriteOrTex($"{TexPath}/T_Sky_Ash.png", 1024, false);
            ConfigureSpriteOrTex($"{SpritePath}/T_Sword_Pixel.png", 512, true);
            ConfigureSpriteOrTex($"{TexPath}/T_Stone_Grey.png", 128, false);
            ConfigureSpriteOrTex($"{TexPath}/T_Path_Dark.png", 128, false);
        }

        static void ConfigureSpriteOrTex(string path, int max, bool sprite)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                importer.SetTextureSettings(settings);
                importer.spritePivot = new Vector2(0.5f, 0f);
            }
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.anisoLevel = 0;
            importer.maxTextureSize = max;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        public static void ApplyDuskPublic() => ApplyDuskToScene();

        static void ApplyDuskToScene()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(DuskPath);
            var atmosphere = Object.FindAnyObjectByType<DarkFantasyAtmosphere>();
            if (atmosphere != null && profile != null)
            {
                atmosphere.profile = profile;
                if (atmosphere.sunOrMoon == null)
                    atmosphere.sunOrMoon = Object.FindAnyObjectByType<Light>();
                atmosphere.Apply();
                EditorUtility.SetDirty(atmosphere);
            }

            var moon = Object.FindAnyObjectByType<MoonDisc>();
            if (moon != null)
                moon.gameObject.SetActive(false);

            var sky = Object.FindAnyObjectByType<SkyDome>();
            if (sky != null)
            {
                var r = sky.GetComponent<MeshRenderer>();
                if (r != null)
                    r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatPath}/M_Sky.mat");
            }
        }

        static void SoftenRetroForDusk()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer == null) return;
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is not RetroLookFeature retro) continue;
                // Soft Angel's Egg / Berserk dusk — avoid checkerboard sky
                retro.settings.pixelSize = 1.6f;
                retro.settings.colorSteps = 18f;
                retro.settings.ditherStrength = 0.1f;
                retro.settings.contrast = 1.06f;
                retro.settings.saturation = 0.78f;
                retro.settings.vignette = 0.38f;
                retro.settings.tint = new Color(0.94f, 0.9f, 0.92f, 1f);
                EditorUtility.SetDirty(retro);
            }
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        static void FrameCameraForPitch()
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
            player.transform.position = new Vector3(0f, 0.15f, 4f);
            player.transform.rotation = Quaternion.identity;
            player.SnapToGround();

            var castle = GameObject.Find($"{SceneRoots.Environment}/{SceneRoots.Castle}")
                         ?? GameObject.Find(SceneRoots.Castle);
            if (castle != null)
            {
                var look = castle.transform.position + Vector3.up * 6f;
                var dir = look - player.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    player.transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        static void InstallRoachViewmodel()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritePath}/T_Roach.png");
            if (sprite == null)
            {
                EditorUtility.DisplayDialog("Witcher", "Нет Art/Sprites/T_Roach.png", "OK");
                return;
            }

            var scenePlayer = GameObject.Find($"{SceneRoots.Gameplay}/{SceneRoots.Player}")
                              ?? Object.FindAnyObjectByType<FirstPersonController>()?.gameObject;
            if (scenePlayer == null) return;

            var cam = scenePlayer.GetComponentInChildren<Camera>();
            if (cam == null) return;

            foreach (var held in scenePlayer.GetComponentsInChildren<HeldWeapon>(true))
                Object.DestroyImmediate(held.gameObject);
            foreach (var sw in scenePlayer.GetComponentsInChildren<SpriteHeldWeapon>(true))
                Object.DestroyImmediate(sw.gameObject);

            var go = new GameObject("View_Roach");
            go.transform.SetParent(cam.transform, false);
            var view = go.AddComponent<SpriteHeldWeapon>();
            view.localPosition = new Vector3(0f, -0.55f, 0.85f);
            view.localEuler = new Vector3(5f, 0f, 0f);
            view.localScale = 1.15f;
            view.swayAmount = 0.02f;
            view.swaySpeed = 2.2f;

            var child = new GameObject("RoachSprite");
            child.transform.SetParent(go.transform, false);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 100;
            view.spriteRenderer = sr;
            view.weaponSprite = sprite;
        }
    }
}
#endif
