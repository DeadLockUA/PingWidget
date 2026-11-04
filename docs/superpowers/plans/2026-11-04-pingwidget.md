# PingWidget Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows tray app that pings a configurable list of addresses once per second and shows, in an always-on-top dark card, a heartbeat chart per address with red spikes above a per-address threshold.

**Architecture:** One WPF app with a UI-free `Core` folder: settings, ping loop, sample history, health rule, chart geometry, and window placement. All of it is unit-tested. The UI layer stays thin: a card window that draws charts from `ChartGeometry`, a WinForms `NotifyIcon` for the tray, and a settings window. `App` wires them together.

**Tech Stack:** C# 12, .NET 8 (`net8.0-windows`), WPF + WinForms interop (`UseWindowsForms` for `NotifyIcon` only), System.Text.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-04-pingwidget-design.md`

## Global Constraints

- Windows only; target `net8.0-windows`. No NuGet packages in the app beyond the SDK, except test packages in the test project.
- Default hosts on first run: `192.168.1.1` → 10 ms, `192.168.0.1` → 20 ms, `8.8.8.8` → 100 ms.
- Ping interval 1 s; history 120 samples (≈2 min); ping timeout 1000 ms.
- Tray icon red when any host has ≥3 of its last 5 samples above threshold or lost; otherwise green.
- Colours: card `#1b1d22`, chart bg `#14161a`, text `#dddddd`, green `#4cd97b`, red `#ff4d4d`; font Segoe UI 12; card width 300; chart height 34.
- Card: no taskbar button, not in Alt-Tab, never takes focus, Topmost, draggable, position (incl. monitor) remembered; fallback bottom-right of primary work area.
- Settings file: `%APPDATA%\PingWidget\settings.json`. Autostart: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value name `PingWidget`.
- Output: a single `.exe` with no installer.

## Review Focus

1. Unresolvable host name or invalid address typed in settings → counted as a lost packet (red bar), no crash or exception dialog. *(Task 3 test)*
2. Missing, empty, or corrupted `settings.json` → app starts with defaults and doesn't crash. *(Task 1 test)*
3. Saved position on a monitor that is now disconnected → card appears bottom-right of primary screen, not off-screen. *(Task 5 test)*
4. A host slower than the interval (1 s timeout every tick) → ticks don't pile up (overdue periods are skipped) and memory/CPU stay flat. *(Task 3 test)*
5. Host list edited while running → unchanged hosts keep their history, removed hosts disappear, new hosts start empty, no race with an in-flight tick. *(Task 3 test)*

---

## File Structure

```
PingWidget.sln
src/PingWidget/PingWidget.csproj
src/PingWidget/App.xaml, App.xaml.cs            — composition root, lifetime
src/PingWidget/Core/HostConfig.cs               — record HostConfig
src/PingWidget/Core/AppSettings.cs              — settings model + Defaults()
src/PingWidget/Core/SettingsStore.cs            — JSON load/save
src/PingWidget/Core/PingSample.cs               — record struct PingSample
src/PingWidget/Core/SampleHistory.cs            — fixed-size ring buffer
src/PingWidget/Core/HealthEvaluator.cs          — 3-of-5 rule
src/PingWidget/Core/IPinger.cs, SystemPinger.cs — ping abstraction
src/PingWidget/Core/PingMonitor.cs              — 1 s loop over hosts
src/PingWidget/Core/ChartGeometry.cs            — samples → line segments
src/PingWidget/Core/PlacementResolver.cs        — saved position vs monitors
src/PingWidget/Core/AutoStart.cs                — HKCU Run key
src/PingWidget/Widget/HeartbeatChart.cs         — FrameworkElement, OnRender
src/PingWidget/Widget/WidgetWindow.xaml(.cs)    — the card
src/PingWidget/Widget/HostRowViewModel.cs       — per-row binding data
src/PingWidget/Tray/TrayIconController.cs       — NotifyIcon + menu + icon colour
src/PingWidget/Settings/SettingsWindow.xaml(.cs)— edit hosts
tests/PingWidget.Tests/PingWidget.Tests.csproj  — xUnit, references app project
README.md
```

---

### Task 1: Solution scaffold + settings model and store

