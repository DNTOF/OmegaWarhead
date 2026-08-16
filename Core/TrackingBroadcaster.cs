using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.UI.Utilities;
using MEC;
using OmegaWarhead.Configs;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using Player = Exiled.API.Features.Player;

namespace OmegaWarhead.Core
{
    /// <summary>
    /// Location tracking broadcaster.
    ///
    /// Design notes:
    /// - Collecting phase: players holding radioactive elements are tracked server-wide at a coarse interval (e.g., every 6s)
    /// - Countdown phase: the operator is tracked at a high frequency (e.g., every 3s)
    /// - Both frequencies share the same implementation, differentiated by parameters
    ///
    /// Implementation strategy (key difference from previous versions):
    /// Previous versions used EXILED's native Broadcast() to re-send every few seconds,
    /// causing the on-screen hint to flicker/reappear constantly — bad UX, and inconsistent
    /// with the project's unified HSM persistent panel approach. The new approach:
    /// assigns each "viewer" (every online player except the tracked target) a persistent
    /// Hint, then updates only Hint.Text in-place without adding/removing Hints,
    /// achieving "persistent display with smooth content refresh" instead of flickering.
    ///
    /// Known limitation: players who join mid-tracking won't receive the Hint until the
    /// next Start/Stop cycle. This is an acceptable edge case given the small impact.
    /// </summary>
    public sealed class TrackingBroadcaster
    {
        #region Singleton

        private static TrackingBroadcaster _instance;
        public static TrackingBroadcaster Instance => _instance;

        public static TrackingBroadcaster Create()
        {
            if (_instance != null)
                return _instance;
            _instance = new TrackingBroadcaster();
            return _instance;
        }

        public static void Destroy()
        {
            _instance?.StopAll();
            _instance = null;
        }

        #endregion

        private const int TrackingHintY = 80;

        private sealed class BroadcastState
        {
            public CoroutineHandle Coroutine;

            /// <summary>Target → per-viewer Hint instances (for in-place text updates).</summary>
            public readonly Dictionary<Player, Hint> ViewerHints = new Dictionary<Player, Hint>();

            /// <summary>
            /// Countdown-phase only: references the current session for embedding the live remaining
            /// seconds in the broadcast text. Collecting-phase broadcasts don't need this; pass null.
            /// </summary>
            public NukeSession Session;
        }

        /// <summary>Collecting phase: element holder → broadcast state.</summary>
        private readonly Dictionary<Player, BroadcastState> _collecting = new Dictionary<Player, BroadcastState>();

        /// <summary>Countdown phase: operator → broadcast state.</summary>
        private readonly Dictionary<Player, BroadcastState> _counting = new Dictionary<Player, BroadcastState>();

        private TrackingBroadcaster() { }

        #region Collecting Phase Broadcast

        public void StartCollectingBroadcast(Player holder, float intervalSeconds)
        {
            StartBroadcast(_collecting, holder, intervalSeconds, isCounting: false, session: null);
        }

        public void StopCollectingBroadcast(Player holder)
        {
            StopBroadcast(_collecting, holder);
        }

        /// <summary>Allows external code to check whether a player is already being tracked for collecting, for edge-trigger hook decisions.</summary>
        public bool IsCollectingTracked(Player holder) => holder != null && _collecting.ContainsKey(holder);

        #endregion

        #region Countdown Phase Broadcast

        public void StartCountingBroadcast(Player op, NukeSession session, float intervalSeconds)
        {
            StartBroadcast(_counting, op, intervalSeconds, isCounting: true, session);
        }

        public void StopCountingBroadcast(Player op)
        {
            StopBroadcast(_counting, op);
        }

        #endregion

        #region Common Implementation

        private void StartBroadcast(Dictionary<Player, BroadcastState> table, Player target,
            float intervalSeconds, bool isCounting, NukeSession session)
        {
            if (target == null || table.ContainsKey(target))
                return;

            BroadcastState state = new BroadcastState { Session = session };

            // Assign a persistent Hint to every other online player
            foreach (Player viewer in Player.List)
            {
                if (viewer == null || viewer == target || !viewer.IsConnected)
                    continue;

                Hint hint = new Hint
                {
                    YCoordinate = TrackingHintY,
                    Alignment = HintAlignment.Center,
                    FontSize = 18,
                };
                HintServiceMeow.Core.Utilities.PlayerDisplay.Get(viewer).AddHint(hint);
                state.ViewerHints[viewer] = hint;
            }

            state.Coroutine = Timing.RunCoroutine(BroadcastCoroutine(target, state, intervalSeconds, isCounting));
            table[target] = state;
        }

        private void StopBroadcast(Dictionary<Player, BroadcastState> table, Player target)
        {
            if (target == null || !table.TryGetValue(target, out BroadcastState state))
                return;

            if (state.Coroutine.IsRunning)
                Timing.KillCoroutines(state.Coroutine);

            foreach (var kv in state.ViewerHints)
            {
                if (kv.Key != null && kv.Key.IsConnected)
                {
                    try { HintServiceMeow.Core.Utilities.PlayerDisplay.Get(kv.Key).RemoveHint(kv.Value); } catch { }
                }
            }
            state.ViewerHints.Clear();

            table.Remove(target);
        }

        private IEnumerator<float> BroadcastCoroutine(Player target, BroadcastState state, float interval, bool isCounting)
        {
            while (target != null && target.IsConnected && target.IsAlive)
            {
                string location = GetLocationDescription(target);
                string text;

                if (isCounting)
                {
                    // Previously the remaining seconds were omitted (only location was shown),
                    // so other players could see "who is where" but not "how long until detonation".
                    // Now reads from state.Session in real time.
                    float remaining = state.Session?.RemainingTime ?? -1f;
                    string timeText = remaining >= 0f
                        ? Localization.TrackingTimeRemaining(remaining)
                        : Localization.TrackingReadingCountdown;

                    text = Localization.TrackingCounting(
                        target.Nickname, location, timeText,
                        state.Session?.PointOfNoReturn ?? false);
                }
                else
                {
                    text = Localization.TrackingCollecting(target.Nickname, location);
                }

                // In-place text update for each viewer's Hint — no add/remove, no flicker
                foreach (var kv in state.ViewerHints)
                {
                    if (kv.Key != null && kv.Key.IsConnected)
                        kv.Value.Text = text;
                }

                yield return Timing.WaitForSeconds(interval);
            }
        }

        /// <summary>
        /// Gets a human-readable location description for a player.
        /// Uses the room's native English name from the EXILED API.
        /// </summary>
        private string GetLocationDescription(Player player)
        {
            try
            {
                Room room = player.CurrentRoom;
                if (room != null)
                {
                    string zone = room.Zone.ToString();
                    string roomName = room.Name;
                    return $"{roomName} ({zone})";
                }
                return player.Zone.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        public void StopAll()
        {
            List<Player> collectingTargets = new List<Player>(_collecting.Keys);
            foreach (Player p in collectingTargets)
                StopBroadcast(_collecting, p);

            List<Player> countingTargets = new List<Player>(_counting.Keys);
            foreach (Player p in countingTargets)
                StopBroadcast(_counting, p);
        }

        #endregion
    }
}
