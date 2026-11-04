# Hardware Metrics Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a right-hand column to the PingWidget card with CPU (load, temp), RAM (used/total, %) and GPU (load, VRAM used/total + %, temp) rows, each with a value and a mini heartbeat chart that turns red above a configurable threshold.

**Architecture:** Sensor values come from LibreHardwareMonitorLib through a thin `IHardwareSource`. They are flattened into plain `SensorReading` records, and a pure `SensorPicker` maps them to six `MetricKind` readings, so all selection logic is unit-tested with fake sensor lists. A `HardwareMonitor` samples once per second off the UI thread into ring buffers. The existing chart geometry and chart control are generalised from ping samples to `double?` values, so ping rows and metric rows share one renderer.

**Tech Stack:** C# 12, .NET 8 WPF (existing app), LibreHardwareMonitorLib 0.9.6, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-04-hardware-metrics-design.md` (builds on `docs/superpowers/specs/2026-11-04-pingwidget-design.md`)

## Global Constraints

- Package: `LibreHardwareMonitorLib` **0.9.6** (PawnIO-based; must not ship/load WinRing0). Its transitive packages are allowed; no other new packages.
- Never trigger UAC; autostart unchanged (HKCU Run). Everything except CPU temp works without admin.
- Sample once per second; history 120 samples.
- Default thresholds: CPU load 90 %, CPU temp 80 °C, RAM 90 %, GPU load 90 %, VRAM 90 %, GPU temp 80 °C. Percent thresholds 1–100, temperature thresholds 1–150.
- Unreadable metric → value text `—`, empty chart; never crash, never show a dialog.
- Tray icon colour stays ping-only.
- GPU temp = edge sensor (`GPU Core`), not hot spot. CPU temp = `Core (Tctl/Tdie)`.
- Value formats: `23%`, `54°C`, `19.6/32 GB 61%`, `3.5/16 GB 22%`. GB with format `0.#`, invariant culture.
- Colours/fonts as the ping card: green `#4cd97b`, red `#ff4d4d`, chart bg `#14161a`, labels grey `#888888`, Segoe UI.

Sensor facts measured on the user's machine (Ryzen 5 5600X, RX 6900 XT, non-admin, LHM 0.9.6):

| HardwareType / Hardware name | SensorType | Sensor name | Value |
|---|---|---|---|
| `Cpu` | Load | `CPU Total` | 46.27 |
| `Cpu` | Temperature | `Core (Tctl/Tdie)` | **0** (no admin, no PawnIO) |
| `Memory` / `Total Memory` | Load | `Memory` | 57.95 |
| `Memory` / `Total Memory` | Data | `Memory Used` / `Memory Available` | 18.50 / 13.42 (GB) |
| `Memory` / `Virtual Memory` | … | (same names; must be ignored) | |
| `GpuAmd` | Load | `GPU Core` | 5 |
| `GpuAmd` | Temperature | `GPU Core` / `GPU Hot Spot` | 59 / 67 |
| `GpuAmd` | SmallData | `GPU Memory Used` / `GPU Memory Total` | 3533 / 16368 (MB) |
| `GpuAmd` | SmallData | `D3D Dedicated Memory Used` / `D3D Dedicated Memory Total` | 3533.7 / 16338.2 (MB) |

`Computer.Open()` took ~0.9 s; each `Update()` pass ~30 ms.

## Review Focus

1. LHM `Open()`/`Update()` throws or hangs (driver/AV trouble) → all metric rows show `—`, ping keeps working, ticks don't pile up. *(Task 3 tests)*
2. CPU temp sensor reports `0`, negative or NaN without admin → `—`, never `0°C`. *(Task 2 test)*
3. Different hardware (NVIDIA GPU, Intel iGPU only, no GPU, Intel CPU naming) → picks what exists, missing rows show `—`. *(Task 2 tests)*
4. settings.json from v1 (no `MetricThresholds`), or thresholds hand-edited out of range → defaults or clamped values, no crash. *(Task 4 tests)*
5. Card becomes ~2× wider while its saved position is near the right screen edge → the card is pulled fully back onto the screen. *(The existing SizeChanged → PlacementResolver clamp handles this; Task 5 checks it manually.)*

