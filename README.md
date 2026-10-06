# AdaptNess

AdaptNess is a lightweight Windows system-tray utility that adjusts the primary laptop display’s brightness from the visual brightness of the content currently on screen.

Dark content maps to a higher panel brightness and bright content maps to a lower one. AdaptNess runs locally in the background and can be enabled, disabled, paused, or exited from its tray icon.

## Privacy

Screen analysis is fully local, transient, and in memory only.

- No screenshots are saved.
- No screen data is uploaded, streamed, or transmitted.
- No network services, telemetry, analytics, accounts, OCR, AI models, webcam, or microphone access are used.
- Diagnostic logs contain operational events only; they never contain screen images, pixels, or screen content.

## Features

- Primary-display brightness control through the Windows WMI monitor APIs.
- Small native in-memory display sample (32×18) and perceived-luminance analysis.
- Inverted, smoothed adaptive brightness mapping with bounded 28–50% automatic output by default.
- Hysteresis, rate limiting, monotonic convergence, and protection against treating AdaptNess writes as manual brightness changes.
- System-tray enable/disable control, 15-minute pause, settings, and clean exit.
- Persisted minimum/maximum automatic brightness, enabled state, and optional Start with Windows.
- Single-instance protection.

## Requirements

- Windows 11 (Windows 10 may work, but is not the primary target).
- .NET SDK 10 or a compatible .NET 10 runtime.
- A display whose internal-panel brightness is exposed through `root\WMI` `WmiMonitorBrightness` and `WmiMonitorBrightnessMethods`.

AdaptNess is intended for the primary internal display. External monitors and multi-monitor selection are not supported in this first release.

## Installation and build

From Windows PowerShell, using the Windows .NET SDK:

```powershell
Set-Location '\\wsl$\Ubuntu-24.04\home\lenovowsl\Development\Projects\AdaptNess'
dotnet build -c Release
```

Run the release build:

```powershell
dotnet run -c Release --project .\AdaptNess.csproj
```

The program starts in the notification area. It does not need a console window during normal use.

## Usage

- Left-click the tray icon to toggle adaptive brightness.
- Right-click it for Enable/Disable, Pause for 15 minutes, Settings, and Exit.
- Choose **Settings** to set the automatic minimum and maximum brightness and optionally enable Start with Windows.
- Use **Exit** to stop analysis and release resources cleanly.

When adaptive brightness is disabled or paused, AdaptNess stops screen capture and analysis and leaves the current physical brightness unchanged.

## Configuration

Defaults:

- Automatic range: 28–50%
- Analysis frequency: 4 Hz
- EMA smoothing: 0.7 seconds
- Deadband: 4 brightness points
- Maximum automatic step: 4 brightness points

Settings and the optional local diagnostic log are stored under `%LocalAppData%\AdaptNess`. The log is bounded to 512 KB and contains no screen content.

Start with Windows is disabled by default. When enabled, AdaptNess writes a per-user entry under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; it does not require administrator privileges or install a service.

## How it works

AdaptNess samples the primary desktop directly into a small native buffer with Win32 GDI, calculates perceived luminance (`0.2126R + 0.7152G + 0.0722B`), then applies exponential smoothing and target hysteresis. Brightness changes are rate-limited and always step monotonically toward the current stable target.

Brightness is read and set through the Windows WMI monitor classes. Recent automatic WMI writes are tracked so their readback is not mistaken for a manual user adjustment. A genuine manual adjustment becomes a bounded user preference offset.

## Known limitations

- Primary display only.
- Requires WMI brightness support from the display/driver.
- Uses the current native GDI sampling implementation; it is intentionally kept simple for this MVP.
- No installer, system-tray customization, or advanced configuration profiles.

## Development setup

The source repository is managed in WSL, but AdaptNess is built and run natively from Windows PowerShell using the Windows .NET SDK. No Visual Studio, Git for Windows, or .NET installation in WSL is required.

Run the built-in deterministic checks:

```powershell
dotnet run --project .\AdaptNess.csproj -- --self-test
```

There is no separate test project in this MVP; use the self-tests above for the adaptive-control and luminance checks. The tray build does not open a console, so the command reports success through its exit code and writes the result to `%LocalAppData%\AdaptNess\adaptNess.log`.