**Files:**
- Create: `PingWidget.sln`, `src/PingWidget/PingWidget.csproj`, `src/PingWidget/App.xaml(.cs)` (empty app that starts and shuts down), `src/PingWidget/Core/HostConfig.cs`, `AppSettings.cs`, `SettingsStore.cs`
- Test: `tests/PingWidget.Tests/PingWidget.Tests.csproj`, `tests/PingWidget.Tests/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record HostConfig(string Address, int ThresholdMs);`
  - `public sealed class AppSettings { List<HostConfig> Hosts; bool ShowOnDesktop = true; bool StartWithWindows = false; double? WindowLeft; double? WindowTop; static AppSettings Defaults(); }`
  - `public static class SettingsStore { static string DefaultPath { get; } static AppSettings Load(string path); static void Save(AppSettings s, string path); }`

csproj: `OutputType=WinExe`, `TargetFramework=net8.0-windows`, `UseWPF=true`, `UseWindowsForms=true`, `Nullable=enable`, `ImplicitUsings=enable`, `<InternalsVisibleTo Include="PingWidget.Tests" />`. To avoid WPF/WinForms type clashes under ImplicitUsings, add `<Using Remove="System.Windows.Forms" />` and `<Using Remove="System.Drawing" />`. Test project: `net8.0-windows`, `UseWPF=true`, xunit + xunit.runner.visualstudio + Microsoft.NET.Test.Sdk.

- [ ] **Step 1: Write failing tests** in `SettingsStoreTests.cs` (each uses a temp file path):
  - `Load_MissingFile_ReturnsDefaults`: `Hosts` equals `[("192.168.1.1",10),("192.168.0.1",20),("8.8.8.8",100)]`, `ShowOnDesktop == true`, `StartWithWindows == false`, `WindowLeft == null`.
  - `Load_CorruptJson_ReturnsDefaults`: file content `"{not json"` → defaults, no throw.
  - `Load_EmptyFile_ReturnsDefaults`.
  - `SaveThenLoad_RoundTrips`: hosts `[("1.1.1.1",50)]`, `ShowOnDesktop=false`, `WindowLeft=1930.5`, `WindowTop=40` → equal after load.
  - `Save_CreatesMissingDirectory`.
- [ ] **Step 2: Run** `dotnet test tests/PingWidget.Tests` → FAIL (types missing / not compiling).
- [ ] **Step 3: Implement** the three Core files. `DefaultPath` = `Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), "PingWidget", "settings.json")`. `Load` catches `IOException`, `JsonException`, `UnauthorizedAccessException` → `Defaults()`; a null `Hosts` after deserialisation → default hosts.
- [ ] **Step 4: Run** `dotnet test tests/PingWidget.Tests` → PASS; `dotnet build PingWidget.sln` → 0 errors.
- [ ] **Step 5: Commit** `feat: scaffold solution and settings store`.

### Task 2: Sample history and health rule

**Files:**
- Create: `src/PingWidget/Core/PingSample.cs`, `SampleHistory.cs`, `HealthEvaluator.cs`
- Test: `tests/PingWidget.Tests/SampleHistoryTests.cs`, `HealthEvaluatorTests.cs`

**Interfaces:**
- Produces:
  - `public readonly record struct PingSample(DateTime Time, long? RoundtripMs) { bool IsLost => RoundtripMs is null; }`
  - `public sealed class SampleHistory { SampleHistory(int capacity = 120); void Add(PingSample s); IReadOnlyList<PingSample> Snapshot(); int Count { get; } }` (thread-safe via `lock`; `Snapshot` returns oldest → newest)
  - `public static class HealthEvaluator { static bool IsUnhealthy(IReadOnlyList<PingSample> samples, int thresholdMs, int window = 5, int badCount = 3); }`

- [ ] **Step 1: Write failing tests:**
  - `Add_BeyondCapacity_DropsOldest`: capacity 3, add rtt 1,2,3,4 → snapshot rtts `[2,3,4]`.
  - `Snapshot_IsCopy`: mutating after snapshot does not change the returned list.
  - `IsUnhealthy_TwoBadOfFive_False`: rtts `[5,50,5,50,5]`, threshold 10 → false.
  - `IsUnhealthy_ThreeBadOfFive_True`: `[50,5,50,5,50]` → true.
  - `IsUnhealthy_LostCountsAsBad`: `[null,null,5,null,5]` → true.
  - `IsUnhealthy_OnlyLastFiveCount`: `[50,50,50,5,5,5,5,5]` → false.
  - `IsUnhealthy_EqualToThreshold_NotBad`: `[10,10,10,10,10]`, threshold 10 → false.
  - `IsUnhealthy_FewerThanFiveSamples`: `[50,50,50]` → true; `[50,50]` → false.
