using UnityEngine;

namespace Witcher.Visuals
{
    public enum DarkFantasyMood
    {
        CastleDusk,
        ManorNight
    }

    /// <summary>
    /// One-click mood: fog, ambient, directional light, and sky tint.
    /// Drop on any GameObject in the scene (or use Witcher menu to create it).
    /// </summary>
    [ExecuteAlways]
    public sealed class DarkFantasyAtmosphere : MonoBehaviour
    {
        [Header("Mood")]
        public DarkFantasyMood mood = DarkFantasyMood.CastleDusk;

        [Header("References (auto-filled if empty)")]
        public Light sunOrMoon;
        public Camera mainCamera;

        [Header("Apply")]
        public bool applyInEditMode = true;

        void OnEnable() => TryApply();
        void OnValidate()
        {
            if (applyInEditMode)
                TryApply();
        }

        [ContextMenu("Apply Mood Now")]
        public void TryApply()
        {
            if (sunOrMoon == null)
                sunOrMoon = FindFirstObjectByType<Light>();
            if (mainCamera == null)
                mainCamera = Camera.main;

            switch (mood)
            {
                case DarkFantasyMood.CastleDusk:
                    ApplyCastleDusk();
                    break;
                case DarkFantasyMood.ManorNight:
                    ApplyManorNight();
                    break;
            }
        }

        void ApplyCastleDusk()
        {
            // Sunset: deep purple / blood-orange, high contrast
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.35f, 0.18f, 0.28f);
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.16f, 0.28f);
            RenderSettings.subtractiveShadowColor = new Color(0.08f, 0.05f, 0.12f);

            if (sunOrMoon != null)
            {
                sunOrMoon.type = LightType.Directional;
                sunOrMoon.color = new Color(1f, 0.45f, 0.28f);
                sunOrMoon.intensity = 1.35f;
                sunOrMoon.shadows = LightShadows.Soft;
                sunOrMoon.transform.rotation = Quaternion.Euler(12f, -35f, 0f);
            }

            if (mainCamera != null)
                mainCamera.backgroundColor = new Color(0.28f, 0.12f, 0.22f);

            DynamicGI.UpdateEnvironment();
        }

        void ApplyManorNight()
        {
            // Moonlit teal / magenta night
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.08f, 0.16f, 0.22f);
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.16f, 0.22f);
            RenderSettings.subtractiveShadowColor = new Color(0.02f, 0.04f, 0.08f);

            if (sunOrMoon != null)
            {
                sunOrMoon.type = LightType.Directional;
                sunOrMoon.color = new Color(0.55f, 0.75f, 1f);
                sunOrMoon.intensity = 0.55f;
                sunOrMoon.shadows = LightShadows.Soft;
                sunOrMoon.transform.rotation = Quaternion.Euler(28f, 140f, 0f);
            }

            if (mainCamera != null)
                mainCamera.backgroundColor = new Color(0.05f, 0.08f, 0.14f);

            DynamicGI.UpdateEnvironment();
        }
    }
}
