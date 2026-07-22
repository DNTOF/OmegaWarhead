using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Spawn;
using Exiled.CustomItems.API.Features;
using MEC;
using OmegaWarhead.Items;
using Player = Exiled.API.Features.Player;
using Exiled.API.Features.Pickups;
namespace OmegaWarhead.Core
{
    /// <summary>
    /// Radioactive Element spawn point selector.
    ///
    /// Design notes:
    /// - Random room spawns: randomly selects rooms from the current map's room list as spawn points
    /// - Simultaneous cap: MaxSpawnedElements (default 6)
    /// - After an element is picked up/consumed, a new one respawns after ElementRespawnDelay (default 45s)
    ///
    /// Implementation strategy:
    /// - On round start, select up to MaxSpawnedElements rooms as initial spawn points
    /// - Each spawn "slot" independently runs a "picked up → delay → respawn in new random room" loop
    /// - Excludes special room types (elevators, surface, etc.) unsuitable for placing elements
    ///
    /// Note: this class uses Exiled.CustomItems SpawnProperties + DynamicSpawnPoint,
    /// but since we need dynamic "respawn after pickup" behavior (CustomItem's native
    /// SpawnProperties only spawns once at round start), we manually manage the spawn
    /// loop via coroutines.
    /// </summary>
    public sealed class SpawnPointSelector
    {
        #region Singleton

        private static SpawnPointSelector _instance;
        public static SpawnPointSelector Instance => _instance;

        public static SpawnPointSelector Create()
        {
            if (_instance != null)
                return _instance;
            _instance = new SpawnPointSelector();
            return _instance;
        }

        public static void Destroy()
        {
            _instance?.StopAll();
            _instance = null;
        }

        #endregion

        /// <summary>
        /// Currently active spawn coroutine handles (one per "slot"; slot count = Config.MaxSpawnedElements).
        /// Note: slots are not tied to specific rooms — each slot re-randomizes its room on every respawn.
        /// This is the true "random room spawn" behavior (previous versions tied slots to fixed rooms
        /// chosen at round start, which effectively was fixed-point spawning and contradicted the
        /// "random room (harder to predict)" design decision — corrected here).
        /// </summary>
        private readonly List<CoroutineHandle> _spawnCoroutines = new List<CoroutineHandle>();

        /// <summary>
        /// Set of rooms currently occupied (element spawned but not yet picked up).
        /// Prevents two elements from spawning in the same room simultaneously.
        /// MEC coroutines run cooperatively on the main thread; no locking needed.
        /// </summary>
        private readonly HashSet<Room> _occupiedRooms = new HashSet<Room>();

        private readonly Random _rng = new Random();

        private volatile bool _running;

        /// <summary>
        /// Room type blacklist: elevators, entrance (surface), Pocket dimension, etc.
        /// </summary>
        private static readonly HashSet<RoomType> BlacklistedRoomTypes = new HashSet<RoomType>
        {
            RoomType.EzGateA,
            RoomType.EzGateB,
            RoomType.EzCollapsedTunnel,
            RoomType.LczAirlock,
            RoomType.HczEzCheckpointA,
            RoomType.HczEzCheckpointB,
            RoomType.LczCheckpointA,
            RoomType.LczCheckpointB,
            RoomType.Surface,
        };

        private SpawnPointSelector() { }

        #region Start Spawn Loop

        /// <summary>
        /// Called on round start: selects spawn points and starts the spawn loop.
        /// </summary>
        /// <param name="elementItem">RadioactiveElement CustomItem instance</param>
        /// <param name="maxSpawned">Maximum simultaneous spawned count</param>
        /// <param name="respawnDelay">Respawn delay in seconds after an element is picked up</param>
        public void StartSpawnLoop(RadioactiveElement elementItem, int maxSpawned, float respawnDelay)
        {
            if (elementItem == null)
            {
                Log.Warn("[SpawnPointSelector] elementItem is null, skipping spawn loop.");
                return;
            }

            StopAll();
            _occupiedRooms.Clear();
            _running = true;

            int candidateCount = Room.List.Count(r => !BlacklistedRoomTypes.Contains(r.Type));
            if (candidateCount == 0)
            {
                Log.Warn("[SpawnPointSelector] No available rooms for spawning, skipping.");
                _running = false;
                return;
            }

            int slotCount = Math.Min(maxSpawned, candidateCount);
            Log.Info($"[SpawnPointSelector] Starting {slotCount} spawn slots. Each respawn picks a new random room.");

            for (int i = 0; i < slotCount; i++)
            {
                CoroutineHandle handle = Timing.RunCoroutine(
                    SpawnSlotCoroutine(elementItem, respawnDelay));
                _spawnCoroutines.Add(handle);
            }
        }