- [ ] **Step 2: Run** `dotnet test tests/PingWidget.Tests` → FAIL.
- [ ] **Step 3: Implement.** "Bad" = `IsLost || RoundtripMs > thresholdMs`.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat: sample history and 3-of-5 health rule`.

### Task 3: Pinger and ping monitor

**Files:**
- Create: `src/PingWidget/Core/IPinger.cs`, `SystemPinger.cs`, `PingMonitor.cs`
- Test: `tests/PingWidget.Tests/PingMonitorTests.cs` (with a `FakePinger` in the test file)

**Interfaces:**
- Consumes: `HostConfig`, `PingSample`, `SampleHistory` (Tasks 1–2).
- Produces:
  - `public interface IPinger { Task<long?> PingAsync(string address, int timeoutMs); }` (null = lost, **never throws**)
  - `public sealed class SystemPinger : IPinger` (wraps `System.Net.NetworkInformation.Ping`; any `Status != Success` or any exception → null)
  - `public sealed class PingMonitor : IDisposable { PingMonitor(IPinger pinger, Func<DateTime> clock); void SetHosts(IReadOnlyList<HostConfig> hosts); IReadOnlyList<HostConfig> Hosts { get; } SampleHistory HistoryFor(string address); Task TickAsync(); void Start(TimeSpan interval); void Stop(); event EventHandler? Updated; }`

Behaviour: `TickAsync` pings all hosts in parallel (`Task.WhenAll`), timeout 1000 ms, appends one sample per host, then raises `Updated` (on the thread that ran the tick; UI marshals). `Start` uses a `PeriodicTimer`. If a tick is still running when the next one is due, skip that period (no pile-up). `SetHosts` keeps the history of addresses that are still present (case-insensitive, trimmed), drops removed ones, and creates empty history for new ones. It is safe to call during a tick: a tick works on a snapshot of the host list, and samples for hosts removed mid-tick are discarded.

- [ ] **Step 1: Write failing tests:**
  - `Tick_AppendsOneSamplePerHost`: two hosts, fake returns 7 and 30 → `HistoryFor` each has one sample with those values.
  - `Tick_PingerReturnsNull_RecordsLost`.
  - `SystemPinger_InvalidHost_ReturnsNull`: real `SystemPinger`, address `"no-such-host.invalid"` → null, no throw. *(Review Focus 1)*
  - `SystemPinger_Loopback_ReturnsValue`: `"127.0.0.1"` → not null.
  - `SetHosts_KeepsHistoryForUnchangedHosts`: tick once with A,B; `SetHosts([A,C])` → A has 1 sample, C has 0, `HistoryFor("B")` throws `KeyNotFoundException`. *(Review Focus 5)*
  - `SetHosts_DuringTick_DoesNotThrow`: fake pinger blocks on a `TaskCompletionSource`; start `TickAsync`, call `SetHosts([C])`, release → no exception, C has 0 samples. *(Review Focus 5)*
  - `SlowHost_DoesNotBlockOthers` (Review Focus 4): fake: A returns after 1000 ms (`Task.Delay`), B immediately. `Start(100ms)` for 350 ms then `Stop` → at most 1 tick ran (A's history ≤ 1 sample), no exceptions. Also assert that ticks never overlap: the fake tracks how many `PingAsync` calls are in flight, and the peak must never exceed the host count (2).
  - `Updated_RaisedAfterTick`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** the three files.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat: ping monitor with parallel ticks and host editing`.

### Task 4: Chart geometry

**Files:**
- Create: `src/PingWidget/Core/ChartGeometry.cs`
- Test: `tests/PingWidget.Tests/ChartGeometryTests.cs`

**Interfaces:**
- Consumes: `PingSample`.
- Produces:
  - `public readonly record struct ChartSegment(double X1, double Y1, double X2, double Y2, bool AboveThreshold);`
  - `public sealed record ChartLayout(IReadOnlyList<ChartSegment> Segments, IReadOnlyList<double> LossBarsX, double ThresholdY);`
  - `public static class ChartGeometry { static ChartLayout Compute(IReadOnlyList<PingSample> samples, int thresholdMs, double width, double height, int capacity = 120); }`

