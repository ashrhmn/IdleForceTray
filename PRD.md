# PRD: IdleForce Tray for Windows

## 1) Product summary

A small Windows tray application that watches real user input from keyboard, mouse, and game controllers, then forces the system to Sleep or Shutdown after a configurable idle timeout. It ignores app and driver power requests that would normally block Sleep. Designed for gaming PCs where the user wants PlayStation style auto suspend.

## 2) Goals

* Force Sleep or Shutdown after N minutes without real input.
* Respect only human input. Do not reset the timer for background CPU or network activity.
* One-click control from the tray: pause, change timeout, Sleep now, Shutdown now.
* Lightweight, reliable, minimal footprint, zero extra dependencies.

## 3) Non-goals

* Media mode exceptions. The app will sleep even during video playback if idle.
* Multi-user orchestration across sessions.
* Enterprise policy management or group policy integration.

## 4) Target environment

* OS: Windows 10 21H2 or later, Windows 11 22H2 or later.
* .NET: .NET 8 for WinForms tray app.
* Hardware: typical gaming PC with XInput compatible controllers. Works without a controller as well.

## 5) Primary user stories

* As a gamer, I want my PC to auto sleep after 15 to 30 minutes with no input so I do not waste power.
* As a user, I want a tray menu to change timeout, pause the app, or trigger Sleep or Shutdown instantly.
* As a power user, I want it to ignore games that keep the system awake with power requests.

## 6) Functional requirements

### 6.1 Idle detection

* Keyboard and mouse: use GetLastInputInfo to read the timestamp of the last user input in the current session.
* Controller activity: poll XInputGetState for up to 4 controllers. If any state's packet number changes between polls, treat as user input.
* Optional enhancement: raw HID polling for non XInput controllers is out of scope for v1. Provide an internal flag and interface to add later.

### 6.2 Timer logic

* Configurable timeout in minutes. Defaults: 20.
* Poll interval: 5 seconds.
* Reset the internal lastActivity timestamp when:

  * GetLastInputInfo reports recent input within poll interval plus 1 second.
  * Any XInput controller packet number changed.
* When now minus lastActivity is greater than or equal to timeout:

  * If mode is Sleep: call SetSuspendState with force flag to ignore blockers. Then wait 120 seconds after resume before checking again.
  * If mode is Shutdown: shell out to shutdown.exe with "/s /t 0", then exit after 10 minutes watchdog delay.

### 6.3 Force Sleep behavior

* Default: Sleep. Not Hibernate.
* Option: Guaranteed Sleep mode. On first run, if enabled, run `powercfg -h off` once to prevent SetSuspendState from hibernating. Expose a toggle in the tray menu with a confirmation dialog.

### 6.4 Tray UI

* NotifyIcon in system tray.
* Tooltip shows state and timeout, for example: "IdleForce: running, Sleep in 20m".
* Context menu items:

  * Status label: non interactive.
  * Mode submenu: Sleep or Shutdown.
  * Timeout submenu: 10m, 15m, 20m, 30m, 45m.
  * Pause or Resume.
  * Sleep now.
  * Shutdown now.
  * Guaranteed Sleep toggle.
  * Start with Windows toggle.
  * Exit.
* On change, update the status label and tooltip.

### 6.5 Configuration

* Store settings in JSON at `%APPDATA%\IdleForce\settings.json`.
* Schema:

  ```json
  {
    "Mode": "Sleep",
    "TimeoutMinutes": 20,
    "CheckIntervalSeconds": 5,
    "GuaranteedSleep": false,
    "StartWithWindows": true,
    "LogLevel": "Info",
    "FirstRunCompleted": false
  }
  ```
* Validate values on load. If invalid, fall back to defaults and write corrected values.

### 6.6 Startup behavior

* Toggle Start with Windows from the tray.
* Implementation: a Startup shortcut in `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`. Prefer this over Task Scheduler for per user session presence. If the user selects Task Scheduler, run as the current user with "Run only when user is logged on".