---

## File Structure

```
src/PingWidget/Core/RingBuffer.cs                 — generic thread-safe ring buffer (extracted from SampleHistory)
src/PingWidget/Core/SampleHistory.cs              — becomes: class SampleHistory : RingBuffer<PingSample>
src/PingWidget/Core/ChartGeometry.cs              — + Compute(IReadOnlyList<double?>, …, fixedMax)
src/PingWidget/Core/Metrics/MetricKind.cs         — enum + MetricReading + SensorReading
src/PingWidget/Core/Metrics/SensorPicker.cs       — sensors → metric readings (pure)
src/PingWidget/Core/Metrics/MetricFormatter.cs    — reading → display text (pure)
src/PingWidget/Core/Metrics/MetricThresholds.cs   — six thresholds + Sanitize + Of(kind)
src/PingWidget/Core/Metrics/IHardwareSource.cs    — interface
src/PingWidget/Core/Metrics/LhmHardwareSource.cs  — LibreHardwareMonitor adapter
src/PingWidget/Core/Metrics/HardwareMonitor.cs    — 1 s sampling loop + histories
src/PingWidget/Widget/HeartbeatChart.cs           — Values/Threshold/FixedMax DPs
src/PingWidget/Widget/MetricRowViewModel.cs       — per metric row
src/PingWidget/Widget/WidgetWindow.xaml(.cs)      — two-column layout
src/PingWidget/Settings/SettingsWindow.xaml(.cs)  — threshold fields
src/PingWidget/App.xaml.cs                        — wiring
```

---

### Task 1: Generalise history and chart geometry to plain values

**Files:**
- Create: `src/PingWidget/Core/RingBuffer.cs`
- Modify: `src/PingWidget/Core/SampleHistory.cs`, `src/PingWidget/Core/ChartGeometry.cs`, `src/PingWidget/Widget/HeartbeatChart.cs`, `src/PingWidget/Widget/HostRowViewModel.cs`, `src/PingWidget/Widget/WidgetWindow.xaml`
- Test: `tests/PingWidget.Tests/ChartGeometryTests.cs`, `tests/PingWidget.Tests/HostRowViewModelTests.cs`

**Interfaces:**
- Produces:
  - `public class RingBuffer<T> { RingBuffer(int capacity = 120); void Add(T item); IReadOnlyList<T> Snapshot(); int Count { get; } }`, plus `public sealed class SampleHistory : RingBuffer<PingSample>`. All existing SampleHistory tests stay unchanged and green.
  - `ChartGeometry.Compute(IReadOnlyList<double?> values, double threshold, double width, double height, int capacity = 120, double? fixedMax = null) → ChartLayout`. With `fixedMax`, the scale max is exactly `fixedMax`, and values above it are clamped to the top. Without it, the existing rule applies (`max(threshold*2.5, max value)`). `null` = gap + loss bar, as today. The existing `PingSample` overload delegates to this one.
  - `HeartbeatChart` DPs: `Values` (`IReadOnlyList<double?>`), `Threshold` (`double`), `FixedMax` (`double`, `NaN` = auto). Replaces `Samples`/`ThresholdMs`.
  - `HostRowViewModel.Values` (`IReadOnlyList<double?>`, projected from `Samples`; raises PropertyChanged together with `Samples`).

- [ ] **Step 1: Write failing tests:**
  - `Values_FixedMax_ScalesToFixedMax`: values `[50, 50]`, threshold 90, `fixedMax: 100`, h 34 → segment `Y1 == 34 - 2 - 0.5*30 == 17`.
  - `Values_FixedMax_ClampsAbove`: values `[150]` plus one more `[150, 150]` with fixedMax 100 → `Y == 2`.
  - `Values_Null_IsLossBar`: `[1, null, 1]` → `LossBarsX == [118]` (w 119).
  - `HostRow_ValuesProjectSamples`: samples `[5, null]` → `Values == [5.0, null]`, and setting Samples raises PropertyChanged for `Values`.
