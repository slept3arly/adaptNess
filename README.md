# AdaptNess

AdaptNess is a Windows system tray application that adjusts display brightness from the average luminance of the primary desktop. Darker screen content maps to a higher brightness target; brighter content maps to a lower one.

The project targets Windows and .NET 10. The current project version is 0.1.0. This is an early source project; the checkout contains no installer or release binaries.

## Features

- Samples the primary desktop into a 32×18 in-memory buffer and calculates perceived luminance.
- Smooths the measured luminance and moves brightness toward a stable target in bounded steps.
- Reads and sets brightness through the Windows WMI monitor brightness classes.
- Provides tray controls to enable or disable adaptation, pause it for 15 minutes, open settings, or exit.
- Saves the automatic brightness range, enabled state, and optional Start with Windows preference.
- Prevents a second AdaptNess process from starting.

## Requirements and hardware support

- Windows 10 or later and the .NET 10 SDK to build.
- The .NET 10 Windows Desktop Runtime to run a framework-dependent build.
- A display driver that exposes the WMI classes `WmiMonitorBrightness` and `WmiMonitorBrightnessMethods` in `root\WMI`.

Screen sampling uses the primary desktop. AdaptNess does not offer display selection. For brightness, it reads the first WMI monitor brightness result and sends a write to each brightness-method instance returned by WMI. Actual behavior therefore depends on the display and driver; external displays and multi-display setups are not selectable or specifically supported.

## Build and run

The repository can be kept in WSL while builds and application runs use the Windows .NET SDK. In Windows PowerShell, change to this checkout's WSL UNC path, then run:

```powershell
Set-Location '\\wsl.localhost\Ubuntu-24.04\home\lenovowsl\Development\Projects\AdaptNess'
dotnet build .\AdaptNess.csproj
dotnet run --project .\AdaptNess.csproj
```

Replace the example path with the UNC path for your WSL distribution and checkout. The normal application is a `WinExe`, so it starts without a console window.

The self-tests run before the tray application starts and return a process exit code:

```powershell
dotnet run --project .\AdaptNess.csproj -- --self-test
if ($LASTEXITCODE -ne 0) { throw "AdaptNess self-tests failed: $LASTEXITCODE" }
```

Create a Release build with:

```powershell
dotnet build .\AdaptNess.csproj -c Release
```

The executable and supporting build output are placed under `bin\Release\net10.0-windows`. This is a build output, not a packaged installer. The project does not enable self-contained publishing.

## Tray controls and settings

Left-click the tray icon to toggle adaptive brightness. Right-click it to enable or disable adaptation, pause for 15 minutes, open Settings, or exit. While disabled or paused, capture and analysis stop and AdaptNess does not issue brightness updates. The 15-minute pause then re-enables adaptation if the application is still running.

Settings let you choose the minimum and maximum automatic brightness, enable or disable adaptation, and set whether AdaptNess starts with Windows. The default automatic range is 28–50%; values are constrained to 1–99%, with the maximum above the minimum. Other control values are fixed in the current implementation: 250 ms analysis interval, 0.7 s exponential smoothing time, 4-point target deadband, 650 ms minimum between brightness writes, and a maximum 4-point step.

Settings and the log are stored in `%LocalAppData%\AdaptNess` (`settings.json` and `adaptNess.log`). Start with Windows is off by default. When enabled, AdaptNess writes a per-user value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; it does not install a service.

## Privacy

Screen samples are processed in memory; the application does not save the captured pixel buffer. The source contains no network client or upload path. The local log records operational data, including luminance measurements, brightness targets and readbacks, and exception messages. Logging failures are ignored so they do not stop display control.

For details about capture, local data, and startup registration, see [Privacy and security](docs/PRIVACY.md).

## Limitations

- The capture source is the primary desktop and there is no display selector.
- Brightness control requires compatible WMI support from the display driver. AdaptNess cannot make that interface available when the hardware or driver does not expose it.
- Screen capture can fail; while it does, the current brightness is held and capture is retried.
- There is no installer, automatic updater, profile system, or release artifact in this checkout.

## Development

Source and Git are managed in WSL; use the Windows host's .NET SDK to build, run, and check the app. No .NET SDK or Git for Windows installation in WSL is required. See [Development](docs/DEVELOPMENT.md) for setup, commands, and troubleshooting. The implementation and runtime flow are described in [Architecture](docs/ARCHITECTURE.md).
