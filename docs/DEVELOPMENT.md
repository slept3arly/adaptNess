# Development

## Tools and checkout

- WSL with the repository checkout and Git.
- Windows 10 or later.
- The Windows .NET 10 SDK with the Windows Desktop targeting pack used by WinForms.
- A WMI-capable display is needed for runtime brightness checks.

The intended setup keeps source and Git in WSL and invokes the Windows SDK from Windows PowerShell through the checkout's WSL UNC path. AdaptNess does not require Git for Windows or a .NET SDK installed in WSL. Replace the example path below with the path for your WSL distribution and checkout.

```powershell
Set-Location '\\wsl.localhost\Ubuntu-24.04\home\lenovowsl\Development\Projects\AdaptNess'
```

## Build and self-tests

Run these commands in Windows PowerShell from the repository directory:

```powershell
dotnet --info
dotnet build .\AdaptNess.csproj
dotnet run --project .\AdaptNess.csproj -- --self-test
if ($LASTEXITCODE -ne 0) { throw "AdaptNess self-tests failed: $LASTEXITCODE" }
dotnet build .\AdaptNess.csproj -c Release
```

The self-test command runs the application's internal deterministic checks and exits without starting the tray UI. `WinExe` does not provide a normal console result. Check `$LASTEXITCODE`; the app also records `Self-tests passed.` or the failure details in `%LocalAppData%\AdaptNess\adaptNess.log` when logging is available.

Debug and Release outputs go under `bin\Debug\net10.0-windows` and `bin\Release\net10.0-windows`. The Release build is framework-dependent; it is not an installer or a self-contained publish.

## Runtime checks on Windows

Run `dotnet run --project .\AdaptNess.csproj` from PowerShell. Confirm the tray icon appears, then check enable/disable, the 15-minute pause, settings save/cancel, and Exit. The pause lasts 15 minutes, so a short runtime check can instead verify that pause stops brightness updates and that enabling again resumes them. Check the local log for startup, capture, luminance, target, and WMI messages.

To inspect whether the WMI brightness classes return data on a machine, run:

```powershell
Get-CimInstance -Namespace root\WMI -ClassName WmiMonitorBrightness
Get-CimInstance -Namespace root\WMI -ClassName WmiMonitorBrightnessMethods
```

These queries help diagnose hardware support; they do not verify that a brightness write changes the panel. Verify brightness behavior on the actual display and driver. Also verify that Start with Windows creates or removes `HKCU:\Software\Microsoft\Windows\CurrentVersion\Run\AdaptNess`, and that a second launch reports that AdaptNess is already running.

The self-tests do not validate the desktop capture, WMI writes, registry access, or live tray behavior. Those need a Windows desktop session and compatible hardware.

## Common problems

- **`NETSDK...` says the target framework is missing:** install the Windows .NET 10 SDK and confirm PowerShell resolves the Windows `dotnet` executable with `Get-Command dotnet`.
- **WindowsDesktop reference packs or WinForms targets cannot be resolved:** run the build with the Windows SDK. A WSL .NET installation is not the supported build environment for this project.
- **WMI returns no brightness object:** the display or driver may not expose the required WMI class. Check both CIM queries above and the log. AdaptNess does not provide a fallback brightness API.
- **A WMI write fails:** inspect the exception in `adaptNess.log`, confirm the brightness-method query returns an instance, and test on the built-in display. Write failures stop the current engine run.
- **Settings or logs cannot be written:** check access to `%LocalAppData%\AdaptNess`; settings errors surface in the UI, while log write errors are intentionally ignored.
- **The tray icon does not appear:** confirm the process is running and Windows has not placed the icon in the overflow area. A second process exits after showing an already-running message.

## Changing a setting

The user-facing settings are `MinimumBrightness`, `MaximumBrightness`, `AdaptiveEnabled`, and `StartWithWindows` in `AppSettings`. When adding or changing one, update the settings form, normalization and persistence behavior, and the apply path in `AdaptNessApplicationContext`. Check whether a changed value must restart the engine so the new value reaches its worker-owned controllers. Keep optional constructor defaults and normalization consistent with existing saved `settings.json` files. Add or adjust a self-test for deterministic validation where practical, then verify settings load/save on Windows.

Analysis interval, smoothing time, update interval, deadband, and maximum step currently live in `AdaptiveBrightnessSettings`; they are implementation constants, not persisted user settings. Decide explicitly whether a proposed change should remain fixed or become part of `AppSettings` before exposing it in the UI.

## Git workflow

Run Git commands in WSL. Make a focused branch for each change, inspect `git diff` and `git diff --check`, and keep generated `bin` and `obj` output out of commits. Run builds, self-tests, and hardware checks from Windows PowerShell. Record the commands and outcomes in the change description. Do not commit local settings, logs, or generated build output.
