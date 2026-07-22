using Exiled.API.Features;

namespace OmegaWarhead.Core
{
    /// <summary>
    /// Public status hooks for the OMEGA Warhead Launch System.
    ///
    /// Purpose: provide third-party plugins (e.g., a future BGM extension using
    /// SecretLabNAudio or similar libraries) with a stable subscription point,
    /// without requiring OmegaWarhead to depend on those plugins.
    /// Whether third-party extensions are installed/enabled has zero impact on
    /// OmegaWarhead's own operation — pure one-way broadcast.
    ///
    /// Usage example (in a third-party plugin):
    /// <code>
    /// OmegaWarhead.Core.NukeEvents.CountingStarted += (session) =>
    /// {
    ///     // Play countdown BGM here
    /// };
    /// OmegaWarhead.Core.NukeEvents.PointOfNoReturnReached += (session) =>
    /// {
    ///     // Switch to climax BGM
    /// };
    /// </code>
    ///
    /// Note: third-party plugins that reference OmegaWarhead.Core.NukeEvents need to
    /// add OmegaWarhead.dll as a compile-time reference (but do not need it at runtime
    /// as long as they guard access when OmegaWarhead is not loaded). This is the
    /// standard pattern for optional inter-plugin dependencies.
    /// </summary>
    public static class NukeEvents
    {
        /// <summary>Fired when a player starts collecting radioactive elements (held count goes from 0 to &gt;0).</summary>
        public static event System.Action<Player> CollectingStarted;

        /// <summary>Fired when a player has collected enough elements and the controller is successfully synthesized.</summary>
        public static event System.Action<Player> ControllerAssembled;

        /// <summary>Fired when the operator first activates the controller, entering the Confirming state.</summary>
        public static event System.Action<NukeSession> ConfirmingStarted;

        /// <summary>Fired when the confirmation window expires without a second activation, returning to Idle.</summary>
        public static event System.Action<Player> ConfirmingTimedOut;

        /// <summary>Fired when the second confirmation succeeds, the operator's role is reset to Tutorial, entering Locked.</summary>
        public static event System.Action<NukeSession> Locked;

        /// <summary>Fired at the exact moment the countdown phase begins ("the nuke has started counting down").</summary>
        public static event System.Action<NukeSession> CountingStarted;

        /// <summary>Fired once when the remaining countdown time crosses the point-of-no-return threshold.</summary>
        public static event System.Action<NukeSession> PointOfNoReturnReached;

        /// <summary>
        /// Fired when the launch is aborted before the point of no return (operator dies/disconnects).
        /// The round continues normally; third-party plugins can switch BGM back here.
        /// </summary>
        public static event System.Action<Player> LaunchAborted;

        /// <summary>Fired when the countdown reaches zero, immediately before the global kill (detonation moment).</summary>
        public static event System.Action<NukeSession> Detonating;

        /// <summary>Fired after the global kill has been executed, just before the round ends.</summary>
        public static event System.Action<NukeSession> DetonationCompleted;

        // ------------------- Internal Raise Methods (OmegaWarhead only) -------------------

        internal static void RaiseCollectingStarted(Player player) => CollectingStarted?.Invoke(player);
        internal static void RaiseControllerAssembled(Player player) => ControllerAssembled?.Invoke(player);
        internal static void RaiseConfirmingStarted(NukeSession session) => ConfirmingStarted?.Invoke(session);
        internal static void RaiseConfirmingTimedOut(Player player) => ConfirmingTimedOut?.Invoke(player);
        internal static void RaiseLocked(NukeSession session) => Locked?.Invoke(session);
        internal static void RaiseCountingStarted(NukeSession session) => CountingStarted?.Invoke(session);
        internal static void RaisePointOfNoReturnReached(NukeSession session) => PointOfNoReturnReached?.Invoke(session);
        internal static void RaiseLaunchAborted(Player player) => LaunchAborted?.Invoke(player);
        internal static void RaiseDetonating(NukeSession session) => Detonating?.Invoke(session);
        internal static void RaiseDetonationCompleted(NukeSession session) => DetonationCompleted?.Invoke(session);
    }
}
