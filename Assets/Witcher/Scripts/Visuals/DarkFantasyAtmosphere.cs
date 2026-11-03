using UnityEngine;
using Witcher.Data;

namespace Witcher.Visuals
{
    /// <summary>
    /// Applies an <see cref="AtmosphereProfile"/> to the scene.
    /// Lives under _Systems/Atmosphere — not on the castle or player.
    /// </summary>
    [ExecuteAlways]
    public sealed class DarkFantasyAtmosphere : MonoBehaviour
    {
        [Header("Data")]
        public AtmosphereProfile profile;

        [Header("References")]
        public Light sunOrMoon;
        public Camera mainCamera;

        [Header("Edit")]
        public bool applyInEditMode = true;

        void OnEnable() => Apply();

        void OnValidate()
        {
            if (applyInEditMode)
                Apply();
        }

        [ContextMenu("Apply Profile")]
        public void Apply()
        {
            if (profile == null)
                return;

            if (sunOrMoon == null)
                sunOrMoon = FindAnyObjectByType<Light>();
            if (mainCamera == null)
                mainCamera = Camera.main;

            RenderSettings.fog = profile.fog;
            RenderSettings.fogMode = profile.fogMode;
            RenderSettings.fogColor = profile.fogColor;
            RenderSettings.fogDensity = profile.fogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = profile.ambientLight;
            RenderSettings.subtractiveShadowColor = profile.shadowColor;

            if (sunOrMoon != null)
            {
                sunOrMoon.type = LightType.Directional;
                sunOrMoon.color = profile.lightColor;
                sunOrMoon.intensity = profile.lightIntensity;
                sunOrMoon.shadows = LightShadows.Soft;
                sunOrMoon.transform.rotation = Quaternion.Euler(profile.lightEuler);
            }

            if (mainCamera != null)
                mainCamera.backgroundColor = profile.cameraBackground;

            DynamicGI.UpdateEnvironment();
        }
    }
}
