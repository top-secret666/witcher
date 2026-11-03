#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Witcher.Visuals;

namespace Witcher.EditorTools
{
    /// <summary>
    /// Legacy / low-level helpers. Prefer Witcher → Architecture for scene work.
    /// </summary>
    public static class DarkFantasySetup
    {
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
                    Selection.activeObject = renderer;
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance<RetroLookFeature>();
            feature.name = "Retro Look";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            AssetDatabase.SaveAssets();

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var so = new SerializedObject(renderer);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            Selection.activeObject = renderer;
            Debug.Log("[Witcher] Retro Look feature added to PC_Renderer.");
        }

        [MenuItem("Witcher/2. Build Dark Fantasy Demo Scene", true)]
        static bool ValidateOldBuild() => false; // hidden — use Architecture menu

        [MenuItem("Witcher/2. Build Dark Fantasy Demo Scene")]
        public static void BuildDemoSceneRedirect()
        {
            ArchitectureBuilder.RebuildSceneFromPrefabs();
        }
    }
}
#endif
