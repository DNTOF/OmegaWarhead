# OmegaWarhead

> **OMEGA Warhead Launch Controller** — A custom nuke launch system for SCP: Secret Laboratory (EXILED plugin).

[![Build](https://github.com/DNTOF/OmegaWarhead/actions/workflows/build.yml/badge.svg)](https://github.com/DNTOF/OmegaWarhead/actions/workflows/build.yml)

---

## Overview

OmegaWarhead replaces the standard Alpha Warhead detonation with a **multi-phase, player-driven launch system**. Instead of simply flipping a switch in the nuke room, players must:

1. **Collect** radioactive elements scattered randomly across the facility
2. **Synthesize** a Warhead Launch Controller once enough elements are gathered
3. **Confirm** the launch via a two-step activation process
4. **Survive** the countdown while the entire server hunts you down

The system features a custom UI panel (powered by HintServiceMeow), server-wide location tracking, CASSIE announcements, and an irreversible point-of-no-return mechanic.

---

## Gameplay Flow

```
┌─────────────┐    ┌──────────────┐    ┌──────────┐    ┌───────────┐    ┌────────────┐
│  Collecting  │ →  │  Synthesis   │ →  │ Confirm  │ →  │ Countdown │ →  │ Detonation │
│  (scavenge)  │    │  (auto-craft)│    │ (2-step) │    │ (survive) │    │ (global)   │
└─────────────┘    └──────────────┘    └──────────┘    └───────────┘    └────────────┘
```

### Phase 1: Collecting
- **Radioactive Elements** spawn in random rooms across the facility (configurable count, default 6)
- Each element held deals **damage per second** (linear: 1 dmg/s per element)
- Holding elements triggers a **server-wide location broadcast** so others can hunt you
- Elements respawn in new random rooms after being picked up (default 45s delay)

### Phase 2: Synthesis
- Once you collect the required number of elements (default 5), they are **automatically consumed**
- A **Warhead Launch Controller** is crafted and placed in your inventory
- A panel appears on your screen (HSM overlay) showing standby status

### Phase 3: Confirmation
- Press the configured keybind (default: **K**) to initiate the launch sequence
- A **confirmation window** opens (default 5s) — you must press the keybind again within this window
- Timeout → reverts to Idle (you can retry)
- Second confirmation → your role is reset to **Tutorial** (detached from your original team)

### Phase 4: Countdown
- A countdown begins (default 268s / 4m28s)
- The operator receives survival gear (movement boost, MicroHID, heavy armor, E11-SR)
- The operator's location is broadcast to the entire server at high frequency
- CASSIE announces milestones (60s, 30s, 10s)
- **Point of No Return** (default 10s remaining): operator death no longer aborts the launch

### Phase 5: Detonation
- Countdown reaches zero → vanilla nuke detonates (kills SCP-079 + visual effects)
- After a short delay, **all surviving players are killed** (global kill, covers zones vanilla nuke misses)
- Round ends normally

---

## Configuration

All values are adjustable in the generated `config.yml`. Default values:

| Option | Default | Description |
|---|---|---|
| `is_enabled` | `true` | Enable/disable the plugin |
| `debug` | `false` | Enable debug logging |
| `required_element_count` | `5` | Elements needed to synthesize the controller |
| `max_spawned_elements` | `6` | Max elements on the map at once |
| `element_respawn_delay` | `45` | Seconds before a picked-up element respawns |
| `damage_per_element_per_second` | `1.0` | Damage per element held per second |
| `collecting_track_interval_seconds` | `6` | Location broadcast interval during collecting phase |
| `confirm_window_seconds` | `5` | Time window for the second confirmation |
| `countdown_total_seconds` | `268` | Total countdown duration (4m28s) |
| `point_of_no_return_seconds` | `10` | Remaining time after which the launch cannot be aborted |
| `counting_track_interval_seconds` | `3` | Location broadcast interval during countdown |
| `detonation_kill_delay_seconds` | `4` | Delay between countdown zero and global kill |

---

## Custom Items

### Radioactive Element (ID: 10001)
- Appears as a coin on the ground
- Deals radiation damage while held
- Spawns randomly in non-blacklisted rooms (excludes elevators, surface, checkpoints)

### Warhead Launch Controller (ID: 10002)
- Appears as a Chaos Insurgency keycard
- Obtained via auto-synthesis (not spawned)
- **Global unique**: only one exists per round
- Destroyed if the operator dies before the point of no return

---

## Events API

Third-party plugins can hook into OmegaWarhead's state machine via the `NukeEvents` class:

```csharp
using OmegaWarhead.Core;

// Example: play BGM during countdown
NukeEvents.CountingStarted += (session) => { /* start BGM */ };
NukeEvents.PointOfNoReturnReached += (session) => { /* climax BGM */ };
NukeEvents.LaunchAborted += (player) => { /* stop BGM, resume normal */ };
NukeEvents.Detonating += (session) => { /* explosion SFX */ };
```

All available events:
- `CollectingStarted(Player)` — player picks up their first element
- `ControllerAssembled(Player)` — controller synthesized successfully
- `ConfirmingStarted(NukeSession)` — first confirmation
- `ConfirmingTimedOut(Player)` — confirmation window expired
- `Locked(NukeSession)` — second confirmation, role reset to Tutorial
- `CountingStarted(NukeSession)` — countdown begins
- `PointOfNoReturnReached(NukeSession)` — irreversible threshold crossed
- `LaunchAborted(Player)` — launch aborted (operator died before PoNR)
- `Detonating(NukeSession)` — countdown reached zero
- `DetonationCompleted(NukeSession)` — global kill executed

---

## Dependencies

| Dependency | Version | Notes |
|---|---|---|
| [EXILED](https://github.com/ExMod-Team/EXILED) | 9.0.0+ | Server plugin framework for SCP:SL |
| [HintServiceMeow](https://github.com/MeowServer/HintServiceMeow) | Latest | Custom UI hint display system |
| Exiled.CustomItems | (bundled with EXILED) | Custom item API |

---

## Installation

1. Ensure **EXILED** and **HintServiceMeow** are installed on your SCP:SL server
2. Download the latest `OmegaWarhead.dll` from [Releases](https://github.com/DNTOF/OmegaWarhead/releases)
3. Place the DLL in your server's `EXILED/Plugins` folder
4. Restart the server (or use EXILED's hot-reload)
5. Adjust `config.yml` in `EXILED/Configs/OmegaWarhead/` as needed

---

## Keybind

Players can customize the confirmation keybind in-game via **Settings → Keybinds**:

- **Label**: `OMEGA Warhead Launch Controller: Confirm`
- **Default**: `K`
- **Condition**: controller must be in inventory (does not need to be the currently held item)

---

## Build from Source

```bash
git clone https://github.com/DNTOF/OmegaWarhead.git
cd OmegaWarhead
dotnet restore
dotnet build -c Release
```

The compiled DLL will be at `bin/Release/OmegaWarhead.dll`.

---

## License

This project is proprietary. Unauthorized redistribution or modification is prohibited.

---

**Made with ☢️ by DNT_OF**
