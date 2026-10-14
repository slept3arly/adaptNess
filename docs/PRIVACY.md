# Privacy and security

AdaptNess analyzes a reduced sample of the primary desktop to choose a brightness target. The capture buffer is created in memory, analyzed, and discarded by the worker on its next iteration. The source does not write captured pixels to disk or contain a network client or upload path. This describes the application code; it does not make claims about unrelated Windows components or other software on the computer.

## Local data

The application stores two files under `%LocalAppData%\AdaptNess`:

- `settings.json` contains the automatic brightness minimum and maximum, whether adaptation is enabled, and whether Start with Windows is enabled.
- `adaptNess.log` contains timestamps, operational messages, luminance percentages, mapped and requested brightness values, readbacks, and exception type/message text. The log is truncated when it is larger than 512 KiB before a new append.

The log does not receive the captured pixel buffer or an image. Exception text is produced by Windows and .NET APIs and may contain machine-specific details. The app also writes messages to `Debug.WriteLine`.

## Windows integration

Brightness control queries WMI in `root\WMI` and uses `WmiMonitorBrightnessMethods` to request changes. Availability and behavior depend on the display driver and machine configuration. The project has no app-specific elevation manifest and does not install a service.

When Start with Windows is enabled in Settings, AdaptNess writes its launch command to the current user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` key. Disabling the option removes that value. The startup preference is also stored in `settings.json`.

## Data not collected by the application

There is no account system, telemetry, analytics, OCR, or remote service integration in the application source. AdaptNess does not send screen samples or settings over a network. A user can remove local settings and logs by deleting `%LocalAppData%\AdaptNess`; the startup registry value is separate and should be disabled in Settings before removing the application files.
