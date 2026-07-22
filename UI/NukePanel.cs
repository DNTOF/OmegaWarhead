using System;
using System.Collections.Generic;
using System.Text;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.UI.Utilities;
using MEC;
using OmegaWarhead.Core;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using Player = Exiled.API.Features.Player;

namespace OmegaWarhead.UI
{
    /// <summary>
    /// Nuke launch UI panel (based on HintServiceMeow).
    ///
    /// Panel structure: four layers of fixed-coordinate Hints stacked together,
    /// matching the layered layout from the design document:
    ///   HeaderHint  Y≈150  Top border + title
    ///   InfoHint    Y≈260  Identity/objective description
    ///   StatusHint  Y≈420  Current phase + countdown + character progress bar
    ///   FooterHint  Y≈560  Operation tip + bottom border
    ///
    /// Important: we use HSM's Hint (fixed coordinate type), not DynamicHint.
    /// DynamicHint auto-repositions to avoid overlap and would disrupt our
    /// carefully positioned four-layer stack — a previous version incorrectly used
    /// DynamicHint; corrected here to Hint.
    /// Specific class names / namespace / enum names: verify against your local
    /// HSM-EX version's actual API — these are the least deterministic lines in the project.
    ///
    /// Color semantics: Blue = normal info, Yellow = Confirming, Red = Locked/Counting irreversible.
    /// Border style: `* - [ ]` mixed, mimicking Foundation announcement tone.
    /// </summary>
    public sealed class NukePanel
    {
        #region Singleton

        private static NukePanel _instance;
        public static NukePanel Instance => _instance;

        public static NukePanel Create()
        {
            if (_instance != null)
                return _instance;
            _instance = new NukePanel();
            return _instance;
        }

        public static void Destroy()
        {
            _instance?.HideAllForAll();
            _instance = null;
        }

        #endregion

        private const int HeaderY = 150;
        private const int InfoY = 260;
        private const int StatusY = 420;
        private const int FooterY = 560;

        private const string ColorBlue = "#4aa3ff";
        private const string ColorYellow = "#ffcc00";
        private const string ColorRed = "#ff3b3b";

        private sealed class PanelSet
        {
            public Hint Header;
            public Hint Info;
            public Hint Status;
            public Hint Footer;
            public CoroutineHandle RefreshHandle;
        }

        private readonly Dictionary<Player, PanelSet> _panels = new Dictionary<Player, PanelSet>();

        private NukePanel() { }

        #region Panel Lifecycle

        /// <summary>
        /// Creates and shows the four-layer panel (skips if already exists),
        /// renders initial content as Idle state.
        /// Called by NukeSessionManager's poll coroutine when controller is detected in inventory.
        /// </summary>
        public void ShowPanel(Player player)
        {
            if (player == null || !player.IsConnected)
                return;

            if (_panels.ContainsKey(player))
                return;

            PanelSet set = new PanelSet
            {
                Header = new Hint { YCoordinate = HeaderY, Alignment = HintAlignment.Center, FontSize = 22 },
                Info = new Hint { YCoordinate = InfoY, Alignment = HintAlignment.Center, FontSize = 20 },
                Status = new Hint { YCoordinate = StatusY, Alignment = HintAlignment.Center, FontSize = 26 },
                Footer = new Hint { YCoordinate = FooterY, Alignment = HintAlignment.Center, FontSize = 18 },
            };

            HintServiceMeow.Core.Utilities.PlayerDisplay display = HintServiceMeow.Core.Utilities.PlayerDisplay.Get(player);
            display.AddHint(set.Header);
            display.AddHint(set.Info);
            display.AddHint(set.Status);
            display.AddHint(set.Footer);

            _panels[player] = set;

            RenderIdle(player);
        }

        /// <summary>
        /// Removes the player's four-layer panel and stops its refresh coroutine (if any).
        /// Called by the poll coroutine (when controller is no longer in inventory while still Idle)
        /// or on session destruction.
        /// </summary>
        public void HidePanel(Player player)
        {
            if (player == null)
                return;

            if (!_panels.TryGetValue(player, out PanelSet set))
                return;

            if (set.RefreshHandle.IsRunning)
                Timing.KillCoroutines(set.RefreshHandle);

            if (player.IsConnected)
            {
                HintServiceMeow.Core.Utilities.PlayerDisplay display = HintServiceMeow.Core.Utilities.PlayerDisplay.Get(player);
                try { display.RemoveHint(set.Header); } catch { }
                try { display.RemoveHint(set.Info); } catch { }
                try { display.RemoveHint(set.Status); } catch { }
                try { display.RemoveHint(set.Footer); } catch { }
            }

            _panels.Remove(player);
        }

        /// <summary>Used by the poll coroutine to check whether a player already has the panel shown, avoiding duplicate Show/Hide.</summary>
        public bool IsShown(Player player) => player != null && _panels.ContainsKey(player);

        #endregion

        #region Per-Phase Rendering

