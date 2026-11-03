#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Witcher.Core;
using Witcher.Data;
using Witcher.Player;
using Witcher.Visuals;

namespace Witcher.EditorTools
{
    /// <summary>
    /// Hybrid retro FPS: billboard trees, sprite sword, crunchy textures, URP render scale.
    /// </summary>
    public static class HybridRetroSetup
    {
        const string TexPath = "Assets/Witcher/Art/Textures";
        const string SpritePath = "Assets/Witcher/Art/Sprites";
        const string PrefabNature = "Assets/Witcher/Prefabs/Environment/Nature";
        const string PrefabPlayer = "Assets/Witcher/Prefabs/Player/PF_Player.prefab";
        const string PcRpAsset = "Assets/Settings/PC_RPAsset.asset";
        const string BrightDay = "Assets/Witcher/Data/Atmosphere/Atmosphere_BrightDay.asset";

        static readonly string[] WorldTextureNames =
        {
            "T_Stone_Cream", "T_Stone_Grey", "T_Roof_Red", "T_Cobble", "T_Path_Dark",
            "T_Hedge", "T_Carpet_Red", "T_Ground", "T_Sky_Day", "T_Sky_Dusk"
        };

        [MenuItem("Witcher/Art/Make Textures Crunchy")]
        public static void MakeTexturesCrunchy()
        {
            int count = 0;
            foreach (var name in WorldTextureNames)
            {
                foreach (var ext in new[] { ".png", ".jpg" })
                {
                    var path = $"{TexPath}/{name}{ext}";
                    int max = name.StartsWith("T_Sky") ? 1024 : 128;
                    if (ConfigureCrunchyImport(path, max))
                        count++;
                }
            }

            // Also crunch any sprites folder textures
            if (AssetDatabase.IsValidFolder(SpritePath))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpritePath }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (ConfigureCrunchyImport(path, 256, asSprite: true))
                        count++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Witcher", $"Crunchy: обновлено импортов ≈ {count}\nFilter=Point, maxSize≤128 (sky 512).", "OK");
        }

