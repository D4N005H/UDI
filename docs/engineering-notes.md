# Engineering notes (working ledger)

Working notes for the audit and repair of this repository. Kept concise and
updated as work proceeds. Status words used throughout:
VERIFIED (inspected source, docs, or reproduced), HYPOTHESIS (awaiting a
discriminating check), UNKNOWN, DESIGN DECISION.

## Branch and worktree state

- Branch `claude/eloquent-curie-78h3gn`, base `main` at `8a27281`.
- Commits `7ae2121` and `8a27281` were reverted on this branch (user approved)
  because `7ae2121` deleted every type the manager depends on
  (`FFBEffects`, `DeviceInfo`, `FlatJoyState2`, `DIDEVCAPS`, `ActiveDeviceInfo`,
  `DICondition`, `DBTEvents`, `DIJOYSTATE2`, `DwFlags`) and the package could
  not compile. Details under Findings.

## Repository contents (VERIFIED from `git ls-files`, 58 tracked files)

| Area | Files | Notes |
|------|-------|-------|
| Native DLL | `src~/DirectInputForceFeedback~/**` (`.cpp` 2039 lines, `.h`, pch, vcxproj, sln) | C++20 (`std::map::contains`), MSVC v143, x64 and Win32 configs. Release x64 post-build copies the DLL into `Runtime/Plugins/DLL`. |
| Shipped binary | `Runtime/Plugins/DLL/DirectInputForceFeedback.dll` | PE32+ x64, built 2026-05-02, 30 exports. Imports `DINPUT8.dll`, `MSVCP140`, `VCRUNTIME140(_1)`, UCRT api-sets. Last rebuilt in the same commit as the last `.cpp` change (`31fcbc6`). |
| Unity runtime | `Runtime/DirectInputManager.cs`, `Runtime/DirectInputLogger.cs`, `Runtime/Plugins/VirtualInputSystemDevice.cs`, asmdefs | Runtime asmdef: Editor + WindowsStandalone64 only, `allowUnsafeCode`. |
| Unity tests | `Tests/Editor/*.asmdef`, `Tests/Runtime/*.asmdef` | asmdefs only. No test source files exist. |
| Samples | three `.unitypackage` archives in `Samples~/` | Extracted to scratch for reading. Contain the sample scripts (`DIInputManager.cs`, `RuntimeFFBConfigUI.cs`, `DirectInputControlPanel.cs`, NWH integration). |
| Explorer | `src~/DirectInputExplorer~/**` | WinForms, `net5.0-windows10.0.22000.0`, x64. Compiles `Runtime/DirectInputManager.cs` by link. Tracked `bin/` and `obj/` output including a 25 MB `Microsoft.Windows.SDK.NET.dll`. |
| Docs | `README.md`, `src~/DirectInputExplorer~/README.md`, `package.json` | No CLAUDE.md or AGENTS.md. |

Excluded from review: `.meta` files (Unity bookkeeping), tracked build output
under `bin/` and `obj/` (generated), sample scene and prefab YAML (data).

## Architecture map (VERIFIED)

```
Explorer Form1 (WinForms, UI thread, 20 ms Timer)
   |                                   Unity: DirectInputDevice (Input System device)
   v                                     |  FinishSetup -> DIManager.Attach
DIManager (static, Runtime/DirectInputManager.cs)  <--- OnUpdate -> DIManager.Poll
   |  P/Invoke (Ansi LPStr GUID strings, HRESULT as int)
   v
DirectInputForceFeedback.dll (global std::map state keyed by GUID string)
   |  IDirectInput8 / IDirectInputDevice8 / IDirectInputEffect
   v
dinput8.dll -> driver -> wheelbase
```

Device lifecycle: `StartDirectInput` -> `EnumerateDevices` (two `EnumDevices`
passes, second flags FFB) -> `CreateDevice(guid)` (`c_dfDIJoystick2`,
`SetCooperativeLevel(GetForegroundWindow(), EXCLUSIVE|BACKGROUND)`, `Acquire`)
-> `GetDeviceState` each poll (re-acquires on `DIERR_INPUTLOST`/`NOTACQUIRED`)
-> `CreateFFBEffect(type)` builds one `DIEFFECT` across every `DIDOI_FFACTUATOR`
object, `INFINITE` duration, gain 10000, direction 10000 on every axis, zero
type-specific parameters, then `Start(1, 0)` immediately -> `Update*` calls
`SetParameters(DIEP_TYPESPECIFICPARAMS)` -> `DestroyFFBEffect` Stop/Unload/
Release -> `DestroyDevice` -> `StopDirectInput`.

