using UnityEngine;

namespace Witcher.Data
{
    /// <summary>
    /// Data-only mood preset (ScriptableObject).
    /// Scene logic reads this — no hard-coded colors in MonoBehaviours.
    /// </summary>
    [CreateAssetMenu(fileName = "Atmosphere_", menuName = "Witcher/Atmosphere Profile", order = 0)]
    public sealed class AtmosphereProfile : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Mood";

        [Header("Fog")]
        public bool fog = true;
        public FogMode fogMode = FogMode.ExponentialSquared;
        public Color fogColor = new Color(0.2f, 0.15f, 0.25f);
        [Range(0f, 0.1f)] public float fogDensity = 0.02f;

        [Header("Ambient")]
        public Color ambientLight = new Color(0.2f, 0.18f, 0.25f);
        public Color shadowColor = new Color(0.05f, 0.04f, 0.08f);

        [Header("Sun / Moon")]
        public Color lightColor = new Color(1f, 0.55f, 0.35f);
        [Range(0f, 3f)] public float lightIntensity = 1.2f;
        public Vector3 lightEuler = new Vector3(15f, -30f, 0f);

        [Header("Camera")]
        public Color cameraBackground = new Color(0.15f, 0.1f, 0.18f);
    }
}