Rules (from the mockup): x step = `width / (capacity - 1)`. Samples are right-aligned, so the newest sample is at `x = width`. Scale max = `max(thresholdMs * 2.5, max rtt in samples)`. `y = height - 2 - (min(rtt, max) / max) * (height - 4)`. A segment joins two consecutive non-lost samples, and `AboveThreshold = max(rtt1, rtt2) > thresholdMs`. A lost sample adds its x to `LossBarsX` and breaks the line (no segment across it). `ThresholdY` uses the same y formula.

- [ ] **Step 1: Write failing tests** (width 119, height 34, capacity 120, so the x step = 1):
  - `Empty_NoSegments`, with `ThresholdY` computed for threshold 10 (max 25) = `34 - 2 - (10/25)*30` = `20`.
  - `NewestSampleAtRightEdge`: 3 samples → last segment `X2 == 119`, first segment `X1 == 117`.
  - `SegmentAboveThreshold_WhenEitherEndAbove`: rtts `[5, 50, 5]`, threshold 10 → two segments, both `AboveThreshold == true`; rtts `[5,5]` → false.
  - `LostSample_BreaksLineAndAddsBar`: `[5, null, 5]` → 0 segments, `LossBarsX == [118]`.
  - `SpikeAboveScale_Clamped`: threshold 10, rtts `[5, 1000]` → max becomes 1000, so the y of 1000 equals `2` (top) and stays within `[0,34]`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat: heartbeat chart geometry`.

### Task 5: Placement resolver

**Files:**
- Create: `src/PingWidget/Core/PlacementResolver.cs`
- Test: `tests/PingWidget.Tests/PlacementResolverTests.cs`

**Interfaces:**
- Produces:
  - `public readonly record struct ScreenArea(double Left, double Top, double Width, double Height, bool IsPrimary);` (work areas, in WPF device-independent units)
  - `public static class PlacementResolver { static (double Left, double Top) Resolve(double? savedLeft, double? savedTop, double cardWidth, double cardHeight, IReadOnlyList<ScreenArea> screens, double margin = 12); }`

Rule: if a saved position exists and the saved top-left point lies inside some screen's work area, use the saved position, clamped so the whole card fits in that screen. Otherwise use the primary screen's bottom-right: `(Right - cardWidth - margin, Bottom - cardHeight - margin)`.

- [ ] **Step 1: Write failing tests** (primary `0,0,1920,1040`; secondary `1920,0,1920,1040`):
  - `NoSaved_BottomRightOfPrimary`: card 300×150 → `(1608, 878)`.
  - `SavedOnSecondary_Kept`: saved `(2500, 100)` → `(2500, 100)`.
  - `SavedOnDisconnectedMonitor_FallsBack` (Review Focus 3): only primary present, saved `(2500,100)` → `(1608, 878)`.
  - `SavedPartlyOffEdge_Clamped`: saved `(1900, 1000)` on primary only, card 300×150 → `(1620, 890)`.
  - `NegativeCoordinatesMonitor`: screens primary + left monitor `-1920,0,1920,1040`; saved `(-1000, 200)` → kept.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat: window placement with monitor fallback`.

### Task 6: Widget card window

**Files:**
- Create: `src/PingWidget/Widget/HeartbeatChart.cs`, `HostRowViewModel.cs`, `WidgetWindow.xaml`, `WidgetWindow.xaml.cs`
- Modify: `src/PingWidget/App.xaml.cs` (temporary wiring: load settings, start `PingMonitor`, show window; Task 7 replaces this with the tray)

**Interfaces:**
- Consumes: `ChartGeometry.Compute`, `PingMonitor`, `SampleHistory`, `PlacementResolver`, `AppSettings`.
- Produces:
  - `public sealed class HeartbeatChart : FrameworkElement` with dependency properties `Samples` (`IReadOnlyList<PingSample>`) and `ThresholdMs` (int). It redraws (`AffectsRender`) and draws, in order: background `#14161a`, threshold line (red at 35% opacity, dash 3/3), loss bars (3 px wide, red at 60% opacity, full height), segments (1.6 px, green/red). Frozen brushes and pens.
  - `public sealed class HostRowViewModel : INotifyPropertyChanged { string Address; int ThresholdMs; IReadOnlyList<PingSample> Samples; string CurrentText /* "12 ms" or "—" when the last sample is lost or there are none */; bool IsAboveThreshold /* last sample lost or > threshold */; }`
  - `public partial class WidgetWindow : Window { WidgetWindow(PingMonitor monitor, AppSettings settings, Action saveSettings); }`

