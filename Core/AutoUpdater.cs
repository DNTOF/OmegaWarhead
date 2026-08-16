using System;
using System.IO;
using System.Net;
using System.Threading;
using Exiled.API.Features;
using MEC;

namespace OmegaWarhead.Core
{
    /// <summary>
    /// Automatic update checker.
    ///
    /// On plugin enable, fetches the latest release info from the GitHub repository.
    /// If a newer version is available, downloads the DLL and overwrites the current
    /// plugin file on disk (the new DLL takes effect on the next server restart or
    /// plugin reload — in-place overwrite of a loaded assembly is not possible).
    ///
    /// Uses <see cref="WebClient"/> deliberately (rather than HttpClient) to stay
    /// compatible with .NET Framework 4.8 and mono-less environments.
    /// </summary>
    public static class AutoUpdater
    {
        /// <summary>GitHub repo owner.</summary>
        public const string RepoOwner = "DNTOF";

        /// <summary>GitHub repo name.</summary>
        public const string RepoName = "OmegaWarhead";

        /// <summary>Latest release API URL (public, no auth needed).</summary>
        private const string LatestReleaseApi = "https://api.github.com/repos/DNTOF/OmegaWarhead/releases/latest";

        /// <summary>
        /// Whether an update is currently being downloaded. Prevents concurrent downloads.
        /// </summary>
        private static bool _downloading;

        /// <summary>
        /// Whether the plugin file was successfully replaced by a newer version this session.
        /// </summary>
        public static bool UpdatedApplied { get; private set; }

        /// <summary>
        /// Starts the update check on a background thread.
        /// </summary>
        public static void CheckForUpdates()
        {
            Thread thread = new Thread(CheckAsync)
            {
                IsBackground = true,
                Name = "OmegaWarhead-AutoUpdate"
            };
            thread.Start();
        }

        private static void CheckAsync()
        {
            try
            {
                Log.Info("[AutoUpdater] Checking for updates...");

                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "OmegaWarhead-AutoUpdater/1.0");

                    // Fetch latest release metadata
                    string json = client.DownloadString(LatestReleaseApi);
                    string tagName = ExtractTagName(json);

                    if (string.IsNullOrEmpty(tagName))
                    {
                        Log.Warn("[AutoUpdater] Could not parse latest release tag.");
                        return;
                    }

                    // Compare with current plugin version.
                    // InformationalVersion carries build markers (e.g. "1.0.1-fix2"),
                    // which Plugin.Version (always "1.0.1") cannot distinguish.
                    string currentVersion = GetInformationalVersion();
                    Log.Info($"[AutoUpdater] Latest release: {tagName}, current: {currentVersion}");

                    if (tagName.Equals(currentVersion, StringComparison.OrdinalIgnoreCase) ||
                        tagName.Equals($"v{currentVersion}", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Info("[AutoUpdater] Already up to date.");
                        return;
                    }

                    // New version available — download the DLL asset
                    string dllUrl = ExtractDllUrl(json);
                    if (string.IsNullOrEmpty(dllUrl))
                    {
                        Log.Warn("[AutoUpdater] Release found but no DLL asset in it.");
                        return;
                    }

                    DownloadAndReplace(dllUrl);
                }
            }
            catch (Exception ex)
            {
                // Update failures must NEVER break the plugin itself
                Log.Warn($"[AutoUpdater] Update check failed (non-fatal): {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads the new DLL and overwrites the current plugin file.
        /// </summary>
        private static void DownloadAndReplace(string dllUrl)
        {
            if (_downloading)
            {
                Log.Info("[AutoUpdater] Download already in progress, skipping.");
                return;
            }

            _downloading = true;
            try
            {
                string currentFile = PluginFileLocator.ResolveDllPath();
                if (currentFile == null)
                {
                    Log.Warn("[AutoUpdater] Cannot determine plugin file location; skipping update.");
                    return;
                }

                Log.Info($"[AutoUpdater] Downloading update from {dllUrl}...");

                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "OmegaWarhead-AutoUpdater/1.0");

                    // Download to a temp file first (never write a partial DLL over the live one)
                    string tempFile = currentFile + ".update.tmp";
                    client.DownloadFile(dllUrl, tempFile);

                    // Sanity check: the downloaded file must be a non-empty DLL
                    FileInfo info = new FileInfo(tempFile);
                    if (info.Length == 0)
                    {
                        Log.Warn("[AutoUpdater] Downloaded file is empty; aborting update.");
                        File.Delete(tempFile);
                        return;
                    }

                    // Overwrite the plugin DLL. On Windows, a loaded assembly file may be
                    // locked; if the overwrite fails, we still keep the temp file and log it.
                    try
                    {
                        File.Copy(tempFile, currentFile, overwrite: true);
                        UpdatedApplied = true;
                        Log.Info("[AutoUpdater] Update applied. Restart the server (or reload plugins) to load the new version.");
                    }
                    catch (IOException)
                    {
                        Log.Warn("[AutoUpdater] Plugin file is locked (assembly loaded). The update will be applied on the next server restart.");
                    }

                    try { File.Delete(tempFile); } catch { }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[AutoUpdater] Download failed (non-fatal): {ex.Message}");
            }
            finally
            {
                _downloading = false;
            }
        }

        /// <summary>
        /// Reads the assembly InformationalVersion (e.g. "1.0.1-fix2").
        /// Falls back to Plugin.Version.ToString() if the attribute is missing.
        /// </summary>
        private static string GetInformationalVersion()
        {
            try
            {
                var attrs = typeof(AutoUpdater).Assembly.GetCustomAttributes(
                    typeof(System.Reflection.AssemblyInformationalVersionAttribute), false);
                if (attrs.Length > 0 && attrs[0] is System.Reflection.AssemblyInformationalVersionAttribute attr)
                {
                    string info = attr.InformationalVersion;
                    if (!string.IsNullOrEmpty(info))
                        return info.Trim();
                }
            }
            catch { }

            return OmegaWarheadPlugin.Instance?.Version.ToString() ?? "0.0.0";
        }

        /// <summary>
        /// Extracts the tag name from the GitHub release API JSON.
        /// </summary>
        private static string ExtractTagName(string json)
        {
            const string key = "\"tag_name\":\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0)
                return null;

            int start = idx + key.Length;
            int end = json.IndexOf('"', start);
            return end > start ? json.Substring(start, end - start) : null;
        }

        /// <summary>
        /// Extracts the browser_download_url of the first .dll asset from the release JSON.
        /// </summary>
        private static string ExtractDllUrl(string json)
        {
            const string key = "\"browser_download_url\":\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            while (idx >= 0)
            {
                int start = idx + key.Length;
                int end = json.IndexOf('"', start);
                if (end > start)
                {
                    string url = json.Substring(start, end - start);
                    if (url.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        return url;
                }
                idx = json.IndexOf(key, start, StringComparison.Ordinal);
            }
            return null;
        }
    }
}
