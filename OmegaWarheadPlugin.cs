using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.CustomItems.API.Features;
using MEC;
using OmegaWarhead.Configs;
using OmegaWarhead.Core;
using OmegaWarhead.Items;
using OmegaWarhead.UI;
using Player = Exiled.API.Features.Player;

namespace OmegaWarhead
{
    /// <summary>
    /// OMEGA Warhead Launch Controller — Plugin Entry Point.
    ///
    /// Design Notes:
    /// - Inherits EXILED's Plugin&lt;TConfig&gt;, where TConfig is Configs/Config.cs.
    /// - Exposes Config to other classes via the Instance singleton (RadioactiveElement.DamageTicker etc.).
    /// - OnEnabled: registers CustomItems, subscribes to round events, initializes managers.
    /// - OnDisabled: reverses cleanup, ensuring hot-reload safety.
    ///
    /// Global Unique Controller Constraint (confirmed):
    /// Only one WarheadController may exist per round. TryGiveTo checks
    /// WarheadController.ActiveInstance and ignores duplicate synthesis requests.
    /// </summary>
    public sealed class OmegaWarheadPlugin : Plugin<Config>
    {
        // ------------------- Singleton -------------------

        /// <summary>
        /// Global singleton. RadioactiveElement.DamageTicker reads
        /// OmegaWarheadPlugin.Instance.Config for damage coefficients,
        /// so this field must be assigned first thing in OnEnabled.
        /// </summary>
        public static OmegaWarheadPlugin Instance { get; private set; }

        // ------------------- Metadata -------------------

        public override string Name => "OmegaWarhead";

        public override string Author => "DNT_OF";

        public override Version Version => new Version(1, 0, 0);

        /// <summary>
        /// Minimum required EXILED version. SCP:SL 14.2.x corresponds to EXILED 9.x.
        /// Set 9.0.0 as the lower bound; adjust to your local EXILED version if needed.
        /// </summary>
        public override Version RequiredExiledVersion => new Version(9, 0, 0);

        // ------------------- Subsystem References -------------------

        /// <summary>Nuke session manager: state machine transitions, death handling, global kill.</summary>
        public NukeSessionManager SessionManager { get; private set; }

        /// <summary>Tracking broadcaster: collecting/countdown frequencies share the same logic.</summary>
        public TrackingBroadcaster TrackingBroadcaster { get; private set; }

        /// <summary>Random room spawn point selector.</summary>
        public SpawnPointSelector SpawnPointSelector { get; private set; }

        /// <summary>Nuke UI panel (HSM-based).</summary>
        public NukePanel NukePanel { get; private set; }

        // ------------------- Custom Items -------------------

        /// <summary>RadioactiveElement CustomItem instance reference, used by SpawnPointSelector / synthesis logic.</summary>
        public RadioactiveElement RadioactiveElementItem { get; private set; }

        /// <summary>WarheadController CustomItem instance reference.</summary>
        public WarheadController WarheadControllerItem { get; private set; }

        /// <summary>
        /// Snapshot of current RadioactiveElement holdings: Player → count.
        /// Exposed for external plugins (e.g., SLDataAPI) to read via reflection.
        /// Internal logic should use RadioactiveElement.HeldCount directly.
        /// </summary>
        public IReadOnlyDictionary<Player, int> ElementHolders => RadioactiveElement.HeldCount;

        // ------------------- Coroutine Handles -------------------

        private List<CoroutineHandle> _roundCoroutines = new List<CoroutineHandle>();

        // ------------------- Lifecycle -------------------

        public override void OnEnabled()
        {
            // Assign singleton immediately — downstream registration reads Instance.Config
            Instance = this;

            Log.Info("OMEGA Warhead Launch Controller enabling...");

            // 1. Register CustomItems (manual instantiation + Register for instance references)
            RadioactiveElementItem = new RadioactiveElement();
            RadioactiveElementItem.Init();

            WarheadControllerItem = new WarheadController();
            WarheadControllerItem.Init();

            // 2. Initialize subsystems (singletons; Create() subscribes to events internally)
            NukeSessionManager.Create(Config);
            TrackingBroadcaster.Create();
            SpawnPointSelector.Create();
            NukePanel.Create();

            // Hold references for external access
            SessionManager = NukeSessionManager.Instance;
            TrackingBroadcaster = TrackingBroadcaster.Instance;
            SpawnPointSelector = SpawnPointSelector.Instance;
            NukePanel = NukePanel.Instance;

            // 3. Subscribe to round events
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEnded;
            Exiled.Events.Handlers.Server.RestartingRound += OnRestartingRound;

            // 4. Subscribe to element count changes for synthesis triggering
            //    (RadioactiveElement's own SubscribeEvents handles pickup/drop counting;
            //    we only need to react when the count crosses the synthesis threshold.)
            RadioactiveElement.CountChanged += OnElementCountChanged;

            base.OnEnabled();
            Log.Info("OMEGA Warhead Launch Controller enabled.");
        }