Window properties: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`, `Topmost=True`, `ShowInTaskbar=False`, `ShowActivated=False`, `ResizeMode=NoResize`, `SizeToContent=Height`, `Width=300`. In `OnSourceInitialized`, add the extended styles `WS_EX_TOOLWINDOW (0x80) | WS_EX_NOACTIVATE (0x08000000)` with `GetWindowLongPtr`/`SetWindowLongPtr`. These hide the card from Alt-Tab and stop it taking focus. Content: a `Border` (corner radius 8, `#1b1d22`, padding 8) holding an `ItemsControl` of rows. Each row: a `DockPanel` with the address on the left and `CurrentText` on the right (red or green), with a `HeartbeatChart` under it, Height 34. Dragging: `MouseLeftButtonDown` → `DragMove()`, then save `Left`/`Top` into settings and call `saveSettings`. On `Loaded`, set the position with `PlacementResolver.Resolve`. The screen list comes from `System.Windows.Forms.Screen.AllScreens` (`WorkingArea`), converted to DIPs with the window's `CompositionTarget.TransformFromDevice`. On `monitor.Updated`, use `Dispatcher.BeginInvoke` to refresh each row's `Samples` from `HistoryFor(address).Snapshot()`. Rebuild the rows whenever `monitor.Hosts` changes.

- [ ] **Step 1: Write failing test** `HostRowViewModelTests`: last sample 12 → `CurrentText == "12 ms"`, threshold 10 → `IsAboveThreshold == true`; last sample lost → `"—"` and true; no samples → `"—"` and false.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** the four files and the temporary App wiring.
- [ ] **Step 4: Run** `dotnet test` → PASS. Then `dotnet run --project src/PingWidget` and check by eye: the card appears bottom-right; three rows; lines move every second; 8.8.8.8 is green under 100 ms; it does not appear in the taskbar or Alt-Tab; clicking it does not take focus from the active window; dragging it to the second monitor and restarting keeps it there. Take a screenshot as evidence.
- [ ] **Step 5: Commit** `feat: always-on-top heartbeat card`.

### Task 7: Tray icon, show-on-desktop, autostart, app lifetime

**Files:**
- Create: `src/PingWidget/Tray/TrayIconController.cs`, `src/PingWidget/Core/AutoStart.cs`
- Modify: `src/PingWidget/App.xaml` (`ShutdownMode="OnExplicitShutdown"`), `App.xaml.cs` (final composition root)
- Test: `tests/PingWidget.Tests/AutoStartTests.cs`

**Interfaces:**
- Consumes: `PingMonitor`, `HealthEvaluator`, `AppSettings`, `SettingsStore`, `WidgetWindow`.
- Produces:
  - `public static class AutoStart { static bool IsEnabled(string valueName = "PingWidget"); static void Set(bool enabled, string exePath, string valueName = "PingWidget"); }` (exe path quoted in the registry value)
  - `public sealed class TrayIconController : IDisposable { TrayIconController(); bool ShowOnDesktop { get; set; } bool StartWithWindows { get; set; } void SetHealthy(bool healthy); event EventHandler? ShowOnDesktopToggled, StartWithWindowsToggled, SettingsRequested, ExitRequested; }`

Tray menu, in this order: **Показывать на рабочем столе** (check), **Настройки…**, **Запускать с Windows** (check), separator, **Выход**. Double-clicking the icon toggles show-on-desktop. The icon is drawn at runtime as a 16×16 filled circle in green `#4cd97b` or red `#ff4d4d` (create both once, at startup). The tooltip is `PingWidget`. App logic: on each `monitor.Updated`, healthy = no host for which `HealthEvaluator.IsUnhealthy(history.Snapshot(), threshold)` is true; call `SetHealthy` on the UI thread. Show-on-desktop toggles `WidgetWindow` Show/Hide and persists the setting. On exit: `monitor.Stop()`, save settings, dispose the tray icon, `Shutdown()`. Single instance: a named `Mutex` `"PingWidget.SingleInstance"`; a second launch exits immediately.