        [MenuItem("Witcher/Art/Apply Pixel Render Scale")]
        public static void ApplyPixelRenderScale()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcRpAsset);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Witcher", "Не найден PC_RPAsset.", "OK");
                return;
            }

            var so = new SerializedObject(asset);
            var prop = so.FindProperty("m_RenderScale");
            if (prop == null)
            {
                EditorUtility.DisplayDialog("Witcher", "Не найдено поле m_RenderScale.", "OK");
                return;
            }

            prop.floatValue = 0.35f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            // Soften MSAA for chunkier pixels
            var msaa = so.FindProperty("m_MSAA");
            if (msaa != null)
            {
                msaa.intValue = 1; // Disabled
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }

            Selection.activeObject = asset;
            EditorUtility.DisplayDialog("Witcher", "URP Render Scale = 0.35 (пиксельные края геометрии).\nMSAA выключен.", "OK");
        }

        [MenuItem("Witcher/Art/Convert Forest To Billboards")]
        public static void ConvertForestToBillboards()
        {
            EnsureSpriteFolder();
            var treeSprite = LoadOrCreateTreeSprite();

            var forest = GameObject.Find($"{SceneRoots.Environment}/{SceneRoots.Forest}")
                         ?? GameObject.Find(SceneRoots.Forest);
            if (forest == null)
            {
                EditorUtility.DisplayDialog("Witcher", "В сцене нет _Environment/Forest. Открой DarkFantasy_Demo.", "OK");
                return;
            }

            var toReplace = new List<Transform>();
            foreach (Transform child in forest.transform)
            {
                if (child.name.Contains("Pine") || child.name.Contains("PF_Pine") || child.GetComponentInChildren<MeshFilter>() != null)
                    toReplace.Add(child);
            }

            if (toReplace.Count == 0)
            {
                EditorUtility.DisplayDialog("Witcher", "В Forest нет деревьев для замены.", "OK");
                return;
            }

            int n = 0;
            foreach (var old in toReplace)
            {
                Vector3 pos = old.position;
                float heightScale = Mathf.Clamp(old.lossyScale.y, 0.7f, 2.2f);
                Object.DestroyImmediate(old.gameObject);

                var billboard = CreateTreeBillboard($"PineBillboard_{n}", treeSprite, heightScale);
                billboard.transform.SetParent(forest.transform, false);
                billboard.transform.position = pos;
                n++;
            }

            if (!AssetDatabase.IsValidFolder(PrefabNature))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Witcher/Prefabs/Environment"))
                    AssetDatabase.CreateFolder("Assets/Witcher/Prefabs", "Environment");
                AssetDatabase.CreateFolder("Assets/Witcher/Prefabs/Environment", "Nature");
            }

            var sample = CreateTreeBillboard("PF_PineBillboard", treeSprite, 1f);
            PrefabUtility.SaveAsPrefabAsset(sample, $"{PrefabNature}/PF_PineBillboard.prefab");
            Object.DestroyImmediate(sample);

            EditorSceneManager.MarkAllScenesDirty();
            EditorUtility.DisplayDialog("Witcher", $"Лес → billboards: {n} шт.\nПрефаб: PF_PineBillboard", "OK");
        }

        [MenuItem("Witcher/Art/Install Sprite Sword On Player")]
        public static void InstallSpriteSword()
        {
            EnsureSpriteFolder();
            var swordSprite = LoadOrCreateSwordSprite();

            // Update prefab
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPlayer);
            if (playerPrefab != null)
            {
                string path = PrefabPlayer;
                using (var edit = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    StripAndInstallSword(edit.prefabContentsRoot, swordSprite);
                }
            }

            // Update scene instance
            var scenePlayer = GameObject.Find($"{SceneRoots.Gameplay}/{SceneRoots.Player}")
                              ?? GameObject.Find(SceneRoots.Player)
                              ?? Object.FindAnyObjectByType<FirstPersonController>()?.gameObject;
            if (scenePlayer != null)
            {
                StripAndInstallSword(scenePlayer, swordSprite);
                EditorSceneManager.MarkAllScenesDirty();
            }

            EditorUtility.DisplayDialog(
                "Witcher",
                "Меч теперь 2D-спрайт у камеры.\n" +
                "Замени Art/Sprites/T_Sword_Pixel.png своим пиксельным мечом (с альфой) и снова запусти это меню.",
                "OK");
        }

        [MenuItem("Witcher/Art/Apply Hybrid Retro Bundle")]
        public static void ApplyHybridBundle()
        {
            MakeTexturesCrunchy();
            ApplyPixelRenderScale();
            SoftenRetroDefaultsStronger();
            TuneBrightDayFog();
            ConvertForestToBillboards();
            InstallSpriteSword();
            EditorUtility.DisplayDialog(
                "Witcher — Hybrid Retro",
                "Сделано:\n• crunchy textures\n• render scale 0.35\n• billboard forest\n• sprite sword\n• fog BrightDay\n• Retro Look stronger\n\nPlay ▶",
                "OK");
        }

        // -------------------------------------------------------------------------

        static bool ConfigureCrunchyImport(string path, int maxSize, bool asSprite = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return false;

            if (asSprite || path.Contains("/Sprites/"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = path.Contains("Sword") ? 128f : 64f;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                importer.SetTextureSettings(settings);
                importer.spritePivot = new Vector2(0.5f, 0f);
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
            }

            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.anisoLevel = 0;
            importer.wrapMode = path.Contains("T_Sky") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return true;
        }

        static void EnsureSpriteFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Witcher/Art"))
                AssetDatabase.CreateFolder("Assets/Witcher", "Art");
            if (!AssetDatabase.IsValidFolder(SpritePath))
                AssetDatabase.CreateFolder("Assets/Witcher/Art", "Sprites");
            if (!AssetDatabase.IsValidFolder(PrefabNature))
            {
                // best-effort; ArchitectureBuilder usually created it
            }
        }

        static Sprite LoadOrCreateTreeSprite()
        {
            var custom = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritePath}/T_Pine_Billboard.png");
            if (custom != null) return custom;

            // Prefer hedge texture converted to sprite
            foreach (var ext in new[] { ".png", ".jpg" })
            {
                var hedgePath = $"{TexPath}/T_Hedge{ext}";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(hedgePath) == null)
                    continue;

                var dest = $"{SpritePath}/T_Pine_Billboard.png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(dest) == null)
                    AssetDatabase.CopyAsset(hedgePath, dest);

                ConfigureCrunchyImport(dest, 128, asSprite: true);
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>(dest);
                if (spr != null) return spr;
            }

            return CreateProceduralSprite($"{SpritePath}/T_Pine_Billboard.png", DrawPinePixels, 64, 96);
        }

        static Sprite LoadOrCreateSwordSprite()
        {
            var path = $"{SpritePath}/T_Sword_Pixel.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            // Never overwrite a real painted sword with the tiny procedural stub
            if (tex != null && tex.width >= 64 && tex.height >= 64)
            {
                ConfigureCrunchyImport(path, 1024, asSprite: true);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) return sprite;
            }

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null && existing.rect.width >= 64f)
                return existing;

            return CreateProceduralSprite(path, DrawSwordPixels, 48, 96);
        }

        static Sprite CreateProceduralSprite(string assetPath, System.Action<Texture2D> paint, int w, int h)
        {
            EnsureSpriteFolder();
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            // clear
            var clear = new Color[w * h];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            tex.SetPixels(clear);
            paint(tex);
            tex.Apply();

            var abs = assetPath.Replace("Assets/", Application.dataPath + "/");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(abs));
            System.IO.File.WriteAllBytes(abs, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath);
            ConfigureCrunchyImport(assetPath, Mathf.Max(w, h), asSprite: true);
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        static void DrawPinePixels(Texture2D tex)
        {
            int w = tex.width;
            int h = tex.height;
            // trunk
            FillRect(tex, w / 2 - 2, 0, 4, h / 4, new Color(0.25f, 0.15f, 0.08f, 1f));
            // foliage layers
            Color green = new Color(0.12f, 0.35f, 0.14f, 1f);
            Color dark = new Color(0.06f, 0.2f, 0.08f, 1f);
            for (int layer = 0; layer < 4; layer++)
            {
                int top = h / 4 + layer * (h / 6);
                int half = w / 2 - layer * 4;
                for (int y = 0; y < h / 5; y++)
                {
                    int rowHalf = half - y / 2;
                    for (int x = -rowHalf; x <= rowHalf; x++)
                    {
                        int px = w / 2 + x;
                        int py = top + y;
                        if (px < 0 || px >= w || py < 0 || py >= h) continue;
                        tex.SetPixel(px, py, (x + y) % 3 == 0 ? dark : green);
                    }
                }
            }
        }

        static void DrawSwordPixels(Texture2D tex)
        {
            int w = tex.width;
            int h = tex.height;
            Color blade = new Color(0.75f, 0.9f, 1f, 1f);
            Color edge = new Color(0.45f, 0.7f, 0.95f, 1f);
            Color hilt = new Color(0.4f, 0.22f, 0.1f, 1f);
            Color guard = new Color(0.7f, 0.7f, 0.75f, 1f);
            // blade vertical
            FillRect(tex, w / 2 - 3, h / 3, 6, h * 2 / 3 - 2, blade);
            FillRect(tex, w / 2 - 1, h / 3, 2, h * 2 / 3 - 2, edge);
            // tip
            for (int i = 0; i < 6; i++)
                FillRect(tex, w / 2 - 3 + i / 2, h - 2 - i, 6 - i, 1, blade);
            // guard
            FillRect(tex, w / 2 - 12, h / 3 - 3, 24, 4, guard);
            // hilt
            FillRect(tex, w / 2 - 3, 4, 6, h / 3 - 6, hilt);
        }

        static void FillRect(Texture2D tex, int x, int y, int w, int h, Color c)
        {
            for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
            {
                if (xx < 0 || yy < 0 || xx >= tex.width || yy >= tex.height) continue;
                tex.SetPixel(xx, yy, c);
            }
        }

        static GameObject CreateTreeBillboard(string name, Sprite sprite, float heightScale)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            go.AddComponent<BillboardY>();
            float h = 3.5f * heightScale;
            go.transform.localScale = Vector3.one * h;
            // Feet on ground: sprite pivot usually center
            return go;
        }

        static void StripAndInstallSword(GameObject playerRoot, Sprite swordSprite)
        {
            var cam = playerRoot.GetComponentInChildren<Camera>();
            if (cam == null) return;

            // Remove old 3D / held weapon
            foreach (var held in playerRoot.GetComponentsInChildren<HeldWeapon>(true))
                Object.DestroyImmediate(held.gameObject);

            foreach (Transform t in cam.transform)
            {
                if (t.name.Contains("Sword") || t.name.Contains("Silver") || t.name.Contains("View_"))
                    Object.DestroyImmediate(t.gameObject);
            }

            var existing = cam.GetComponentInChildren<SpriteHeldWeapon>(true);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var swordGo = new GameObject("View_SpriteSword");
            swordGo.transform.SetParent(cam.transform, false);
            var view = swordGo.AddComponent<SpriteHeldWeapon>();
            view.localPosition = new Vector3(0.36f, -0.38f, 0.62f);
            view.localEuler = new Vector3(5f, -5f, -12f);
            view.localScale = 0.38f;
            view.SetSprite(swordSprite);
            var sr = swordGo.GetComponentInChildren<SpriteRenderer>();
            if (sr == null)
            {
                var child = new GameObject("SwordSprite");
                child.transform.SetParent(swordGo.transform, false);
                sr = child.AddComponent<SpriteRenderer>();
            }

            sr.sprite = swordSprite;
            view.spriteRenderer = sr;
            view.weaponSprite = swordSprite;
        }

        static void SoftenRetroDefaultsStronger()
        {
            // Patch any RetroLookFeature assets embedded in PC_Renderer
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer == null) return;

            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is not RetroLookFeature retro) continue;
                retro.settings.pixelSize = 2.5f;
                retro.settings.colorSteps = 14f;
                retro.settings.ditherStrength = 0.28f;
                retro.settings.contrast = 1.1f;
                retro.settings.saturation = 1.0f;
                retro.settings.vignette = 0.22f;
                EditorUtility.SetDirty(retro);
            }

            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        static void TuneBrightDayFog()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(BrightDay);
            if (profile == null) return;

            profile.fog = true;
            profile.fogColor = new Color(0.55f, 0.72f, 0.95f);
            profile.fogDensity = 0.012f;
            profile.ambientLight = new Color(0.5f, 0.62f, 0.85f);
            profile.shadowColor = new Color(0.22f, 0.25f, 0.42f);
            profile.lightIntensity = 1.45f;
            EditorUtility.SetDirty(profile);

            var atmosphere = Object.FindAnyObjectByType<DarkFantasyAtmosphere>();
            if (atmosphere != null)
            {
                atmosphere.profile = profile;
                atmosphere.Apply();
                EditorUtility.SetDirty(atmosphere);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
#endif