### 6.7 Logging

* File based logging in `%LOCALAPPDATA%\IdleForce\logs\IdleForce.log`.
* Log levels: Error, Warn, Info, Debug.
* Rotate at 1 MB, keep 3 files.
* Do not log keystrokes or button states. Only log high level events such as state changes, sleep invocations, errors.

### 6.8 Safety and UX rules

* Never send input or simulate keys.
* If SetSuspendState fails, log the error and retry once after 5 seconds. If still failing, show a balloon tip: "Sleep failed. Check power settings."
* During Pause, idle tracking continues but no actions are taken.
* On Exit, dispose timers and hide the tray icon cleanly.

### 6.9 Accessibility and localization

* English only for v1.
* All menu items have text labels. No dialogs require keyboard navigation for basic use.

## 7) Non functional requirements

* Performance: CPU under 0.2 percent average on modern CPUs. Working set under 20 MB.
* Reliability: run for weeks without memory growth. Handle missing XInput DLLs gracefully by probing several DLL names.
* Security: no admin rights required for core features. Only Guaranteed Sleep toggle needs elevation when calling `powercfg -h off`.
* Privacy: no network access. No telemetry.

## 8) Technical design

### 8.1 Key APIs

* `BOOL GetLastInputInfo(PLASTINPUTINFO)`.
* `DWORD XInputGetState(DWORD dwUserIndex, XINPUT_STATE* pState)` from xinput1\_4, fallback to xinput1\_3, fallback to xinput9\_1\_0.
* `BOOLEAN SetSuspendState(BOOLEAN Hibernate, BOOLEAN ForceCritical, BOOLEAN DisableWakeEvent)` from powrprof.dll.
* Shell out to `shutdown.exe /s /t 0` for Shutdown mode.
* For startup toggle: create or delete a `.lnk` in the Startup folder using COM ShellLink.

### 8.2 Error handling

* Wrap PInvoke calls in try or catch and return safe defaults.
* When all XInput DLLs fail to load, controller tracking silently disables, keyboard and mouse still work.
* If SetSuspendState returns false, capture `Marshal.GetLastWin32Error()` and log.

### 8.3 Process model

* User mode app in the interactive session. Do not run as a service. Services live in session 0 which cannot read interactive input timestamps reliably.
* Single instance. Use a named mutex `Global\IdleForceTray` to prevent duplicates.

### 8.4 Build and packaging

* Project type: WinForms. Single Program.cs file is acceptable.
* Publish options:

  * Self contained win-x64. Single file. Trim enabled.
  * Optional Native AOT publish to reduce startup and working set.
* Deliverable: IdleForceTray.exe plus a small icon resource.
* Optional MSI or ZIP. For v1, ZIP is enough.

### 8.5 File layout

```
%APPDATA%\IdleForce\settings.json
%LOCALAPPDATA%\IdleForce\logs\IdleForce.log
Startup shortcut in user Startup folder if enabled
```

## 9) Detailed UX spec

### 9.1 Tray tooltip states

* Running: "IdleForce: running, Sleep in Xm" or "IdleForce: running, Shutdown in Xm".
* Paused: "IdleForce: paused".
* After action: show Windows toast or balloon tip "System sleeping now" or "Shutting down now" when available.

### 9.2 Dialogs

* Guaranteed Sleep toggle prompts: "This turns off Hibernate to ensure Sleep. Continue" Yes or No. If Yes and elevation is needed, request elevation.

## 10) Data model

* In memory state:

  * `DateTime lastActivityUtc`
  * `TimeSpan timeout`
  * `bool paused`
  * `Mode mode` enum with Sleep or Shutdown
  * `bool guaranteedSleep`
* Settings model maps 1 to 1 with JSON schema.

## 11) Pseudocode for core loop