- [ ] **Step 2: Run** `dotnet test` → FAIL (compile).
- [ ] **Step 3: Implement.** Rebind the ping row chart in `WidgetWindow.xaml` to `Values` / `Threshold` (from `ThresholdMs`).
- [ ] **Step 4: Run** `dotnet test` → all PASS (including all pre-existing tests).
- [ ] **Step 5: Commit** `refactor: generic ring buffer and value-based chart`.

### Task 2: Sensor picking and formatting

**Files:**
- Create: `src/PingWidget/Core/Metrics/MetricKind.cs`, `SensorPicker.cs`, `MetricFormatter.cs`
- Test: `tests/PingWidget.Tests/SensorPickerTests.cs`, `tests/PingWidget.Tests/MetricFormatterTests.cs`

**Interfaces:**
- Produces:
  - `public enum MetricKind { CpuLoad, CpuTemp, RamUsed, GpuLoad, VramUsed, GpuTemp }`
  - `public readonly record struct MetricReading(double? Value, double? UsedGb = null, double? TotalGb = null) { static MetricReading Missing; }`. Value is a percent for load/RAM/VRAM and °C for temps.
  - `public sealed record SensorReading(string HardwareType, string HardwareName, string SensorType, string Name, float? Value);` (strings = LHM enum `.ToString()`)
  - `public static class SensorPicker { static IReadOnlyDictionary<MetricKind, MetricReading> Pick(IReadOnlyList<SensorReading> sensors); }` (always contains all six keys)
  - `public static class MetricFormatter { static string Format(MetricKind kind, MetricReading r); }`

Picking rules:
- CPU: hardware type `Cpu`. Load = `Load`/`CPU Total`. Temp = first `Temperature` sensor named `Core (Tctl/Tdie)`, then `Core (Tctl)`, then `CPU Package`.
- RAM: hardware type `Memory` whose name is not `Virtual Memory` (prefer `Total Memory`). Value = `Load`/`Memory`. Used = `Data`/`Memory Used`. Total = Used + `Data`/`Memory Available`.
- GPU: pick one hardware. Priority `GpuNvidia` = `GpuAmd` (first found) over `GpuIntel`. Load = `Load`/`GPU Core`. Temp = `Temperature`/`GPU Core`. VRAM: `SmallData` `GPU Memory Used`/`GPU Memory Total` (MB), falling back to `D3D Dedicated Memory Used`/`D3D Dedicated Memory Total`. Used/TotalGb = MB/1024. Value = used/total·100.
- Any value that is null, NaN, or (for temps) ≤ 0 → that metric is `Missing`. Any total ≤ 0 → `Missing`.

