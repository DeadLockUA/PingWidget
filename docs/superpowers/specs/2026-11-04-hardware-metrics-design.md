# PingWidget: hardware metrics panel (design)

Date: 2026-11-04
Builds on: `docs/superpowers/specs/2026-11-04-pingwidget-design.md` (the shipped ping card).

## Intent

The user wants the same at-a-glance card to also show how loaded and how hot the PC is, next to network
health. The section uses the same visual language as the ping card: a number plus a heartbeat line that turns
red above a threshold.

## Layout (chosen: mockup option B, "mini chart per metric")

The card gets wider. The ping column stays on the left unchanged. A new column on the right holds three
captioned sections:

| Section | Rows (label → value format) |
|---------|------------------------------|
| CPU | `Load` → `23%`; `Temp` → `54°C` |
| RAM | `Used` → `19.6/32 GB 61%` (absolute and percent in one cell) |
| GPU | `Load` → `9%`; `VRAM` → `3.9/16 GB 24%` (absolute and percent in one cell); `Temp` → `46°C` |

Each row has a short grey label, then a right-aligned value, then a narrow chart (about 120×14 px in the mockup).
The section caption (`CPU` / `RAM` / `GPU`) is small grey text above its rows. Same colours and font as the ping
card: green `#4cd97b`, red `#ff4d4d`, chart background `#14161a`, Segoe UI. The value text turns red when the
latest value is above threshold, as the ping value does.
Reference mockup: `.superpowers/brainstorm/25405-*/content/metrics-layout.html` (card B).

## Behaviour

- Sample once per second, alongside the ping. History is ~2 minutes (120 samples), the same as ping.
- Chart segments above the row's threshold are red; otherwise green. Threshold drawn as a dashed line.
- Default thresholds (*Claude's call, user accepted*): CPU load 90%, RAM 90%, GPU load 90%, VRAM 90%,
  CPU temp 80°C, GPU temp 80°C. The settings window lets the user edit these six thresholds.
- RAM and VRAM thresholds/charts work on the percentage.
- The tray icon colour still depends on ping only. Hardware metrics never turn it red.
- A metric that can't be read shows `—` and an empty chart. Nothing crashes, and no dialog appears.
- GPU `Temp` shows the **edge** sensor ("GPU Core" in LHM). This is what AMD's software shows as GPU
  temperature. The junction/hot-spot reads 15–25°C higher and would keep the 80°C row red while gaming.
  *Claude's call.* CPU `Temp` uses Tctl/Tdie (on the 5600X it equals CCD1).
- One GPU: use it. If there are several, use the primary/discrete one. (User has one: AMD Radeon RX 6900 XT.)

## CPU temperature and admin rights (user decision)

- **Without admin rights**, the CPU temperature row shows `—`. The app must never trigger a UAC prompt by
  itself, and autostart stays the existing per-user Run key.
- **With admin rights** (user chose "Run as administrator"), and the PawnIO driver installed once by the user
  from pawnio.eu, the CPU temp row shows the value.
- All other metrics must work without admin rights.

## Tech (Claude's call)

- Sensors via the **LibreHardwareMonitorLib** NuGet package, a version that uses PawnIO and **not** WinRing0
  (0.9.6 or later). This is the first third-party package in the app. The project's earlier
  "no NuGet beyond SDK" constraint is lifted for this package and the packages it pulls in transitively
  (HidSharp, System.Management, etc.). *Claude's call.*
- RAM can come from LHM or from `GlobalMemoryStatusEx`; CPU load can come from LHM. Builder's choice.
- Hardware sampling must not block the UI thread or the ping loop.

## Target machine (user's)

AMD Ryzen 5 5600X, AMD Radeon RX 6900 XT (16 GB), 32 GB RAM is assumed (the builder should read it, not
hard-code it). The user does not run as admin by default.

## Non-goals

- No fan speeds, clocks, power, disk or network throughput.
- No per-core CPU breakdown.
- No admin elevation flow, scheduled task or bundled driver installer.
- No alerts from hardware metrics (tray stays ping-only).

## Anti-goals

- Never show a UAC prompt on normal start.
- Never ship or load the WinRing0 driver (Defender flags it as a vulnerable driver).
- The card must stay light: sampling hardware once per second must not noticeably load the CPU.

## Left to the builder

- Internal structure: how sampling is scheduled, and whether it shares the ping timer.
- Exact pixel sizes, as long as the right column's height roughly matches the ping column's.
- The settings UI for the six thresholds.
- Units and rounding (GB to 1 decimal place is the mockup's choice).
