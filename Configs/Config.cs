using System.ComponentModel;
using Exiled.API.Interfaces;

namespace OmegaWarhead.Configs
{
    /// <summary>
    /// Main plugin configuration. All values can be adjusted in the generated config.yml.
    /// </summary>
    public sealed class Config : IConfig
    {
        [Description("Whether the plugin is enabled")]
        public bool IsEnabled { get; set; } = true;

        [Description("Whether to output debug logs")]
        public bool Debug { get; set; } = false;

        // ------------------- Collecting Phase -------------------

        [Description("Number of radioactive elements required to auto-synthesize the warhead launch controller")]
        public int RequiredElementCount { get; set; } = 5;

        [Description("Maximum number of radioactive elements present on the map at once (random room spawns)")]
        public int MaxSpawnedElements { get; set; } = 6;

        [Description("Delay in seconds before a new element respawns after one is picked up or consumed")]
        public float ElementRespawnDelay { get; set; } = 45f;

        [Description("Damage dealt per second, per radioactive element held")]
        public float DamagePerElementPerSecond { get; set; } = 1f;

        [Description("Collecting phase: interval in seconds for server-wide location broadcast of element holders. Higher = less frequent tracking")]
        public float CollectingTrackIntervalSeconds { get; set; } = 6f;

        // ------------------- Confirmation / Lock Phase -------------------

        [Description("Confirmation window duration in seconds: after first activation, the operator must activate again within this window to confirm")]
        public float ConfirmWindowSeconds { get; set; } = 5f;

        // ------------------- Countdown Phase -------------------

        [Description("Total countdown duration in seconds, starting from Locked → Counting. Default 268s (4m28s), matching the aLIEz track length")]
        public float CountdownTotalSeconds { get; set; } = 268f;

        [Description("Point of no return (seconds): once remaining time drops below this, operator death will no longer abort the launch")]
        public float PointOfNoReturnSeconds { get; set; } = 10f;

        [Description("Countdown phase: interval in seconds for operator location broadcast. Should be more frequent than the collecting phase")]
        public float CountingTrackIntervalSeconds { get; set; } = 3f;

        [Description("Delay in seconds between countdown reaching zero and the global kill execution, allowing alerts/CASSIE to finish playing")]
        public float DetonationKillDelaySeconds { get; set; } = 4f;
    }
}