        /// <summary>Idle: not activated, blue tone.</summary>
        public void RenderIdle(Player player)
        {
            if (player == null || !_panels.TryGetValue(player, out PanelSet set))
                return;

            if (set.RefreshHandle.IsRunning)
                Timing.KillCoroutines(set.RefreshHandle);

            set.Header.Text = Border(ColorBlue, "OMEGA Warhead Launch Control System");
            set.Info.Text = $"<color={ColorBlue}>Status: [ INACTIVE ]</color>\nHolder: {player.Nickname}";
            set.Status.Text = $"<color={ColorBlue}>Standby</color>";
            set.Footer.Text = FooterBorder(ColorBlue, "Activate the device to initiate launch sequence");
        }

        /// <summary>
        /// Confirming: yellow warning, shows remaining confirmation window seconds.
        /// Called once per second by NukeSessionManager's ConfirmWindowCoroutine to refresh.
        /// </summary>
        public void RenderConfirming(Player player, float remainingWindow, float totalWindow)
        {
            if (player == null || !_panels.TryGetValue(player, out PanelSet set))
                return;

            set.Header.Text = Border(ColorYellow, "WARNING: Launch Sequence Pending");
            set.Info.Text = $"<color={ColorYellow}>Activate again to confirm launch</color>\n" +
                             $"Timeout in {Math.Ceiling(remainingWindow):F0}s will auto-cancel";
            set.Status.Text = $"<color={ColorYellow}>{BuildBar(remainingWindow, totalWindow, 6)}</color>";
            set.Footer.Text = FooterBorder(ColorYellow, "This action is irreversible — confirm with caution");
        }

        /// <summary>
        /// Locked: brief transitional state after role reset (red), subsequently overridden by ShowCounting.
        /// </summary>
        public void RenderLocked(Player player)
        {
            if (player == null || !_panels.TryGetValue(player, out PanelSet set))
                return;

            set.Header.Text = Border(ColorRed, "Identity Reset");
            set.Info.Text = $"<color={ColorRed}>You have been detached from your original team</color>\nStatus: [ LONE OPERATIVE ]";
            set.Status.Text = $"<color={ColorRed}>Everyone is your enemy</color>";
            set.Footer.Text = FooterBorder(ColorRed, "Survive to complete the launch sequence");
        }

        /// <summary>
        /// Counting: starts a per-second refresh coroutine that continuously renders
        /// the countdown + progress bar. Turns solid red with "Cannot reverse" after
        /// crossing the point of no return.
        /// </summary>
        public void ShowCounting(Player player, NukeSession session)
        {
            if (player == null || session == null || !_panels.TryGetValue(player, out PanelSet set))
                return;

            if (set.RefreshHandle.IsRunning)
                Timing.KillCoroutines(set.RefreshHandle);

            set.RefreshHandle = Timing.RunCoroutine(CountingRefreshCoroutine(player, session));
        }

        private IEnumerator<float> CountingRefreshCoroutine(Player player, NukeSession session)
        {
            while (_panels.ContainsKey(player) && session.State == NukeState.Counting)
            {
                if (_panels.TryGetValue(player, out PanelSet set))
                {
                    string color = session.PointOfNoReturn ? ColorRed : ColorYellow;
                    int percent = session.TotalTime > 0f
                        ? (int)Math.Round(100f * (1f - session.RemainingTime / session.TotalTime))
                        : 0;

                    set.Header.Text = Border(color, "OMEGA Warhead Launch Control System");
                    set.Info.Text = session.PointOfNoReturn
                        ? $"<color={ColorRed}><b>Point of no return crossed — cannot reverse</b></color>"
                        : $"<color={color}>Launch sequence in progress</color>";
                    set.Status.Text =
                        $"<color={color}><b>{session.RemainingTime:F0}</b></color> seconds until detonation\n" +
                        $"<color={color}>{BuildBar(session.RemainingTime, session.TotalTime, 12)} {percent}%</color>";
                    set.Footer.Text = FooterBorder(color, "Survive until detonation");
                }

                yield return Timing.WaitForSeconds(1f);
            }
        }

        #endregion

        #region Utilities

        private string Border(string color, string title)
        {
            return $"<color={color}>|** - [ {title} ] - **|</color>";
        }

        private string FooterBorder(string color, string tip)
        {
            return $"<color={color}>-----[ TIP ]-----</color>\n<color=#ffffff>{tip}</color>";
        }

        /// <summary>Builds a progress bar using ▓/░ characters. Ratio is "elapsed" (less remaining = fuller bar).</summary>
        private string BuildBar(float remaining, float total, int width)
        {
            if (total <= 0f)
                total = 1f;

            float ratio = Math.Max(0f, Math.Min(1f, 1f - remaining / total));
            int filled = (int)Math.Round(ratio * width);
            filled = Math.Max(0, Math.Min(width, filled));

            StringBuilder sb = new StringBuilder();
            sb.Append('▓', filled);
            sb.Append('░', width - filled);
            return sb.ToString();
        }

        #endregion

        #region Unified Cleanup

        /// <summary>Hides the panel for a specific player (equivalent to HidePanel; retained for semantic clarity in Manager calls).</summary>
        public void HideAllUI(Player player) => HidePanel(player);

        /// <summary>Hides panels for all players (called on round end / plugin unload).</summary>
        public void HideAllForAll()
        {
            List<Player> players = new List<Player>(_panels.Keys);
            foreach (Player p in players)
                HidePanel(p);
        }

        #endregion
    }
}
