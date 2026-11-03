namespace Witcher.Core
{
    /// <summary>
    /// Canonical Hierarchy root names for every gameplay scene.
    /// Keep empty organizers — never put mesh/logic directly under the scene root.
    /// </summary>
    public static class SceneRoots
    {
        public const string Systems = "_Systems";
        public const string Environment = "_Environment";
        public const string Gameplay = "_Gameplay";

        // Children of _Systems
        public const string Lighting = "Lighting";
        public const string Atmosphere = "Atmosphere";

        // Children of _Environment
        public const string Ground = "Ground";
        public const string Forest = "Forest";
        public const string Castle = "Castle";

        // Children of _Gameplay
        public const string Player = "Player";
    }
}
