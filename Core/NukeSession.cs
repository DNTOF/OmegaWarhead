using System;
using Exiled.API.Features;
using MEC;

namespace OmegaWarhead.Core
{
    /// <summary>
    /// OMEGA Warhead Launch state machine enum.
    ///
    /// State transitions:
    ///   Idle       → (activate device) →   Confirming
    ///   Confirming → (activate again within window) → Locked
    ///   Confirming → (window timeout) →   Idle
    ///   Locked     → (role reset complete) → Counting
    ///   Counting   → (countdown reaches zero) → Detonation
    ///   Counting   → (death before point of no return) → Abort (session destroyed, no Detonation)
    ///   Counting   → (death after point of no return) → No interception, continue to Detonation
    ///
    /// Note: the Collecting phase is not in this state machine — it is implicitly
    /// represented by RadioactiveElement's HeldCount dictionary. Once enough elements
    /// are gathered, the synthesis logic issues a WarheadController to the player,
    /// at which point the player "holds the controller but hasn't activated it" = Idle.
    /// </summary>
    public enum NukeState
    {
        /// <summary>Standby: player holds the launch controller but hasn't activated it.</summary>
        Idle,

        /// <summary>Confirming: activated once, waiting for a second activation within Constants.ConfirmWindowSeconds.</summary>
        Confirming,

        /// <summary>Locked: role has been reset to Tutorial, about to enter countdown.</summary>
        Locked,

        /// <summary>Counting down: internal timer coroutine running, location tracking active.</summary>
        Counting,

        /// <summary>Detonation complete: countdown reached zero, global kill executed.</summary>
        Detonation
    }

    /// <summary>
    /// Data carrier for a single nuke launch session.
    ///
    /// Design notes:
    /// - One NukeSession corresponds to one operator.
    /// - Whether multiple simultaneous sessions are allowed is decided by NukeSessionManager's
    ///   management strategy. This data structure itself does not enforce singleton or multi-instance.
    /// - All coroutine handles are stored in the session for unified cleanup on session destruction.
    /// </summary>
    public sealed class NukeSession
    {
        /// <summary>
        /// The launch operator. Player reference should not change during the session
        /// (even after role is reset to Tutorial).
        /// </summary>
        public Player Operator { get; }

        /// <summary>Current state machine phase.</summary>
        public NukeState State { get; set; } = NukeState.Idle;

        /// <summary>
        /// Remaining countdown seconds (only meaningful during Counting phase).
        /// Decremented each second by the countdown coroutine; the HSM panel reads this to render the progress bar.
        /// </summary>
        public float RemainingTime { get; set; }

        /// <summary>
        /// Snapshot of total countdown duration (from Constants.CountdownTotalSeconds).
        /// Stored here for HSM progress bar percentage calculation (RemainingTime / TotalTime),
        /// avoiding a Config read on every frame.
        /// </summary>
        public float TotalTime { get; set; }

        /// <summary>
        /// Whether the point of no return has been crossed (RemainingTime &lt;= Constants.PointOfNoReturnSeconds).
        /// Once true, operator death will not abort the launch.
        /// </summary>
        public bool PointOfNoReturn { get; set; }

        /// <summary>
        /// Main countdown coroutine handle (running during Counting phase).
        /// Killed by Manager via Timing.KillCoroutines(Timer) on session destruction.
        /// </summary>
        public CoroutineHandle Timer { get; set; }

        /// <summary>
        /// Confirmation window timeout coroutine handle (running during Confirming phase).
        /// On timeout, reverts state to Idle.
        /// </summary>
        public CoroutineHandle ConfirmWindowTimer { get; set; }

        /// <summary>
        /// Session creation timestamp, for logging/debugging.
        /// </summary>
        public DateTime CreatedAt { get; } = DateTime.UtcNow;

        public NukeSession(Player op)
        {
            Operator = op ?? throw new ArgumentNullException(nameof(op));
        }

        /// <summary>
        /// Whether the session is in an irreversible phase (Counting and past point of no return).
        /// Death handling branches use this to decide whether to intercept.
        /// </summary>
        public bool IsIrreversible =>
            State == NukeState.Counting && PointOfNoReturn;

        /// <summary>
        /// Whether the session can be aborted by operator death.
        /// i.e., Counting but not yet past point of no return.
        /// (Idle/Confirming/Locked phase deaths destroy the session directly and don't go through "abort launch" branch.)
        /// </summary>
        public bool IsAbortableByDeath =>
            State == NukeState.Counting && !PointOfNoReturn;

        public override string ToString()
        {
            return $"[NukeSession Operator={Operator?.Nickname} State={State} " +
                   $"Remaining={RemainingTime:F1}s PointOfNoReturn={PointOfNoReturn}]";
        }
    }
}