- [ ] **Step 1: Write failing tests** (build sensor lists from the measured table above):
  - `UserMachine_AllButCpuTemp`: CpuLoad 46.27; CpuTemp `Missing` (sensor value 0); RamUsed Value 57.95, UsedGb 18.50, TotalGb 31.92 (±0.01); GpuLoad 5; GpuTemp 59 (not 67); VramUsed UsedGb 3533/1024, TotalGb 16368/1024, Value 21.58 (±0.01).
  - `VirtualMemoryIgnored`: only a `Virtual Memory` hardware → RamUsed `Missing`.
  - `CpuTempPresentWhenAdmin`: Tctl/Tdie 54 → 54.
  - `CpuTempNaN_Missing`.
  - `NvidiaNamesWork`: `GpuNvidia` with the same sensor names → picked.
  - `DiscretePreferredOverIntel`: `GpuIntel` listed first, then `GpuAmd` → the AMD values are used.
  - `IntelOnly_Used`: only `GpuIntel` with `GPU Core` load 12 → GpuLoad 12, missing VRAM/temp → `Missing`.
  - `NoGpu_AllGpuMissing`.
  - `VramFallbackToD3D`: no `GPU Memory *` → D3D values are used.
  - `IntelCpuPackageTemp`: `CPU Package` 61 → CpuTemp 61.
  - Formatter: CpuLoad 46.27 → `"46%"`; CpuTemp 54.4 → `"54°C"`; RamUsed (57.95, 18.50, 31.92) → `"18.5/31.9 GB 58%"`; VramUsed (21.58, 3.45, 15.98) → `"3.5/16 GB 22%"`; any `Missing` → `"—"`; also under `CultureInfo.CurrentCulture = ru-RU` the decimal separator is still `.`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat: pick and format hardware metrics from sensor readings`.

### Task 3: Hardware source and sampling monitor

**Files:**
- Modify: `src/PingWidget/PingWidget.csproj` (PackageReference `LibreHardwareMonitorLib` `0.9.6`)
- Create: `src/PingWidget/Core/Metrics/IHardwareSource.cs`, `LhmHardwareSource.cs`, `HardwareMonitor.cs`
- Test: `tests/PingWidget.Tests/HardwareMonitorTests.cs` (fake source in the file), `tests/PingWidget.Tests/LhmHardwareSourceTests.cs`

**Interfaces:**
- Consumes: `SensorReading`, `SensorPicker.Pick`, `MetricKind`, `MetricReading`, `RingBuffer<T>`.
- Produces:
  - `public interface IHardwareSource : IDisposable { IReadOnlyList<SensorReading> ReadSensors(); }`
  - `public sealed class LhmHardwareSource : IHardwareSource`. Opens a `Computer { IsCpuEnabled, IsGpuEnabled, IsMemoryEnabled = true }` in its constructor. `ReadSensors` updates every hardware and sub-hardware and flattens their sensors.
  - `public sealed class HardwareMonitor : IDisposable { HardwareMonitor(Func<IHardwareSource> sourceFactory); RingBuffer<MetricReading> HistoryFor(MetricKind kind); Task TickAsync(); void Start(TimeSpan interval); void Stop(); event EventHandler? Updated; }`

Behaviour: `TickAsync` runs source creation (lazily, on the first tick) and `ReadSensors` via `Task.Run`, then picks and appends one reading per kind and raises `Updated`. Any exception from the factory or `ReadSensors` → append `Missing` for all six kinds. After a factory failure, the next creation attempt waits 30 ticks; a `ReadSensors` failure disposes the source and recreates it on the next tick. `Start` uses the same PeriodicTimer + sequential loop pattern as `PingMonitor` (no overlap; missed periods coalesce). `Dispose` stops the loop and disposes the source.

- [ ] **Step 1: Write failing tests:**
  - `Tick_AppendsPickedReadings`: fake returns the measured CPU Load sensor → `HistoryFor(CpuLoad)` last Value 46.27; `HistoryFor(GpuTemp)` last is `Missing`.
  - `ReadThrows_AllMissing_NoThrow` (Review Focus 1).
  - `FactoryThrows_AllMissing_RetriesAfter30Ticks`: factory throws on the first call → 1 call after 30 ticks, a 2nd call on tick 31.
  - `ReadThrows_SourceDisposedAndRecreated`: the fake counts Dispose; after one throw, the next tick calls the factory again.
  - `SlowSource_TicksDoNotOverlap` (Review Focus 1): `ReadSensors` sleeps 500 ms; `Start(50ms)` for 300 ms → max concurrent reads == 1.
  - `LhmHardwareSource_ReadsCpuLoadOnThisMachine` (real hardware): `SensorPicker.Pick(new LhmHardwareSource().ReadSensors())[CpuLoad].Value` is not null.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** (add the package first: `dotnet add src/PingWidget package LibreHardwareMonitorLib --version 0.9.6`).
- [ ] **Step 4: Run** `dotnet test` → PASS.
- [ ] **Step 5: Commit** `feat: hardware sampling via LibreHardwareMonitor`.

### Task 4: Metric thresholds in settings

**Files:**
- Create: `src/PingWidget/Core/Metrics/MetricThresholds.cs`
- Modify: `src/PingWidget/Core/AppSettings.cs` (property `MetricThresholds MetricThresholds { get; set; } = new();`), `src/PingWidget/Core/SettingsStore.cs` (sanitise on load), `src/PingWidget/Settings/HostListValidator.cs` (+ `ValidateThresholds`)
- Test: `tests/PingWidget.Tests/SettingsStoreTests.cs`, `tests/PingWidget.Tests/HostListValidatorTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class MetricThresholds { int CpuLoad = 90; int CpuTemp = 80; int RamUsed = 90; int GpuLoad = 90; int VramUsed = 90; int GpuTemp = 80; int Of(MetricKind k); static bool IsTemperature(MetricKind k); MetricThresholds Sanitized(); }` (properties with setters, for JSON). `Sanitized` clamps percents to 1–100 and temperatures to 1–150.
  - `HostListValidator.ValidateThresholds(MetricThresholds t) → IReadOnlyList<string>`, with messages `"Порог нагрузки и памяти должен быть от 1 до 100 %"` and `"Порог температуры должен быть от 1 до 150 °C"` (each at most once).

- [ ] **Step 1: Write failing tests:**
  - `Load_V1FileWithoutMetricThresholds_Defaults` (Review Focus 4): the old JSON with only Hosts → `CpuLoad == 90`, `CpuTemp == 80`, `GpuTemp == 80`.
  - `Load_NullMetricThresholds_Defaults`: `"MetricThresholds": null`.
  - `Load_MetricThresholdsClamped`: `{"CpuLoad":0,"CpuTemp":999,"RamUsed":150}` → 1, 150, 100; the missing ones default.
  - `SaveThenLoad_RoundTripsThresholds`.
  - `ValidateThresholds_Defaults_Valid`; `ValidateThresholds_PercentOutOfRange` (GpuLoad 101) → percent message; `ValidateThresholds_TempOutOfRange` (GpuTemp 0) → temp message.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat: configurable hardware thresholds in settings`.