        public override void OnDisabled()
        {
            // Reverse cleanup: unsubscribe events → kill coroutines → destroy subsystems → unregister items
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEnded;
            Exiled.Events.Handlers.Server.RestartingRound -= OnRestartingRound;
            RadioactiveElement.CountChanged -= OnElementCountChanged;

            KillAllRoundCoroutines();

            // Destroy subsystems (internally unsubscribes events + kills coroutines)
            NukePanel.Destroy();
            SpawnPointSelector.Destroy();
            TrackingBroadcaster.Destroy();
            NukeSessionManager.Destroy();

            WarheadControllerItem.Destroy();
            RadioactiveElementItem?.Destroy();

            RadioactiveElementItem = null;
            WarheadControllerItem = null;
            SessionManager = null;
            TrackingBroadcaster = null;
            SpawnPointSelector = null;
            NukePanel = null;

            Instance = null;

            base.OnDisabled();
            Log.Info("OMEGA Warhead Launch Controller disabled.");
        }

        // ------------------- Round Event Callbacks -------------------

        /// <summary>
        /// Round start: spawn RadioactiveElements in random rooms on the map.
        /// Actual spawn logic is handled by SpawnPointSelector; this only schedules it.
        /// </summary>
        private void OnRoundStarted()
        {
            Log.Info("Round started, spawning radioactive elements...");

            // Clear residual element count from previous round
            RadioactiveElement.HeldCount.Clear();

            // Start spawn loop
            SpawnPointSelector?.StartSpawnLoop(
                RadioactiveElementItem,
                Config.MaxSpawnedElements,
                Config.ElementRespawnDelay);
        }

        /// <summary>
        /// Round end: clean up all sessions and coroutines to prevent state carry-over.
        /// </summary>
        private void OnRoundEnded(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            Log.Info("Round ended, cleaning OMEGA sessions...");
            KillAllRoundCoroutines();
            SpawnPointSelector?.StopAll();
            TrackingBroadcaster?.StopAll();
            NukePanel?.HideAllForAll();
            // NukeSessionManager subscribes to RoundEnded and will clear ActiveSession
        }

        /// <summary>
        /// Round restart: similar cleanup to OnRoundEnded, but more thorough (players may still be present).
        /// </summary>
        private void OnRestartingRound()
        {
            KillAllRoundCoroutines();
            SpawnPointSelector?.StopAll();
            TrackingBroadcaster?.StopAll();
            NukePanel?.HideAllForAll();
            RadioactiveElement.HeldCount.Clear();
            // NukeSessionManager subscribes to RestartingRound
        }

        // ------------------- Element Count Change Handler (Synthesis Detection) -------------------

        /// <summary>
        /// Reacts to RadioactiveElement.CountChanged: starts/stops collecting broadcast,
        /// checks if synthesis threshold is reached. This is the single handler for
        /// "what to do when count changes" — RadioactiveElement itself only maintains
        /// HeldCount and fires this event, without any business logic.
        /// </summary>
        private void OnElementCountChanged(Player player, int count)
        {
            if (player == null || !player.IsConnected || !player.IsAlive)
                return;

            if (count > 0)
            {
                bool wasAlreadyTracking = TrackingBroadcaster?.IsCollectingTracked(player) ?? false;
                TrackingBroadcaster?.StartCollectingBroadcast(player, Config.CollectingTrackIntervalSeconds);
                if (!wasAlreadyTracking)
                    NukeEvents.RaiseCollectingStarted(player); // only fire once when going from 0 to >0
            }
            else
            {
                TrackingBroadcaster?.StopCollectingBroadcast(player);
            }

            if (count < Config.RequiredElementCount)
                return;

            Log.Info($"[Synthesize] {player.Nickname} collected {count} radioactive elements, " +
                     "synthesizing OMEGA Warhead Launch Controller.");

            // Consume all elements (ClearFromInventory is the single authoritative method
            // for removing elements + clearing HeldCount)
            RadioactiveElementItem.ClearFromInventory(player);
            TrackingBroadcaster?.StopCollectingBroadcast(player);

            // Issue controller (global unique; TryGiveTo checks internally)
            bool given = WarheadControllerItem?.TryGiveTo(player) ?? false;
            if (!given)
            {
                Log.Warn("[Synthesize] Controller issuance failed (active instance already exists). " +
                         "Elements consumed but controller not issued.");
                player.Broadcast(5, "Elements collected, but a controller already exists on the field. Synthesis failed.");
            }
            else
            {
                player.Broadcast(5, "<color=#ff0000><b>OMEGA Warhead Launch Controller synthesized!</b></color>\n" +
                                   "Use the controller to confirm launch.");
                Exiled.API.Features.Cassie.Message("WARNING . OMEGA WARHEAD COMPONENT ASSEMBLED", false, false);
                NukeEvents.RaiseControllerAssembled(player);
            }
        }

        // ------------------- Utilities -------------------

        private void KillAllRoundCoroutines()
        {
            foreach (CoroutineHandle handle in _roundCoroutines)
                Timing.KillCoroutines(handle);
            _roundCoroutines.Clear();
        }
    }
}