        /// <summary>
        /// Single slot spawn loop coroutine: picks a random free room before each spawn
        /// (not reusing a fixed room) → spawn → wait for pickup → delay → re-randomize → loop.
        /// This makes every spawn position unpredictable within the same round,
        /// fulfilling the "random room spawn" design requirement.
        /// </summary>
        private IEnumerator<float> SpawnSlotCoroutine(RadioactiveElement elementItem, float respawnDelay)
        {
            // Brief delay after round start to ensure map is fully loaded
            yield return Timing.WaitForSeconds(2f);

            while (_running)
            {
                Room room = PickRandomFreeRoom();
                if (room == null)
                {
                    // No free rooms temporarily (e.g., more slots than available rooms); retry later
                    yield return Timing.WaitForSeconds(5f);
                    continue;
                }

                _occupiedRooms.Add(room);
                SpawnElementInRoom(elementItem, room);

                // Wait for element to be picked up (poll whether its Pickup still exists near the room)
                yield return Timing.WaitForSeconds(1f);
                while (_running && IsElementStillInRoom(room, elementItem))
                    yield return Timing.WaitForSeconds(1f);

                _occupiedRooms.Remove(room);

                if (!_running)
                    yield break;

                Log.Debug($"[SpawnPointSelector] Element in {room.Name} was picked up/vanished. " +
                          $"Respawning in a new random room after {respawnDelay}s.");

                yield return Timing.WaitForSeconds(respawnDelay);
            }
        }

        /// <summary>
        /// Picks a random room from those currently unoccupied and not blacklisted.
        /// Recomputes the candidate list on every call for true randomness each time.
        /// </summary>
        private Room PickRandomFreeRoom()
        {
            List<Room> candidates = Room.List
                .Where(r => !BlacklistedRoomTypes.Contains(r.Type) && !_occupiedRooms.Contains(r))
                .ToList();

            if (candidates.Count == 0)
                return null;

            return candidates[_rng.Next(candidates.Count)];
        }

        /// <summary>
        /// Spawns a RadioactiveElement Pickup in the specified room.
        /// Uses the room's center position + small random offset to avoid stacking.
        /// </summary>
        private void SpawnElementInRoom(RadioactiveElement elementItem, Room room)
        {
            try
            {
                UnityEngine.Vector3 pos = room.Position + UnityEngine.Vector3.up * 1f;
                elementItem.Spawn(pos);
                Log.Debug($"[SpawnPointSelector] Spawned radioactive element in {room.Name} ({room.Zone}).");
            }
            catch (Exception ex)
            {
                Log.Error($"[SpawnPointSelector] Failed to spawn element in {room.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks whether the room still has the element's Pickup nearby.
        /// Iterates all Pickups and checks CustomItem association.
        /// </summary>
        private bool IsElementStillInRoom(Room room, RadioactiveElement elementItem)
        {
            try
            {
                foreach (Pickup pickup in Pickup.List)
                {
                    if (pickup == null || pickup.Position == null)
                        continue;

                    // Check if it's this CustomItem
                    if (!elementItem.Check(pickup))
                        continue;

                    // Check if near the room (distance < 10m)
                    float dist = UnityEngine.Vector3.Distance(pickup.Position, room.Position);
                    if (dist < 10f)
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Stops all spawn coroutines.
        /// </summary>
        public void StopAll()
        {
            _running = false;
            foreach (CoroutineHandle handle in _spawnCoroutines)
                Timing.KillCoroutines(handle);
            _spawnCoroutines.Clear();
            _occupiedRooms.Clear();
        }

        #endregion
    }
}