```
load settings
init tray UI and menu bindings
seed controller packet numbers

timer every CheckIntervalSeconds:
    if paused: return
    idle = GetLastInputInfoIdle()
    if idle <= CheckIntervalSeconds + 1: lastActivity = now
    if AnyXInputPacketChanged(): lastActivity = now
    if now - lastActivity >= timeout:
        if mode == Shutdown:
            run "shutdown.exe /s /t 0"
            sleep 120 seconds
        else:
            SetSuspendState(Hibernate=false, ForceCritical=true, DisableWakeEvent=false)
            sleep 120 seconds
        lastActivity = now

on GuaranteedSleep toggle:
    if enabling:
        run "powercfg -h off" with elevation
    update setting
```

## 12) Acceptance criteria

### 12.1 Functional

* With keyboard or mouse input, the internal timer resets within one poll cycle.
* With an XInput controller, small stick or button movement resets the timer.
* After the configured timeout with no input, the machine goes to Sleep even if a fullscreen game is running that would normally block Sleep.
* Shutdown mode powers off cleanly after timeout.
* Pause stops actions while the timer continues tracking.
* Changing timeout or mode from the tray applies immediately and persists to settings.json.

### 12.2 Reliability

* Working set remains under 20 MB after 24 hours and after 100 sleeps.
* No unhandled exceptions in logs after continuous operation for 7 days.
* Clean restart after system resume or after explorer.exe restart.

### 12.3 UX

* Tray shows the correct state and updates immediately when settings change.
* Startup toggle works. App is present after next logon if enabled.

## 13) Test plan

### 13.1 Unit tests

* Idle timer math with fake clocks.
* Settings read and write with invalid values.

### 13.2 Integration tests

* Mock XInput layer that simulates packet changes.
* Wrapper around GetLastInputInfo for simulated idle times.

### 13.3 Manual tests

* Fullscreen DirectX game running, no input. Verify Sleep triggers at timeout.
* Game with heavy CPU and GPU load. Verify Sleep still triggers.
* Video playback in browser. Verify Sleep triggers.
* Controller only session. Move thumbstick periodically and confirm timer resets.
* Enable Guaranteed Sleep, verify hibernate is disabled, then confirm Sleep not Hibernate.
* Fast wake and repeated cycles: 20 sleep and resume cycles without crash.
* Startup toggle on and off. Verify behavior across reboots.

### 13.4 Edge cases

* No XInput DLL available. Confirm keyboard and mouse still work.
* System with BitLocker. Confirm SetSuspendState still sleeps. If preboot auth prompts after hibernate, guide user to Guaranteed Sleep.
* Remote Desktop session. App runs only in interactive console session. Document that behavior.

## 14) Risks and mitigations

* Risk: Some OEM power policies override suspend. Mitigation: log return codes and show a tip if Sleep fails.
* Risk: App sleeps during long file operations. This is desired. Document clearly.
* Risk: User expects media exceptions. Document non-goal and provide Pause.

## 15) Telemetry and privacy

* No network calls. Logs are local only.
* Do not capture input contents. Never store key or button values.

## 16) Developer checklist for LLM code generation

* Create WinForms project named IdleForceTray on .NET 8.
* Add PInvoke for GetLastInputInfo, SetSuspendState, XInputGetState with three DLL names.
* Implement named mutex for single instance.
* Implement tray NotifyIcon with context menu and event handlers.
* Implement settings persistence with JSON.
* Implement startup toggle using ShellLink. Provide fallback Task Scheduler method if shortcut creation fails.
* Implement logging with rolling files.
* Implement timer loop with try or catch protection.
* Implement elevation prompt helper for Guaranteed Sleep toggle.
* Write unit tests for idle math and settings.
* Provide publish profile for single file self contained win-x64, trim true. Optional AOT profile.

## 17) Out of scope for v1

* Raw HID controller support.
* Per app allowlist or blocklist.
* Scheduling windows, for example different timeouts at night.

---

## Stats for nerds

Model: GPT-5 Thinking
Knowledge cutoff: 2024-06
Browsing: not used
Estimated tokens: 950 to 1200 total
Generation settings: default system settings
