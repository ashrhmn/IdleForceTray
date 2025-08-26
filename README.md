# IdleForce Tray

A Windows tray application that monitors user input and forces system sleep or shutdown after a configurable idle timeout, inspired by PlayStation's power saving mode. Perfect for gamers who leave their PC paused and return hours or days later to find it still running.

## Features

- Monitors keyboard, mouse, and XInput game controllers
- Forces Sleep or Shutdown after configurable timeout (default 20 minutes)
- Tray menu for easy control: pause, change timeout, instant sleep/shutdown
- Ignores background activity that normally prevents sleep
- Lightweight with minimal system impact
- Start with Windows option

## Installation

1. Download the latest `IdleForceTray.exe` from [GitHub Releases](https://github.com/ashrhmn/IdleForceTray/releases/latest)
2. Run the executable (no installation required)
3. The app will appear in your system tray

## Usage

- Right-click the tray icon to access the menu
- Configure timeout, mode (Sleep/Shutdown), and other settings
- Use "Pause" to temporarily disable idle detection
- Use "Sleep now" or "Shutdown now" for immediate action

## Configuration

Settings are stored in `%APPDATA%\IdleForce\settings.json`

## Requirements

- Windows 10 21H2 or later, Windows 11 22H2 or later
- .NET 8 runtime (included in self-contained build)

## Contributing

Contributions welcome! Please see the PRD.md for detailed requirements.

## License

MIT License
