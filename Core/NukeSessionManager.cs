using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;
using OmegaWarhead.Configs;
using OmegaWarhead.Items;
using OmegaWarhead.UI;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using PlayerRoles;
namespace OmegaWarhead.Core
{
    /// <summary>
    /// Nuke Session Manager (singleton).
    ///
    /// Design decisions (confirmed):
    /// - Global unique controller: only one WarheadController may exist per round,
    ///   therefore only one NukeSession at a time. Implemented via singleton + single ActiveSession field.
    /// - Operator eliminated → session destroyed, controller invalidated, round returns to normal.
    ///   No controller re-issuance, to prevent balance issues (e.g., teammate relay-launch).
    /// - SCP-079 is handled by the vanilla nuke logic (Warhead.Detonate) during global kill;
    ///   vanilla nuke already terminates 079, no separate handling needed.
    ///
    /// State transitions (see NukeState comments for details):
    ///   Idle       --(UsingItem)-->        Confirming
    ///   Confirming --(UsingItem in window)--> Locked
    ///   Confirming --(window timeout)-->    Idle
    ///   Locked     --(role reset done)-->   Counting
    ///   Counting   --(countdown reaches 0)--> Detonation
    ///   Counting   --(death before PoNR)-->   Destroy session
    ///   Counting   --(death after PoNR)-->    No interception, continue to Detonation
    /// </summary>
    public sealed class NukeSessionManager
    {
        #region Singleton

        private static NukeSessionManager _instance;
        public static NukeSessionManager Instance => _instance;

        public static NukeSessionManager Create()
        {
            if (_instance != null)
            {
                Log.Warn("[NukeSessionManager] Instance already exists, Create() called again — returning existing instance.");
                return _instance;
            }
            _instance = new NukeSessionManager();
            return _instance;
        }

        public static void Destroy()
        {
            if (_instance == null)
                return;
            _instance.Unsubscribe();
            _instance.ClearSession();
            _instance = null;
        }

        #endregion

        private CoroutineHandle _idlePanelPollHandle;
        private KeybindSetting _launchKeybind;

        /// <summary>
        /// Current active session. null = no session (Idle or controller not yet synthesized).
        /// Due to the global unique controller constraint, at most one active session at a time.
        /// </summary>
        public NukeSession ActiveSession { get; private set; }

        private NukeSessionManager()
        {
            Subscribe();
        }

        #region Event Subscriptions

        private void Subscribe()
        {
            Exiled.Events.Handlers.Player.Dying += OnDying;
            Exiled.Events.Handlers.Player.Left += OnLeft;
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEnded;
            Exiled.Events.Handlers.Server.RestartingRound += OnRestartingRound;

            // Polling replaces the ChangingItem event: only relies on the verified player.Items,
            // and handles "does the inventory contain the controller" panel display logic
            // (no longer requires the controller to be the currently held item).
            _idlePanelPollHandle = Timing.RunCoroutine(IdlePanelPollCoroutine());

            // Custom keybind: confirmation no longer depends on Use/Inspect (both verified as
            // not viable). Uses ServerSpecificSettings keybind system. Condition: "controller
            // is in inventory". Players can switch weapons and fight normally — no need to
            // hold the controller at all times.
            _launchKeybind = new KeybindSetting(
                id: 90001,
                label: Localization.KeybindLabel,
                suggested: KeyCode.K,
                preventInteractionOnGUI: true,
                allowSpectatorTrigger: false,
                hintDescription: Localization.KeybindHint,
                onChanged: OnLaunchKeybindChanged);

            SettingBase.Register(new List<SettingBase> { _launchKeybind });
        }

        private void Unsubscribe()
        {
            Exiled.Events.Handlers.Player.Dying -= OnDying;
            Exiled.Events.Handlers.Player.Left -= OnLeft;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEnded;
            Exiled.Events.Handlers.Server.RestartingRound -= OnRestartingRound;

            if (_idlePanelPollHandle.IsRunning)
                Timing.KillCoroutines(_idlePanelPollHandle);

            // Note: SettingBase currently has no known Unregister method. On plugin reload,
            // this may cause duplicate keybind registration. If duplicates/errors occur during
            // testing, investigate whether SettingBase has a corresponding unregister API.
            _launchKeybind = null;
        }

