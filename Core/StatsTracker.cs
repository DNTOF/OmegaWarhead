using System;
using System.IO;
using Exiled.API.Features;

namespace OmegaWarhead.Core
{
    /// <summary>
    /// Persistent statistics tracker.
    ///
    /// Stores plugin stats in a small JSON file inside the plugin's config directory:
    ///   - LastUpdateTime  : when the plugin DLL was last updated/replaced
    ///   - TotalLaunchCount: total number of OMEGA Warhead detonations across all sessions
    ///
    /// The file is written atomically (temp file + rename) to avoid corruption
    /// on crash mid-write.
    /// </summary>
    public static class StatsTracker
    {
        /// <summary>SteamID of the special authorized user (owner).</summary>
        public const string OwnerSteamId = "76561199173080951";

        /// <summary>Path to the stats file (relative to the plugin's config dir).</summary>
        private static string StatsFilePath
        {
            get
            {
                string configDir = Paths.Configs;
                try
                {
                    Directory.CreateDirectory(configDir);
                    return Path.Combine(configDir, "OmegaWarhead", "stats.json");
                }
                catch
                {
                    // Fallback: next to the plugin DLL
                    string dllPath = PluginFileLocator.ResolveDllPath();
                    string dir = dllPath != null ? Path.GetDirectoryName(dllPath) : null;
                    return Path.Combine(dir ?? ".", "OmegaWarhead_stats.json");
                }
            }
        }

        /// <summary>When the plugin was last updated (file write time of the DLL at load).</summary>
        public static DateTime LastUpdateTime { get; private set; }

        /// <summary>Total number of detonations across all sessions.</summary>
        public static int TotalLaunchCount { get; private set; }

        /// <summary>
        /// Loads persisted stats at plugin enable.
        /// Prefers the saved LastUpdateTime from stats.json (the real deployment
        /// time of the last update); falls back to the DLL file write time.
        /// </summary>
        public static void Load()
        {
            // Try to restore the persisted update time first.
            LastUpdateTime = DateTime.MinValue;

            TotalLaunchCount = 0;
            try
            {
                string path = StatsFilePath;
                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("LastUpdateTime", StringComparison.OrdinalIgnoreCase))
                        {
                            int eq = trimmed.IndexOf('=');
                            if (eq >= 0 && DateTime.TryParse(trimmed.Substring(eq + 1).Trim(), out DateTime saved))
                                LastUpdateTime = saved;
                        }
                        else if (trimmed.StartsWith("TotalLaunchCount", StringComparison.OrdinalIgnoreCase))
                        {
                            int eq = trimmed.IndexOf('=');
                            if (eq >= 0 && int.TryParse(trimmed.Substring(eq + 1).Trim(), out int count))
                                TotalLaunchCount = count;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[StatsTracker] Failed to load stats (non-fatal): {ex.Message}");
            }

            // No saved record yet — use the DLL file write time as the baseline.
            if (LastUpdateTime == DateTime.MinValue)
            {
                try
                {
                    string dllPath = PluginFileLocator.ResolveDllPath();
                    LastUpdateTime = dllPath != null ? File.GetLastWriteTime(dllPath) : DateTime.UtcNow;
                }
                catch
                {
                    LastUpdateTime = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Records a successful detonation.
        /// </summary>
        public static void RecordLaunch()
        {
            TotalLaunchCount++;
            Save();
        }

        /// <summary>
        /// Saves the stats file atomically.
        /// </summary>
        private static void Save()
        {
            try
            {
                string path = StatsFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                string temp = path + ".tmp";
                File.WriteAllText(temp,
                    $"LastUpdateTime={LastUpdateTime:O}{Environment.NewLine}" +
                    $"TotalLaunchCount={TotalLaunchCount}{Environment.NewLine}");
                File.Copy(temp, path, overwrite: true);
                try { File.Delete(temp); } catch { }
            }
            catch (Exception ex)
            {
                Log.Warn($"[StatsTracker] Failed to save stats (non-fatal): {ex.Message}");
            }
        }
    }
}