Threads: Explorer and Unity both call the DLL from the main thread. The DLL
has no locks. `DIManager.OnDeviceChange` is only ever invoked from
`_WindowsHookCallback`, and nothing in the current native code installs that
hook (see F3).

## Findings so far

| ID | Severity | Status | Where | What |
|----|----------|--------|-------|------|
| F1 | Critical | VERIFIED, fixed by revert | `Runtime/DirectInputManager.cs` at `7ae2121` | Commit deleted ~300 lines of type definitions still referenced 100+ times in the same file; package and Explorer cannot compile. |
| F2 | High | VERIFIED, removed by revert | same, `7ae2121`/`8a27281` | Rate limiter dropped (did not coalesce) any constant or condition update within 16 ms of the previous one, so a "set force to 0" could be silently discarded. `UpdateEffect` loop sent one native call per condition although the DLL already writes every axis on each call; second call always hit the limiter so the function reported failure. Axis validation compared `conditions.Length` with the string count returned by `EnumerateFFBAxes` (1 + 3 per axis) and leaked that string array on every call. |
| F3 | High | VERIFIED | `DirectInputForceFeedback.cpp` | `_WindowsHookCallback` exists but no `SetWindowsHookEx`/`RegisterDeviceNotification` call exists, so `g_deviceCallback` never fires. Hot-plug events (`OnDeviceAdded`/`OnDeviceRemoved`) are dead in both Unity and Explorer. History check pending. |
| F4 | High | VERIFIED | `Form1.cs` + `Form1.Designer.cs` custom tab | Every slider and numeric control on the custom-force tab carries `Tag = "CustomForceMagnitude"`; the shared handlers look up `"UD"+Tag` / `"Slider"+Tag`, find nothing, and dereference null. Any interaction with those controls throws `NullReferenceException` before any force data is sent. The tab is non-functional. |
| F5 | Medium | VERIFIED | `Form1.cs` `TimerPoll_Tick_1` | When devices exist but none is selected (`SelectedIndex == -1`, e.g. after the selected device is removed and the list refreshed) the 20 ms timer indexes `Devices[-1]` and throws every tick. |
| F6 | Medium | VERIFIED | `Runtime/DirectInputManager.cs` `DeviceInfo` | C# `bool FFBCapable` marshals as 4-byte `BOOL`; native field is 1-byte `bool`. Sizes coincide (48 bytes) only because of trailing padding; the read includes three padding bytes. Should be `[MarshalAs(UnmanagedType.I1)]`. |
| F7 | Medium | VERIFIED | `Runtime/DirectInputManager.cs` `Poll` | Every poll of every device computes an MD5 over a marshaled copy of the state (AllocHGlobal, byte[], MD5 instance) only to detect change. A direct struct comparison does the same with no allocation. Measurement pending. |
| F8 | Low | VERIFIED | `DirectInputForceFeedback.cpp` `CreateDevice` | Cooperative level uses `GetForegroundWindow()`, which can belong to another process; falls back to the desktop window. Exact DirectInput requirement to be confirmed against documentation. |
| F9 | Low | VERIFIED | `package.json` | Declares `"unity": "2017.0"` while the code uses C# 9 target-typed `new()` and `Math.Clamp`, which need Unity 2021.2 or newer. |

## Corroborating sources gathered so far

- Wine `include/dinput.h` (scratch `refs/wine_dinput.h`): struct layouts and
  constants (`DIEP_*`, `DIEFF_*`, `DIERR_*`, `DISFFC_*`, `DIGFFS_*`,
  `DI_FFNOMINALMAX`, `DI_SECONDS`). VERIFIED values match the native code.
- Wine `dlls/dinput/joystick_hid.c`: condition effects accept either one
  `DICONDITION` or `cAxes` of them (lines 2879-2880); a Cartesian direction
  is converted with `atan2`, so (10000, 10000) on two axes is a 45 degree
  diagonal. Corroboration only, not Microsoft documentation.