- [ ] **Step 1: Write failing test** `AutoStart_SetAndClear` with a test-only value name `PingWidget.Test`: `Set(true, @"C:\x\PingWidget.exe", name)` → `IsEnabled(name)` true and the raw value equals `"\"C:\x\PingWidget.exe\""`; `Set(false, …)` → false. Clean up in `finally`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** AutoStart, TrayIconController, and the final App wiring.
- [ ] **Step 4: Run** `dotnet test` → PASS. Then manual check: the tray icon appears; unchecking "Показывать на рабочем столе" hides the card and the setting survives a restart; set a threshold of 1 ms on any host and the icon turns red within about 3 s; "Запускать с Windows" creates or removes the Run value (check with `reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v PingWidget`); "Выход" leaves no process behind; a second launch does not create a second icon.
- [ ] **Step 5: Commit** `feat: tray icon, show-on-desktop toggle, autostart`.

### Task 8: Settings window

**Files:**
- Create: `src/PingWidget/Settings/SettingsWindow.xaml`, `SettingsWindow.xaml.cs`, `src/PingWidget/Settings/HostListValidator.cs`
- Modify: `App.xaml.cs` (handle `SettingsRequested`)
- Test: `tests/PingWidget.Tests/HostListValidatorTests.cs`

**Interfaces:**
- Consumes: `HostConfig`, `AppSettings`, `PingMonitor.SetHosts`, `SettingsStore.Save`.
- Produces:
  - `public static class HostListValidator { static IReadOnlyList<string> Validate(IReadOnlyList<HostConfig> hosts); }` returns Russian error messages; an empty list means valid.
  - `public partial class SettingsWindow : Window { SettingsWindow(IReadOnlyList<HostConfig> current); IReadOnlyList<HostConfig>? Result { get; } }` (null when cancelled)

UI: a `DataGrid` with two columns, **Адрес** and **Порог, мс**; buttons **Добавить**, **Удалить**, **Сохранить**, **Отмена**. It opens as a normal window, centred and activated. Only one window can be open at a time: if it is already open, activate it instead of opening another. On save: trim addresses, validate, and show the errors in a `TextBlock` (no MessageBox) if there are any. If valid, `Result` = the list, App calls `monitor.SetHosts`, persists the settings, and the card's rows rebuild.

Validation rules: address must not be blank → `"Адрес не может быть пустым"`; threshold must be 1–10000 → `"Порог должен быть от 1 до 10000 мс"`; no duplicate addresses (case-insensitive) → `"Адрес {a} указан дважды"`; at least one host → `"Добавьте хотя бы один адрес"`.

- [ ] **Step 1: Write failing tests:** one test per rule above, asserting the exact message; plus the valid default list → empty.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `dotnet test` → PASS. Manual check: add `1.1.1.1` with threshold 30 and the card shows a fourth row within 1 s, while the other rows keep their history; remove it and the row disappears; enter threshold `0` and an error is shown and nothing is saved; type `no-such-host.invalid` and the row shows red loss bars with no crash.
- [ ] **Step 5: Commit** `feat: settings window for hosts and thresholds`.

### Task 9: Single-exe publish and README

**Files:**
- Modify: `src/PingWidget/PingWidget.csproj` (publish properties)
- Create: `README.md`

Publish profile: `dotnet publish src/PingWidget -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish`. The app is framework-dependent: it needs the .NET 8 Desktop Runtime and keeps the exe small. The README says what the app does, includes a screenshot (`docs/screenshot.png` from Task 6), how to build and publish, where settings are stored, and the runtime requirement. Add `publish/` to `.gitignore`.

- [ ] **Step 1: Run** the publish command → `publish/PingWidget.exe` exists, and it is the only `.exe` there.
- [ ] **Step 2: Launch** `publish/PingWidget.exe` from Explorer → the tray icon and card appear.
- [ ] **Step 3: Write README.md.**
- [ ] **Step 4: Run** `dotnet test` one last time → all PASS.
- [ ] **Step 5: Commit and push** `docs: README and single-file publish`.
