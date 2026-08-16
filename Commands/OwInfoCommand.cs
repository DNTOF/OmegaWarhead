using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CommandSystem;
using Exiled.API.Features;
using OmegaWarhead.Configs;
using OmegaWarhead.Core;

namespace OmegaWarhead.Commands
{
    /// <summary>
    /// Player game-console command: owinfo (typed as ".owinfo" in the in-game
    /// console opened with the ~ key).
    ///
    /// Shows plugin information for authorized users only:
    ///   - Last plugin update time
    ///   - Total number of OMEGA Warhead launches
    ///   - SHA-256 of the plugin DLL file
    ///
    /// Permission: server console (full permissions) OR the designated owner SteamID.
    /// </summary>
    [CommandHandler(typeof(ClientCommandHandler))]
    public sealed class OwInfoCommand : ICommand
    {
        /// <inheritdoc />
        public string Command { get; } = "owinfo";

        /// <inheritdoc />
        public string[] Aliases { get; } = System.Array.Empty<string>();

        /// <inheritdoc />
        public string Description { get; } = "Shows OMEGA Warhead plugin info (last update, total launches, DLL checksum).";

        /// <inheritdoc />
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = string.Empty;

            // Permission check (mirrors the proven player_gui approach):
            // - Non-player senders (server console) are always allowed
            // - Players allowed if they have RemoteAdmin access OR are the designated owner SteamID
            Player player = Player.Get(sender);
            bool isAuthorized;
            if (player == null)
            {
                isAuthorized = true; // server console / host
            }
            else
            {
                isAuthorized = player.RemoteAdminAccess
                              || string.Equals(ExtractSteamId(player.UserId), StatsTracker.OwnerSteamId, StringComparison.OrdinalIgnoreCase);
            }

            if (!isAuthorized)
            {
                response = Localization.OwInfoDenied;
                return false;
            }

            // --- Last update: show absolute date + time (yyyy-MM-dd HH:mm:ss) ---
            // DateTime.MinValue means no persisted record exists yet.
            string lastUpdate = StatsTracker.LastUpdateTime == DateTime.MinValue
                ? (Localization.IsEnglish ? "never" : "暂无记录")
                : StatsTracker.LastUpdateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

            // --- Total launches ---
            int totalLaunches = StatsTracker.TotalLaunchCount;

            // --- SHA-256 of the DLL ---
            string sha256 = ComputeSha256();

            response = $"{Localization.OwInfoHeader}\n" +
                       $"{Localization.OwInfoLastUpdate} {lastUpdate}\n" +
                       $"{Localization.OwInfoTotalLaunches} {totalLaunches}\n" +
                       $"{Localization.OwInfoSha256} {sha256}\n" +
                       $"<color=#888888><size=10>By DNT_OF</size></color>";
            return true;
        }

        /// <summary>
        /// Extracts the pure Steam64ID from a "76561198000000000@steam" style UserId.
        /// </summary>
        private static string ExtractSteamId(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            int at = userId.IndexOf('@');
            return at > 0 ? userId.Substring(0, at) : userId;
        }

        /// <summary>
        /// Computes the SHA-256 of the plugin DLL file.
        /// Falls back through multiple candidate paths in case Assembly.Location
        /// is empty (some EXILED loaders) or the file is locked.
        /// </summary>
        private static string ComputeSha256()
        {
            try
            {
                string path = PluginFileLocator.ResolveDllPath();
                if (path == null)
                    return "unknown";

                // ReadAllBytes is more tolerant than FileStream on locked files.
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(bytes);
                    StringBuilder sb = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash)
                        sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[owinfo] SHA-256 computation failed: {ex.Message}");
                return "unknown";
            }
        }
    }
}
