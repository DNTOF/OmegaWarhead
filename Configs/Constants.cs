namespace OmegaWarhead.Configs
{
    /// <summary>
    /// Hardcoded gameplay constants. These values are intentionally NOT exposed
    /// in config.yml — they are baked into the assembly to keep the plugin
    /// tamper-resistant (editing the config cannot alter gameplay balance).
    /// </summary>
    public static class Constants
    {
        // ------------------- Collecting Phase -------------------

        /// <summary>Number of radioactive elements required to auto-synthesize the warhead launch controller.</summary>
        public const int RequiredElementCount = 5;

        /// <summary>Maximum number of radioactive elements present on the map at once.</summary>
        public const int MaxSpawnedElements = 6;

        /// <summary>Delay in seconds before an element respawns after being picked up or consumed.</summary>
        public const float ElementRespawnDelay = 45f;

        /// <summary>Damage dealt per second, per radioactive element held.</summary>
        public const float DamagePerElementPerSecond = 1f;

        /// <summary>Collecting phase: interval in seconds for server-wide location broadcasts of element holders.</summary>
        public const float CollectingTrackIntervalSeconds = 6f;

        // ------------------- Confirmation / Lock Phase -------------------

        /// <summary>Confirmation window in seconds: second activation must occur within this window.</summary>
        public const float ConfirmWindowSeconds = 5f;

        // ------------------- Countdown Phase -------------------

        /// <summary>Total countdown duration in seconds (268s = 4m28s, matching the aLIEz track length).</summary>
        public const float CountdownTotalSeconds = 268f;

        /// <summary>Point of no return (seconds): below this, operator death no longer aborts the launch.</summary>
        public const float PointOfNoReturnSeconds = 10f;

        /// <summary>Countdown phase: interval in seconds for operator location broadcasts.</summary>
        public const float CountingTrackIntervalSeconds = 3f;

        /// <summary>Delay in seconds between countdown zero and the global kill execution.</summary>
        public const float DetonationKillDelaySeconds = 4f;
    }
}
