using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Spawn;
using Exiled.CustomItems.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using MEC;
using OmegaWarhead.Core;
using Player = Exiled.API.Features.Player;
using Item = Exiled.API.Features.Items.Item;
namespace OmegaWarhead.Items
{
    /// <summary>
    /// OMEGA Warhead Launch Controller: a special item synthesized after collecting
    /// enough RadioactiveElements.
    ///
    /// Confirmation mechanism (confirmed: Option A):
    /// Since EXILED's current master branch does not wrap SCP:SL 14.2.6's Inspect event
    /// (verified: ExMod-Team/EXILED Exiled.Events/Handlers/Player.cs has 124 events, none are Inspect*),
    /// we use a custom keybind as the confirmation action, achieving the "double confirmation" feel:
    ///   First activation → enters Confirming state, starts ConfirmWindowSeconds countdown
    ///   Second activation within window → enters Locked → Counting (countdown begins)
    ///   Window timeout without second activation → reverts to Idle
    ///
    /// Global uniqueness (confirmed):
    /// Only one controller may exist per round. NukeSessionManager is a singleton;
    /// once a player holds the controller and enters a session, no other player
    /// can obtain a second one. If the operator is eliminated during countdown →
    /// session destroyed, controller invalidated (no re-issuance), round returns to
    /// normal — prevents balance issues (e.g., teammate relay-launch).
    /// </summary>
    public class WarheadController : CustomItem
    {
        public override uint Id { get; set; } = 10002;

        public override string Name { get; set; } = "OMEGA Warhead Launch Controller";

        public override string Description { get; set; } =
            "A nuclear launch device — one step away from victory. " +
            "Activate again within the confirmation window to initiate the irreversible countdown.";

        public override float Weight { get; set; } = 1.5f;

        // Controller is never randomly spawned via SpawnProperties; only issued by synthesis logic.
        public override SpawnProperties SpawnProperties { get; set; }
        public override ItemType Type { get; set; } = ItemType.KeycardChaosInsurgency;

        /// <summary>
        /// Static reference: the current globally unique controller instance.
        /// Set by NukeSessionManager via TryGiveTo(player) after synthesis,
        /// cleared on session destruction (operator death/disconnect/detonation complete).
        /// </summary>
        public static WarheadController ActiveInstance { get; internal set; }

        /// <summary>
        /// The player currently holding the controller. null = not held by anyone
        /// (theoretically shouldn't happen after issuance since session destruction
        /// removes the item synchronously; retained for defensive checks).
        /// </summary>
        public static Player CurrentHolder { get; internal set; }

        public WarheadController()
        {
            // Event subscriptions happen during construction to avoid duplicates.
            // Note: CustomItem instance lifetime matches the plugin (created once in OnEnabled),
            // so subscribed events are implicitly cleaned up via Unregister in OnDisabled.
            // However, for clarity, explicit subscribe/unsubscribe is managed in NukeSessionManager.
        }

        /// <summary>
        /// Triggered when the player activates the controller.
        /// This is the confirmation action entry point, dispatched from
        /// NukeSessionManager's keybind callback. Actual state machine transitions
        /// are implemented in the Manager; this method only does a quick "is this the controller" check.
        /// </summary>
        internal void HandleUse(Player player)
        {
            if (player is null || !player.IsAlive)
                return;

            // Defensive: only the current holder can trigger
            if (CurrentHolder != player)
            {
                Log.Warn($"[WarheadController] Non-holder {player.Nickname} attempted to use controller — ignored. " +
                         "This typically indicates item sync state anomaly; check CustomItem GiveTo logic.");
                return;
            }

            NukeSessionManager.Instance?.HandleConfirmAction(player);
        }

        /// <summary>
        /// Issues the controller to the specified player. Global unique — checks
        /// whether an active instance already exists before issuing.
        /// Returns true = issued successfully, false = controller already exists (synthesis request ignored).
        /// </summary>
        public bool TryGiveTo(Player player)
        {
            if (ActiveInstance != null)
            {
                Log.Info("[WarheadController] Active controller already exists, refusing to issue new instance (global unique constraint).");
                return false;
            }

            ActiveInstance = this;
            CurrentHolder = player;
            Give(player);
            Log.Info($"[WarheadController] Controller issued to {player.Nickname} ({player.UserId}).");
            return true;
        }

        /// <summary>
        /// Invalidates the controller: removes the item from the holder, clears static references.
        /// Called by NukeSessionManager on session destruction (death/disconnect/detonation complete).
        /// After invalidation, no controller exists for this round — aligns with
        /// "eliminated → return to normal round" design.
        /// </summary>
        public void Invalidate()
        {
            if (CurrentHolder != null && CurrentHolder.IsConnected)
            {
                // Remove controller item instances from holder
                List<Exiled.API.Features.Items.Item> toRemove = new List<Exiled.API.Features.Items.Item>();
                foreach (Exiled.API.Features.Items.Item item in CurrentHolder.Items)
                {
                    if (Check(item))
                        toRemove.Add(item);
                }
                foreach (Exiled.API.Features.Items.Item item in toRemove)
                    CurrentHolder.RemoveItem(item);
            }

            Log.Info($"[WarheadController] Controller invalidated. Previous holder: {CurrentHolder?.Nickname ?? "null"}");
            CurrentHolder = null;
            ActiveInstance = null;
        }
    }
}
