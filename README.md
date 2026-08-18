# OmegaWarhead

> **OMEGA Warhead Launch Controller** — A custom nuke launch system for SCP: Secret Laboratory (EXILED plugin).

> 🌐 **[中文文档](README_ZH.md)**

---

## Overview

OmegaWarhead replaces the standard Alpha Warhead detonation with a **multi-phase, player-driven launch system**. Instead of simply flipping a switch in the nuke room, players must:

1. **Collect** radioactive elements scattered randomly across the facility
2. **Synthesize** a Warhead Launch Controller once enough elements are gathered
3. **Confirm** the launch via a two-step activation process
4. **Survive** the countdown while the entire server hunts you down

The system features a custom UI panel (powered by HintServiceMeow), server-wide location tracking, CASSIE announcements, and an irreversible point-of-no-return mechanic.

> 💡 **Inspiration**: This plugin is inspired by the **Champions Quest** (nuke contract) from *Call of Duty: Warzone* — collect elements, assemble the device, confirm the launch, then survive the entire lobby hunting you down.

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

Only the following options are exposed in `config.yml` — all gameplay balance
values are **hardcoded into the assembly** (`Configs/Constants.cs`) and cannot
be modified via config, keeping the plugin tamper-resistant:

| Option | Default | Description |
|---|---|---|
| `is_enabled` | `true` | Enable/disable the plugin |
| `debug` | `false` | Enable debug logging |
| `lang` | `zh` | Plugin language: `zh` (Simplified Chinese) or `en` (English) |

All player-facing text (panel UI, tracking broadcasts, item names/descriptions,
CASSIE announcements, kill reasons) follows the configured language. CASSIE
announcements now also display **subtitles** for players.

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

## Branding

All in-game UI (launch panel, tracking broadcasts, item descriptions) carries a small **"By DNT_OF"** author tag.

---

## Automatic Updates

On plugin enable, OmegaWarhead checks the GitHub Releases page for a newer
version. If one is found, the new DLL is downloaded and **overwrites the current
plugin file automatically** — it takes effect on the next server restart or
plugin reload. Update checks run on a background thread and can never block
or crash the plugin.

---

## Statistics

The plugin keeps persistent statistics across restarts and exposes them to
authorized server staff (server console or full-permission admins). A reserved
**telemetry/statistics reporting interface** is planned for future releases,
allowing server owners to opt into anonymous usage statistics collection.

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

This project is licensed under the **GNU General Public License v3.0** (GPLv3). See [LICENSE](LICENSE).

> ⚠️ **Plagiarism notice**: This plugin is the original work of **DNT_OF**, licensed under GPLv3.
> Distribution is **only permitted by DNT_OF himself and channels he explicitly authorizes**
> (authorized channels are announced via his Bilibili account: https://space.bilibili.com/3493125592975851).
> Any other channel distributing or reselling this plugin (or modified versions) is committing
> copyright infringement. The SCP:SL modding community has a known problem with copycats and
> resellers — if you see this plugin being sold by an unauthorized source, please report it.
> Official source: https://github.com/DNTOF/OmegaWarhead

---

**Made with ☢️ by DNT_OF**
