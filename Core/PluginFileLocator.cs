using System;
using System.IO;
using Exiled.API.Features;

namespace OmegaWarhead.Core
{
    /// <summary>
    /// Locates the plugin DLL file on disk.
    ///
    /// Priority: EXILED's Paths.Plugins (the authoritative plugin directory),
    /// then Assembly.Location, then walking up from the base directory.
    /// Shared by AutoUpdater (file overwrite) and the info command (SHA-256).
    /// </summary>
    public static class PluginFileLocator
    {
        /// <summary>
        /// Resolves the plugin DLL path via multiple candidates:
        /// 1. EXILED Paths.Plugins (authoritative plugin directory)
        /// 2. Assembly.Location (normal EXILED load)
        /// 3. Walking up from the base directory to find OmegaWarhead.dll
        /// Returns null if not found.
        /// </summary>
        public static string ResolveDllPath()
        {
            // Candidate 1: EXILED's authoritative plugin directory
            try
            {
                string pluginsDir = Paths.Plugins;
                if (!string.IsNullOrEmpty(pluginsDir))
                {
                    string candidate = Path.Combine(pluginsDir, "OmegaWarhead.dll");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch { }

            // Candidate 2: Assembly.Location
            try
            {
                string loc = typeof(PluginFileLocator).Assembly.Location;
                if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
                    return loc;
            }
            catch { }

            // Candidate 3: search from base directory upward for OmegaWarhead.dll
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                for (int i = 0; i < 4 && dir != null; i++)
                {
                    string candidate = Path.Combine(dir, "OmegaWarhead.dll");
                    if (File.Exists(candidate))
                        return candidate;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch { }

            // Candidate 4: legacy EXILED directories
            try
            {
                string[] candidates =
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EXILED", "Plugins", "OmegaWarhead.dll"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EXILED_Data", "Plugins", "OmegaWarhead.dll"),
                };
                foreach (string c in candidates)
                {
                    if (File.Exists(c))
                        return c;
                }
            }
            catch { }

            return null;
        }
    }
}