### Task 5: Metric column on the card + app wiring

**Files:**
- Create: `src/PingWidget/Widget/MetricRowViewModel.cs`
- Modify: `src/PingWidget/Widget/WidgetWindow.xaml(.cs)`, `src/PingWidget/App.xaml.cs`
- Test: `tests/PingWidget.Tests/MetricRowViewModelTests.cs`

**Interfaces:**
- Consumes: `HardwareMonitor`, `MetricFormatter`, `MetricThresholds`, `HeartbeatChart` (Values/Threshold/FixedMax).
- Produces:
  - `public sealed class MetricRowViewModel : INotifyPropertyChanged { MetricRowViewModel(MetricKind kind, string label, double threshold); MetricKind Kind; string Label; double Threshold; IReadOnlyList<MetricReading> Readings { get; set; } IReadOnlyList<double?> Values; string CurrentText; bool IsAboveThreshold; }`
  - `WidgetWindow(PingMonitor monitor, HardwareMonitor hardware, AppSettings settings, Action saveSettings)`, plus `public void RebuildMetricRows()` (re-reads thresholds from settings).

Layout: the card `Border` holds a two-column `Grid`: ping column 280 wide | 12 gap | metrics column 270 wide. The window uses `SizeToContent="WidthAndHeight"` (remove the fixed `Width`). The metrics column has three sections; each starts with a caption (`CPU` / `RAM` / `GPU`, 11 px, `#888888`, margin-top 4). The rows are:

| Section | Label | Kind |
|---|---|---|
| CPU | `Load` | CpuLoad |
| CPU | `Temp` | CpuTemp |
| RAM | `Used` | RamUsed |
| GPU | `Load` | GpuLoad |
| GPU | `VRAM` | VramUsed |
| GPU | `Temp` | GpuTemp |

Each row is a `DockPanel` (height 16, margin 0,2): label width 44 in `#aaaaaa`; value width 100, right-aligned, green/red like the ping value; `HeartbeatChart` filling the rest with `FixedMax=100`, `Threshold=` the row's threshold. Row VM: `CurrentText = MetricFormatter.Format(Kind, last)`, or `—` when there are no readings. `IsAboveThreshold` = last Value > Threshold. `Values` = the Value of each reading.