- SDL `src/haptic/windows/SDL_dinputhaptic.c`: uses its own hidden helper
  window for `SetCooperativeLevel(DISCL_EXCLUSIVE | DISCL_BACKGROUND)`, then
  `Acquire`, `SendForceFeedbackCommand(DISFFC_RESET)`; `dwFlags =
  DIEFF_OBJECTOFFSETS` ("Seems obligatory"), gain 10000, sample period 0; one
  `DICONDITION` per axis; counts FFB axes as `DIDFT_AXIS && DIDOI_FFACTUATOR`;
  for a steering axis sets `DIEFF_CARTESIAN` with an all-zero direction.
- SDL `src/joystick/windows/SDL_windowsjoystick.c`: device arrival and removal
  via a message-only window plus `RegisterDeviceNotification(
  DBT_DEVTYP_DEVICEINTERFACE, GUID_DEVINTERFACE_HID)`, handling
  `DBT_DEVICEARRIVAL` and `DBT_DEVICEREMOVECOMPLETE`.
- Microsoft "Effect Direction" page (search snippet only, page itself not
  reachable from this container): single-axis effect uses `DIEFF_CARTESIAN |
  DIEFF_OBJECTOFFSETS`, `cAxes = 1`, one direction element holding 0; a
  two-axis Cartesian effect uses a vector such as {1, 0}. PROVISIONAL until
  the page can be read in full.
- Root commit `f331b45` native code: installed a `WH_CALLWNDPROC` hook on the
  calling thread in `StartDirectInput` and used `FindMainWindow(pid)`
  (EnumWindows filtered by process id) for the cooperative level. Commit
  `6f460a4` ("Hotfix", 2025-03-14) removed the hook installation and replaced
  the window lookup with `GetForegroundWindow()`, citing an asset-database
  refresh crash and window acquisition failures. Direction vector was all
  zeros until `8a4e3d7` set every element to 10000.

## Measurements (VERIFIED, .NET 8 on Linux x64, Release, 200k iterations)

| Path | ns per op | bytes allocated per op |
|------|-----------|------------------------|
| `FlatJoyState2.GetHashCode` (MD5 over marshaled copy, current Poll path) | 4814 | 432 |
| `ValueType.Equals` (reflection, rejected) | 7595 | not measured |
| explicit field-wise comparison (candidate) | 18.8 | 0 |

Runtime differs from Unity Mono/IL2CPP; the ratio (about 250x) and the zero
allocation are the transferable results, not the absolute numbers.

## Build and test status (VERIFIED in this container)

- Explorer `dotnet build -c Release -p:EnableWindowsTargeting=true` with the
  Microsoft-built .NET 8 SDK (8.0.425, extracted to scratch, no system
  install): PASSED, 0 errors, 3 warnings (all NETSDK1138 end-of-life notice
  for net5.0). Ubuntu's source-built SDK lacks the WindowsDesktop SDK and
  cannot build it.
- New test project `src~/DirectInputManager.Tests~` (xunit, net8.0, links the
  real `Runtime/DirectInputManager.cs`): 4 layout tests PASSED, the one-byte
  bool test FAILED as expected (finding F6), benchmark PASSED.
- Native layout cross-check (`scratch/abi/native_layout.c`, clang):
  DeviceInfo 48 bytes, FFBCapable at 40; FlatJoyState2 88, rgdwPOV at 80;
  DIJOYSTATE2 272; DIDEVCAPS 44. Matches the managed assertions.

## Environment baseline (VERIFIED)

- Linux container, no Windows, no Unity, no `.NET` SDK, no MSVC or MinGW, no
  attached devices. `gcc 13`, `clang 18`, `cmake 3.28`, Python 3.13, Node 22.
- `dot.net`/`builds.dotnet.microsoft.com` is denied by the egress proxy, so
  the managed projects cannot be compiled here yet. Native code cannot be
  compiled here (needs Windows SDK `dinput.h` and ATL).
- Consequence: build, run, screenshot, and hardware checks are BLOCKED in this
  environment and are listed as such in the final handoff.

## Next steps

1. Parallel deep-dives: sample consumers, DirectInput docs ledger, Unity
   lifecycle docs, adversarial reviews of native, managed, and Explorer code.
2. Fix batch 1 (managed): marshaling, poll hashing, duplicate `using`.
3. Fix batch 2 (native): device-change notification, effect/direction setup,
   documented cooperative level.
4. Fix batch 3 (Explorer): custom-force tab contract, index guards, layout.
5. Tests, docs, handoff.
