#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Witcher.Core;
using Witcher.Player;
using Witcher.Visuals;

namespace Witcher.EditorTools
{
    public static class LayeredBackdropBuilder
    {
        const string SpritePath = "Assets/Witcher/Art/Sprites";
        const string ResPath = "Assets/Witcher/Resources/Pitch";

        [MenuItem("Witcher/Pitch/4. Build Layered Backdrop (like reference photo)")]
        public static void BuildLayers()
        {
            SyncResources();
            ImportTextures();

            var player = Object.FindAnyObjectByType<FirstPersonController>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Pitch", "Нет игрока. Сначала Pitch → 2.", "OK");
                return;
            }

            player.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
            player.SnapToGround();

            var cam = player.GetComponentInChildren<Camera>();
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Pitch", "Нет камеры.", "OK");
                return;
            }

            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.05f, 0.1f);
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            if (player.cameraPivot != null)
                player.cameraPivot.localRotation = Quaternion.identity;

            var pixel = cam.GetComponent<SimplePixelCamera>();
            if (pixel != null)
                pixel.enabled = false;

            var env = GameObject.Find(SceneRoots.Environment);
            if (env != null)
            {
                SetActive(env.transform, SceneRoots.Castle, false);
                SetActive(env.transform, SceneRoots.Forest, false);
                var old = env.transform.Find("LayeredBackdrop");
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }

            foreach (var sky in Object.FindObjectsByType<SkyDome>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sky.gameObject.SetActive(false);
            foreach (var moon in Object.FindObjectsByType<MoonDisc>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                moon.gameObject.SetActive(false);
            foreach (var atm in Object.FindObjectsByType<DarkFantasyAtmosphere>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                atm.enabled = false;

            RenderSettings.fog = false;
            RenderSettings.ambientLight = new Color(0.6f, 0.5f, 0.55f);

            // Clear quality — no mushy 0.35 scale, almost no dither
            SetRenderScale(1f);
            SoftenRetroOff();

            var rig = cam.GetComponent<PitchPaperRig>();
            if (rig == null)
                rig = cam.gameObject.AddComponent<PitchPaperRig>();
            rig.EnsureBuilt();

            // UI sword comes from Resources/Pitch — do NOT install procedural 3D stub
            foreach (var sword in Object.FindObjectsByType<SpriteHeldWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sword.gameObject.SetActive(false);

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = cam.gameObject;

            EditorUtility.DisplayDialog(
                "Чёткий кадр",
                "Полный экран = T_World_Vista (без дыр и летающих ёлок).\n" +
                "Render Scale = 1.0, dither почти выключен.\n\n" +
                "Play ▶ — должно быть как превью.\nWASD — лёгкий параллакс + меч.",
                "OK");
        }

        static void SoftenRetroOff()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer == null) return;
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is not RetroLookFeature retro) continue;
                retro.settings.pixelSize = 1f;
                retro.settings.colorSteps = 32f;
                retro.settings.ditherStrength = 0.02f;
                retro.settings.contrast = 1f;
                retro.settings.saturation = 1f;
                retro.settings.vignette = 0.15f;
                EditorUtility.SetDirty(retro);
            }
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        static void SetRenderScale(float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (asset == null) return;
            asset.renderScale = scale;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        static void SyncResources()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Witcher/Resources"))
                AssetDatabase.CreateFolder("Assets/Witcher", "Resources");
            if (!AssetDatabase.IsValidFolder(ResPath))
                AssetDatabase.CreateFolder("Assets/Witcher/Resources", "Pitch");

            foreach (var f in new[]
                     {
                         "T_World_Vista.png", "T_L1_SkyMountains.png", "T_L2_CastleMid.png",
                         "T_L2_DenseForest.png", "T_Forest_Mid.png", "T_L3_NearPines.png",
                         "T_Sword_Pixel.png", "T_Sky_NightMoon.png"
                     })
            {
                var src = $"{SpritePath}/{f}";
                var dst = $"{ResPath}/{f}";
                if (AssetDatabase.LoadAssetAtPath<Object>(src) == null) continue;
                if (AssetDatabase.LoadAssetAtPath<Object>(dst) != null)
                    AssetDatabase.DeleteAsset(dst);
                AssetDatabase.CopyAsset(src, dst);
            }
            AssetDatabase.Refresh();
        }

        static void ImportTextures()
        {
            foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { ResPath, SpritePath }))
            {
                var p = AssetDatabase.GUIDToAssetPath(path);
                if (!p.Contains("T_World") && !p.Contains("T_L") && !p.Contains("T_Sword")) continue;
                var importer = AssetImporter.GetAtPath(p) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Default;
                importer.isReadable = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = p.Contains("L3") || p.Contains("Sword") || p.Contains("L2");
                importer.SaveAndReimport();
            }
        }

        static void SetActive(Transform parent, string name, bool active)
        {
            var t = parent.Find(name);
            if (t != null) t.gameObject.SetActive(active);
        }
    }
}
#endif