        /// <summary>
        /// Custom keybind callback. IsPressed fires OnChanged on both press and release;
        /// we only handle the press (rising edge), release does nothing.
        /// </summary>
        private void OnLaunchKeybindChanged(Player player, SettingBase setting)
        {
            if (player == null || !player.IsConnected || !player.IsAlive)
                return;

            if (!(setting is KeybindSetting keybind) || !keybind.IsPressed)
                return;

            if (WarheadController.ActiveInstance == null)
                return;

            // Condition: "controller is in inventory" — does not require currently held
            bool hasControllerInInventory = false;
            foreach (Item item in player.Items)
            {
                if (WarheadController.ActiveInstance.Check(item))
                {
                    hasControllerInInventory = true;
                    break;
                }
            }

            if (!hasControllerInInventory)
                return;

            WarheadController.ActiveInstance.HandleUse(player);
        }

        /// <summary>
        /// Checks every 0.5s whether each online player has the controller in inventory,
        /// showing/hiding the panel accordingly. No longer requires "currently held" —
        /// players can fight normally while the panel remains visible.
        /// </summary>
        private IEnumerator<float> IdlePanelPollCoroutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(0.5f);

                if (WarheadController.ActiveInstance == null)
                    continue;

                foreach (Player player in Player.List)
                {
                    if (player == null || !player.IsConnected)
                        continue;

                    bool isCurrentlyIdleOrNoSession =
                        ActiveSession == null ||
                        ActiveSession.Operator != player ||
                        ActiveSession.State == NukeState.Idle;

                    if (!isCurrentlyIdleOrNoSession)
                        continue; // Confirming and later phases are not affected by this poll

                    bool hasController = false;
                    foreach (Item item in player.Items)
                    {
                        if (WarheadController.ActiveInstance.Check(item))
                        {
                            hasController = true;
                            break;
                        }
                    }

                    bool panelCurrentlyShown = NukePanel.Instance?.IsShown(player) ?? false;

                    if (hasController && !panelCurrentlyShown)
                        NukePanel.Instance?.ShowPanel(player);
                    else if (!hasController && panelCurrentlyShown)
                        NukePanel.Instance?.HidePanel(player);
                }
            }
        }

        #endregion

        #region Confirm Action Entry (called by WarheadController.HandleUse)

        /// <summary>
        /// Handles the player's controller activation action.
        /// Decides between "first confirmation" and "second confirmation → start countdown"
        /// based on current session state.
        /// </summary>
        public void HandleConfirmAction(Player player)
        {
            if (ActiveSession == null)
            {
                // First confirmation: create session, enter Confirming
                ActiveSession = new NukeSession(player)
                {
                    State = NukeState.Confirming,
                    RemainingTime = Constants.CountdownTotalSeconds
                };

                // Defensive: ensure panel exists (normally the poll coroutine should already
                // have shown the Idle panel; this is a safety net for edge cases).
                NukePanel.Instance?.ShowPanel(player);
                NukePanel.Instance?.RenderConfirming(player, Constants.ConfirmWindowSeconds, Constants.ConfirmWindowSeconds);

                ActiveSession.ConfirmWindowTimer = Timing.RunCoroutine(
                    ConfirmWindowCoroutine(ActiveSession));

                Log.Info($"[NukeSession] {player.Nickname} first confirmation, entering Confirming state. " +
                         $"Window: {Constants.ConfirmWindowSeconds}s.");
                return;
            }

            // Session already exists
            if (ActiveSession.Operator != player)
            {
                // Should not happen (global unique controller); defensive handling
                Log.Warn("[NukeSession] Non-session-holder attempted confirmation — ignored.");
                return;
            }

            if (ActiveSession.State == NukeState.Confirming)
            {
                // Second confirmation: cancel confirmation window timer, enter Locked
                if (ActiveSession.ConfirmWindowTimer.IsRunning)
                    Timing.KillCoroutines(ActiveSession.ConfirmWindowTimer);

                TransitionToLocked(ActiveSession);
                return;
            }

            // Idle/Counting/Detonation states: activation has no effect
            // (Idle should have ActiveSession == null and won't reach here;
            //  Counting/Detonation should have the controller locked, activation is no-op)
        }

        #endregion

        #region State Transitions

        /// <summary>
        /// Confirmation window timeout coroutine: if no second confirmation before expiry,
        /// revert to Idle and destroy the session.
        /// Note: reverting to Idle means the session is destroyed, but the controller
        /// remains on the player — they can activate again to start a new confirmation flow.
        /// </summary>
        private IEnumerator<float> ConfirmWindowCoroutine(NukeSession session)
        {
            float remaining = Constants.ConfirmWindowSeconds;

            // Countdown each second, refresh the panel's remaining time display
            while (remaining > 0f)
            {
                if (ActiveSession != session || session.State != NukeState.Confirming)
                    yield break; // Already second-confirmed or destroyed

                yield return Timing.WaitForSeconds(1f);
                remaining -= 1f;

                if (ActiveSession != session || session.State != NukeState.Confirming)
                    yield break;

                NukePanel.Instance?.RenderConfirming(session.Operator, remaining, Constants.ConfirmWindowSeconds);
            }

            Log.Info($"[NukeSession] {session.Operator?.Nickname} confirmation window timed out, reverting to Idle.");
            NukePanel.Instance?.RenderIdle(session.Operator);
            ClearSession();
        }

        /// <summary>
        /// Locked → Counting transition.
        /// Resets the operator's role (detach from original team, set to Tutorial),
        /// then starts the countdown coroutine and location tracking broadcast.
        /// </summary>
        private void TransitionToLocked(NukeSession session)
        {
            session.State = NukeState.Locked;
            Player op = session.Operator;

            if (op == null || !op.IsConnected || !op.IsAlive)
            {
                Log.Warn("[NukeSession] Operator offline/dead during Locked transition, destroying session.");
                ClearSession();
                return;
            }

            // Role reset: detach from original team, set to Tutorial (tutorial-mode role,
            // part of no winning team, so the original team's win conditions no longer apply).
            // Key: RoleSpawnFlags.None skips default equipment assignment (AssignInventory) and
            // spawn point teleport (UseSpawnpoint), so inventory items (including the controller
            // itself) and current position are preserved.
            op.Role.Set(RoleTypeId.Tutorial, SpawnReason.ForceClass, RoleSpawnFlags.None);

            Log.Info($"[NukeSession] {op.Nickname} role reset to Tutorial, entering Locked.");

            NukePanel.Instance?.RenderLocked(op);

            // Immediately transition to Counting and start countdown
            TransitionToCounting(session);
        }

        /// <summary>
        /// Counting state: starts the main countdown coroutine and location tracking broadcast.
        /// </summary>
        private void TransitionToCounting(NukeSession session)
        {
            session.State = NukeState.Counting;
            session.RemainingTime = Constants.CountdownTotalSeconds;
            session.TotalTime = Constants.CountdownTotalSeconds;

            Player op = session.Operator;
            if (op == null)
            {
                ClearSession();
                return;
            }

            // Start main countdown coroutine
            session.Timer = Timing.RunCoroutine(CountdownCoroutine(session));

            // Start location tracking broadcast (countdown-phase frequency)
            TrackingBroadcaster.Instance?.StartCountingBroadcast(op, session, Constants.CountingTrackIntervalSeconds);

            // Show countdown UI to the operator (internally starts a per-second refresh coroutine)
            NukePanel.Instance?.ShowCounting(op, session);

            // Survival gear: from this moment the operator is hunted by the entire server.
            // Movement boost + MicroHID + heavy armor + E11-SR.
            op.EnableEffect(EffectType.MovementBoost, 80, 0f); // Duration 0 = permanent until Detonation cleanup
            op.EnableEffect(EffectType.RainbowTaste, 80, 0f);
            op.EnableEffect(EffectType.Scp1853, 80, 0f);
            op.AddItem(ItemType.MicroHID);
            op.AddItem(ItemType.ArmorHeavy);
            op.AddItem(ItemType.GunE11SR);

            // Server-wide CASSIE alert (with subtitles)
            Exiled.API.Features.Cassie.Message(Localization.CassieLaunchSequence, false, false, true);

            Log.Info($"[NukeSession] {op.Nickname} entering Counting, countdown {Constants.CountdownTotalSeconds}s.");
        }

        /// <summary>
        /// Main countdown coroutine: decrements RemainingTime each second,
        /// marks point of no return when threshold is crossed,
        /// enters Detonation when countdown reaches zero.
        /// </summary>
        private IEnumerator<float> CountdownCoroutine(NukeSession session)
        {
            while (session.State == NukeState.Counting && session.RemainingTime > 0f)
            {
                yield return Timing.WaitForSeconds(1f);

                if (session.State != NukeState.Counting)
                    yield break; // Session was aborted

                session.RemainingTime -= 1f;

                // Mark point of no return
                if (!session.PointOfNoReturn &&
                    session.RemainingTime <= Constants.PointOfNoReturnSeconds)
                {
                    session.PointOfNoReturn = true;
                    Log.Info($"[NukeSession] Point of no return reached (remaining {session.RemainingTime:F1}s). " +
                             "Operator death will no longer abort the launch.");
                    Exiled.API.Features.Cassie.Message(Localization.CassiePointOfNoReturn, false, false, true);
                }

                // Countdown milestone announcements
                if (Math.Abs(session.RemainingTime - 60f) < 0.01f)
                    Exiled.API.Features.Cassie.Message("60 SECONDS", false, false, true);
                else if (Math.Abs(session.RemainingTime - 30f) < 0.01f)
                    Exiled.API.Features.Cassie.Message("30 SECONDS", false, false, true);
                else if (Math.Abs(session.RemainingTime - 10f) < 0.01f)
                    Exiled.API.Features.Cassie.Message("10 .", false, false, true);
            }

            // Countdown reached zero, enter Detonation
            if (session.State == NukeState.Counting)
            {
                TransitionToDetonation(session);
            }
        }

        /// <summary>
        /// Detonation: executes the global kill.
        /// Strategy: first call vanilla nuke Detonate (terminates 079 + visual/audio effects),
        /// then after DetonationKillDelaySeconds, manually kill all surviving players
        /// (vanilla nuke only destroys surface and some underground areas, not the entire facility).
        /// </summary>
        private void TransitionToDetonation(NukeSession session)
        {
            session.State = NukeState.Detonation;
            session.RemainingTime = 0f;

            Player op = session.Operator;
            Log.Info($"[NukeSession] OMEGA Warhead detonated! Operator: {op?.Nickname ?? "null"}");

            // Record launch in persistent stats (owinfo command)
            StatsTracker.RecordLaunch();

            // Stop location tracking
            if (op != null)
                TrackingBroadcaster.Instance?.StopCountingBroadcast(op);

            // Trigger vanilla nuke (terminates 079 + visual/audio effects)
            Exiled.API.Features.Warhead.Detonate();

            // Delayed global kill (ensures all players die; vanilla nuke doesn't cover every zone)
            Timing.RunCoroutine(GlobalKillCoroutine(session, Constants.DetonationKillDelaySeconds));
        }

        /// <summary>
        /// Global kill coroutine: after a delay, kills all surviving players, then ends the round.
        /// </summary>
        private IEnumerator<float> GlobalKillCoroutine(NukeSession session, float delay)
        {
            yield return Timing.WaitForSeconds(delay);

            Log.Info("[NukeSession] Executing global kill...");
            foreach (Player p in Player.List)
            {
                if (p is null || !p.IsAlive)
                    continue;

                // Use Kill instead of Hurt to ensure immediate death
                // Death cause: "OMEGA Warhead"
                p.Kill(Configs.Localization.KillReasonWarhead);
            }

            // Brief delay before cleanup
            yield return Timing.WaitForSeconds(1f);

            // Invalidate controller and clean session
            WarheadController.ActiveInstance?.Invalidate();
            ClearSession();

            // Note: we intentionally do NOT call Round.Restart() here.
            // Previously, killing all players and immediately forcing a restart would race with
            // the game engine's own "no survivors → auto-end round" native logic, which is
            // suspected to cause players to be kicked from the server instead of proceeding
            // normally to the next round. Now we only handle the kills and let the native
            // logic handle round end.
            // If testing reveals the round actually gets stuck and doesn't end (i.e., the
            // native logic fails to recognize this "all dead" scenario), consider adding
            // a forced end, but in a safer way (e.g., only as a fallback when RoundEnded
            // is confirmed not to have fired).
        }

        #endregion

        #region Death / Disconnect Handling

        /// <summary>
        /// Operator death handling:
        /// - Confirming/Locked phase death → destroy session directly, invalidate controller
        /// - Counting phase, before PoNR → abort launch, destroy session, invalidate controller
        /// - Counting phase, after PoNR → no interception, launch continues
        /// - Detonation phase → no interception (already in global kill flow)
        /// </summary>
        private void OnDying(DyingEventArgs ev)
        {
            if (ActiveSession == null || ev.Player != ActiveSession.Operator)
                return;

            NukeSession session = ActiveSession;

            // Past PoNR or already detonating: do not intercept
            if (session.IsIrreversible || session.State == NukeState.Detonation)
            {
                Log.Info($"[NukeSession] Operator {ev.Player.Nickname} died in irreversible phase, launch continues.");
                return;
            }

            // Abortable phase: destroy session, invalidate controller
            Log.Info($"[NukeSession] Operator {ev.Player.Nickname} died in abortable phase " +
                     $"(state={session.State}), aborting launch, invalidating controller.");

            // Stop tracking and UI
            TrackingBroadcaster.Instance?.StopCountingBroadcast(ev.Player);
            NukePanel.Instance?.HideAllUI(ev.Player);

            // Kill countdown coroutines
            if (session.Timer.IsRunning)
                Timing.KillCoroutines(session.Timer);
            if (session.ConfirmWindowTimer.IsRunning)
                Timing.KillCoroutines(session.ConfirmWindowTimer);

            // Invalidate controller (removes item from corpse)
            WarheadController.ActiveInstance?.Invalidate();
            ClearSession();

            // CASSIE abort announcement (with subtitles)
            Exiled.API.Features.Cassie.Message(Localization.CassieAborted, false, false, true);
        }

        /// <summary>
        /// Operator disconnect: same logic as death — abort in abortable phases.
        /// </summary>
        private void OnLeft(LeftEventArgs ev)
        {
            if (ActiveSession == null || ev.Player != ActiveSession.Operator)
                return;

            NukeSession session = ActiveSession;

            if (session.IsIrreversible || session.State == NukeState.Detonation)
            {
                Log.Info($"[NukeSession] Operator {ev.Player.Nickname} disconnected in irreversible phase, launch continues.");
                // After disconnect the Operator reference is retained but the player object
                // is invalid; the countdown coroutine will continue to Detonation.
                return;
            }

            Log.Info($"[NukeSession] Operator {ev.Player.Nickname} disconnected in abortable phase, aborting launch.");

            TrackingBroadcaster.Instance?.StopCountingBroadcast(ev.Player);
            if (session.Timer.IsRunning)
                Timing.KillCoroutines(session.Timer);
            if (session.ConfirmWindowTimer.IsRunning)
                Timing.KillCoroutines(session.ConfirmWindowTimer);

            WarheadController.ActiveInstance?.Invalidate();
            ClearSession();

            Exiled.API.Features.Cassie.Message(Localization.CassieAborted, false, false, true);
        }

        #endregion

        #region Round End / Restart Cleanup

        private void OnRoundEnded(RoundEndedEventArgs ev)
        {
            ClearSession();
        }

        private void OnRestartingRound()
        {
            ClearSession();
        }

        /// <summary>
        /// Clears the current session: kills all coroutines, clears references.
        /// Does not invalidate the controller (caller decides whether to invalidate).
        /// </summary>
        public void ClearSession()
        {
            if (ActiveSession == null)
                return;

            NukeSession session = ActiveSession;
            if (session.Timer.IsRunning)
                Timing.KillCoroutines(session.Timer);
            if (session.ConfirmWindowTimer.IsRunning)
                Timing.KillCoroutines(session.ConfirmWindowTimer);

            // Hide UI
            if (session.Operator != null)
            {
                TrackingBroadcaster.Instance?.StopCountingBroadcast(session.Operator);
                NukePanel.Instance?.HideAllUI(session.Operator);
            }

            ActiveSession = null;
            Log.Info("[NukeSession] Session cleared.");
        }

        #endregion
    }
}