Wiring: `App` creates `new HardwareMonitor(() => new LhmHardwareSource())`, calls `Start(TimeSpan.FromSeconds(1))`, passes it to `WidgetWindow`, and disposes it on exit. The widget subscribes to `hardware.Updated` → `Dispatcher.BeginInvoke` → refresh `Readings` from `HistoryFor(kind).Snapshot()`. Tray health is unchanged (ping only).

- [ ] **Step 1: Write failing tests:**
  - `MetricRow_FormatsLastReading`: kind RamUsed, readings `[(57.95, 18.5, 31.92)]` → `"18.5/31.9 GB 58%"`, threshold 90 → not above.
  - `MetricRow_AboveThreshold`: CpuTemp 85, threshold 80 → true and `"85°C"`.
  - `MetricRow_MissingLast`: last `Missing` → `"—"`, not above.
  - `MetricRow_NoReadings`: → `"—"`.
  - `MetricRow_ValuesProjection` + PropertyChanged for `Values`, `CurrentText`, `IsAboveThreshold`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** the VM, the layout and the wiring.
- [ ] **Step 4: Run** `dotnet test` → PASS. Then run the app (non-admin) and take a DPI-aware screenshot of the card. Check:
  - the right column shows six rows with live values;
  - CPU `Temp` shows `—`;
  - GPU `Temp` is around the edge value, not the hot spot;
  - no UAC prompt appears;
  - the card stays fully on screen in the bottom-right corner (Review Focus 5);
  - the app's CPU use stays below 2 % in Task Manager over 30 s.
- [ ] **Step 5: Commit** `feat: CPU/RAM/GPU column on the card`.

### Task 6: Threshold fields in Settings, publish and README

**Files:**
- Modify: `src/PingWidget/Settings/SettingsWindow.xaml(.cs)`, `src/PingWidget/App.xaml.cs` (`OpenSettings`), `README.md`
- Test: `tests/PingWidget.Tests/SettingsWindowTests.cs`

**Interfaces:**
- Consumes: `MetricThresholds`, `HostListValidator.ValidateThresholds`, `WidgetWindow.RebuildMetricRows`.
- Produces: `SettingsWindow(IReadOnlyList<HostConfig> hosts, MetricThresholds thresholds)`; `MetricThresholds? ResultThresholds { get; }`; and the internal `ThresholdTexts` (`Dictionary<MetricKind, string>`, for tests).

UI: below the host grid add a group headed **Пороги железа**, as a 3×2 grid of label + TextBox (width 60):
- `CPU нагрузка, %`, `CPU темп., °C`
- `RAM, %`, `GPU нагрузка, %`
- `VRAM, %`, `GPU темп., °C`

Grow the window height to 520. `TryAccept` parses each field (non-number → 0) and validates hosts and thresholds together (errors concatenated). On success it sets both results. `App.OpenSettings` then saves `MetricThresholds` and calls `RebuildMetricRows()`.

README: add a "Железо" section. It lists the metrics and explains that CPU temperature needs (1) PawnIO installed once from https://pawnio.eu and (2) running the app as administrator; otherwise it shows `—`.

- [ ] **Step 1: Write failing tests:**
  - `Accept_ThresholdsParsed`: set `ThresholdTexts[GpuTemp] = "85"` → `ResultThresholds.GpuTemp == 85`.
  - `Accept_BadThreshold_ShowsTempError`: `ThresholdTexts[CpuTemp] = "abc"` → false, `ErrorText` contains `"Порог температуры должен быть от 1 до 150 °C"`.
  - Update existing SettingsWindowTests constructors to pass `new MetricThresholds()`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `dotnet test` → PASS. Then `dotnet publish src/PingWidget -c Release -o publish` → `publish/PingWidget.exe` launches and shows the metrics. Check whether the published binaries contain the WinRing0 driver: `grep -c -a "WinRing0" publish/PingWidget.exe` → record the count in the ledger. If it is non-zero, check whether LHM ever loads it (it must not on this machine: no `WinRing0` service appears in `sc query type= driver | grep -i ring`).
- [ ] **Step 5: Commit and push** `feat: hardware thresholds in settings; README`.
