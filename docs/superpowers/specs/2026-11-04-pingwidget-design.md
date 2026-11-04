# PingWidget — design

Date: 2026-11-04
Repo: https://github.com/DeadLockUA/PingWidget (empty at start)

## Intent

A small Windows program that continuously pings a user-defined list of addresses
and draws a heartbeat chart per address, so the user can see network health at a
glance from the corner of the desktop. Red spikes make "this address is slow right
now" obvious without reading numbers.

## Goals

- Always runs in the system tray.
- Tray menu option **"Show on desktop"** (checkbox):
  - on → a dark card sits in a screen corner, always on top of other windows;
  - off → only the tray icon remains.
- Per address: one row with the address on the left and the current ping in ms on
  the right (green below threshold, red above), with a heartbeat line chart under it.
- Line is green while ping ≤ threshold; segments above the threshold are drawn red
  (red spikes). Threshold drawn as a faint dashed red line.
- Lost packet / timeout → red vertical bar across the chart at that moment.
- Addresses are editable by the user (add / remove / edit), each with its own
  threshold in ms.
- Default list on first run:

  | Address      | Threshold |
  |--------------|-----------|
  | 192.168.1.1  | 10 ms     |
  | 192.168.0.1  | 20 ms     |
  | 8.8.8.8      | 100 ms    |

## Visual style (chosen: option A, "compact rows")

Mockup chosen by the user: dark rounded card (~300 px wide, background `#1b1d22`,
chart background `#14161a`), Segoe UI 12 px, light-grey text. Green `#4cd97b`,
red `#ff4d4d`. Each row: header line (address left, `NN ms` right) then a chart
~34 px tall spanning the card width. Card height grows with the number of addresses.
Reference mockup: `.superpowers/brainstorm/*/content/style.html` (card A).

## Behaviour (proposed by Claude, confirmed by user as "all good")

- Ping each address once per second; chart shows the last ~2 minutes (120 samples).
- Default position: bottom-right corner, just above the taskbar. The card can be
  dragged; its position is remembered.
- Tray icon colour: green normally; red when at least one address has 3 of its last
  5 pings above threshold or lost (debounced so normal LAN jitter doesn't make it
  blink). *Claude's call; the user can override.*
- Multi-monitor: the user has a second monitor and will keep the card there, which
  also solves fullscreen games on the main screen. The card can be dragged onto
  any monitor, and its position, monitor included, is restored on the next start.
  If that monitor is gone, it falls back to the bottom-right of the primary screen.
- No in-game overlay: drawing over exclusive-fullscreen games is out of scope (user
  decision).
- Both default gateways (192.168.1.1 and 192.168.0.1) answer on the user's network,
  so the defaults stay as listed. No per-address enable switch is needed.
- Settings persist between runs; option "Start with Windows".

## Tech (Claude's call — user delegated it)

- C# / WPF on .NET 8 (SDK 8.0.425 is installed on the dev machine).
- Output: a single self-contained-or-framework-dependent `.exe`, no installer.
- Ping via `System.Net.NetworkInformation.Ping`.

## Non-goals

- No installer, no auto-update.
- No cross-platform support (Windows only).
- No long-term history, logging to disk, or statistics export.
- No alerts/sounds/notifications beyond the red chart and red tray icon.
- No overlay inside fullscreen games.

## Anti-goals

- Must not steal focus or get in the way: the card must not appear in the taskbar
  or Alt-Tab, and must not grab keyboard focus.
- Must not be heavy: should idle at low CPU/memory; it runs all day.

## Left to the builder

- Internal architecture, project layout, test approach.
- Exact settings UI (separate settings window opened from the tray menu is expected).
- Ping timeout value, where settings are stored, how autostart is implemented.
- Tray icon artwork.
