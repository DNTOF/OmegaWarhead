using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Spawn;
using Exiled.API.Enums;
using Exiled.CustomItems.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using OmegaWarhead.Configs;

namespace OmegaWarhead.Items
{
    /// <summary>
    /// Radioactive Element: a collectible item spawned in random rooms on the map.
    /// Holding it deals damage-per-element-per-second. Once Config.RequiredElementCount
    /// is reached, OmegaWarheadPlugin (subscribed to CountChanged) removes them and
    /// issues a Warhead Launch Controller.
    ///
    /// SpawnProperties is in the Exiled.API.Features.Spawn namespace (not
    /// Exiled.CustomItems.API.Features as incorrectly referenced in a previous version).
    /// </summary>
    public class RadioactiveElement : CustomItem
    {
        public override uint Id { get; set; } = 10001;

        public override string Name { get; set; } = "Radioactive Element";

        public override string Description { get; set; } =
            "A nuclear fuel fragment emitting faint radiation. Prolonged exposure is hazardous to health.";

        public override float Weight { get; set; } = 0.5f;
        public override ItemType Type { get; set; } = ItemType.Coin;

        // Random room spawning: the main plugin selects random rooms from the
        // current map's room list at OnRoundStarted and creates DynamicSpawnPoint,
        // writing to this. No fixed coordinates — aligns with the "random room spawn" design.
        public override SpawnProperties SpawnProperties { get; set; } = new SpawnProperties();

        /// <summary>
        /// Tracks each player's current held element count, read by the DamageTicker coroutine and HSM panel.
        /// </summary>
        public static readonly Dictionary<Player, int> HeldCount = new Dictionary<Player, int>();

        /// <summary>
        /// Held count change notification: fired once after each RecalculateCount completes.
        /// This is the single authoritative signal source for "count changed" —
        /// OmegaWarheadPlugin's synthesis check and TrackingBroadcaster's collecting
        /// broadcast start/stop should all subscribe to this event, rather than
        /// independently re-subscribing to PickingUpItem/DroppingItem and re-scanning
        /// inventory (as a previous version did, causing two sets of logic each
        /// maintaining their own delayed coroutines with race conditions and duplicate calculations).
        /// </summary>
        public static event Action<Player, int> CountChanged;

        private CoroutineHandle _damageTickerHandle;

        protected override void SubscribeEvents()
        {
            // Pickup/drop both maintain HeldCount. RecalculateCount internally fires CountChanged,
            // which OmegaWarheadPlugin subscribes to for synthesis detection — this is the single
            // authoritative path; Plugin no longer re-scans inventory independently.
            Exiled.Events.Handlers.Player.PickingUpItem += HandlePickingUpItemEvent;
            Exiled.Events.Handlers.Player.DroppingItem += HandleDroppingItemEvent;
            Exiled.Events.Handlers.Player.Died += OnPlayerDied;
            Exiled.Events.Handlers.Player.Left += OnPlayerLeft;

            _damageTickerHandle = Timing.RunCoroutine(DamageTicker());

            base.SubscribeEvents();
        }

        protected override void UnsubscribeEvents()
        {
            Exiled.Events.Handlers.Player.PickingUpItem -= HandlePickingUpItemEvent;
            Exiled.Events.Handlers.Player.DroppingItem -= HandleDroppingItemEvent;
            Exiled.Events.Handlers.Player.Died -= OnPlayerDied;
            Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;

            Timing.KillCoroutines(_damageTickerHandle);
            HeldCount.Clear();

            base.UnsubscribeEvents();
        }

        // NOTE: these handlers are deliberately NOT named OnPickingUpItem / OnDroppingItem —
        // CustomItem's base class declares virtual methods with those same names (legacy hooks
        // for subclass overrides). If our private event callbacks used the exact same name/signature,
        // the compiler would emit a "hides inherited member" warning (CS0114), easily confused
        // with a genuine override. Renaming avoids this.
        private void HandlePickingUpItemEvent(PickingUpItemEventArgs ev)
        {
            if (!Check(ev.Pickup))
                return;

            // At the moment of pickup, the inventory hasn't yet actually included the item;
            // delaying by one frame yields a more accurate count.
            Timing.CallDelayed(0.1f, () => RecalculateCount(ev.Player));
        }

        private void HandleDroppingItemEvent(DroppingItemEventArgs ev)
        {
            if (!Check(ev.Item))
                return;

            Timing.CallDelayed(0.1f, () => RecalculateCount(ev.Player));
        }

        private void OnPlayerDied(DiedEventArgs ev)
        {
            HeldCount.Remove(ev.Player);
        }

        private void OnPlayerLeft(LeftEventArgs ev)
        {
            HeldCount.Remove(ev.Player);
        }

        /// <summary>
        /// Recalculates the actual number of this element in a player's inventory.
        /// </summary>
        internal void RecalculateCount(Player player)
        {
            if (player is null || !player.IsConnected)
                return;

            int count = 0;
            foreach (Item item in player.Items)
            {
                if (Check(item))
                    count++;
            }
            Log.Info($"[RadioactiveElement] {player.Nickname} inventory scan complete: found {count} elements (total items in bag: {player.Items.Count}).");
            if (count <= 0)
                HeldCount.Remove(player);
            else
                HeldCount[player] = count;

            CountChanged?.Invoke(player, count);
        }

        /// <summary>
        /// Called during controller synthesis: removes all RadioactiveElement instances
        /// from the player's inventory at once and clears poison/damage state.
        /// </summary>
        public void ClearFromInventory(Player player)
        {
            List<Item> toRemove = new List<Item>();
            foreach (Item item in player.Items)
            {
                if (Check(item))
                    toRemove.Add(item);
            }

            foreach (Item item in toRemove)
                player.RemoveItem(item);

            HeldCount.Remove(player);
        }

        /// <summary>
        /// Damage ticker: deals damage once per second = held count * Config.DamagePerElementPerSecond.
        /// E.g., holding 4 elements at 1 damage/element/s = 4 damage per second, linear relationship.
        /// </summary>
        private IEnumerator<float> DamageTicker()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);

                if (HeldCount.Count == 0)
                    continue;

                float perElement = OmegaWarheadPlugin.Instance.Config.DamagePerElementPerSecond;

                // Snapshot keys to avoid modification during iteration (death/disconnect etc.)
                List<Player> holders = new List<Player>(HeldCount.Keys);
                foreach (Player player in holders)
                {
                    if (player is null || !player.IsConnected || !player.IsAlive)
                        continue;

                    if (!HeldCount.TryGetValue(player, out int count) || count <= 0)
                        continue;

                    float damage = count * perElement;
                    if (damage > 0f)
                        player.Hurt(damage, "Radiation Poisoning");

                    // Maintain a uniform-intensity Poisoned effect as a screen icon indicator only;
                    // actual damage is fully controlled by the manual Hurt() above, not the effect's
                    // internal damage curve.
                    player.EnableEffect(EffectType.Poisoned, 1, 1.5f, true);
                }
            }
        }
    }
}
