# Crunch v1.0.0 — Release Notes

Initial public release of **Crunch**, a fast, portable disk cleanup and storage analyzer for Windows 10 and Windows 11.

---

## Features

### Storage Overview & Quick Clean
- Real-time disk meter with partition usage statistics.
- One-click cleaning for user temp folders, system temp files, Windows Update download cache, and crash dumps.
- Cleans Discord media caches and Chrome/Edge browser caches without touching user sessions or cookies.
- Live selection counter to preview reclaimed disk space before applying changes.

### Large & Dormant File Scanner ("Sleeping Giants")
- Identifies files over 100 MB that have not been modified or accessed in months.
- Groups files into ISOs, installer executables, video captures, and compressed archives.
- File Explorer integration to reveal and inspect items in place.

### Developer Bloat & Leftover App Data
- Hunts down unneeded build artifacts and package dependencies (`node_modules`, `target`, `.venv`, and `bin`/`obj`).
- Scans `AppData` and `ProgramData` for abandoned folders left behind by uninstalled applications by cross-referencing registered Windows uninstall keys.

### Duplicate File Finder
- Multi-tier matching strategy: file size grouping, followed by head/tail chunk checks, and full `XxHash64` validation.
- Automatically keeps the primary file safe while selecting duplicate copies for removal.

### Force Delete ("The Black Hole")
- Drag-and-drop target to delete stubborn files and folders.
- Resolves file lock conflicts using the Windows Restart Manager API.

---

## Downloads & Checksums

| File | Type | SHA256 Checksum |
| :--- | :--- | :--- |
| **`Crunch-v1.0.0-win-x64.zip`** | Portable Archive (~62 MB) | `18F37ECFE5EF0160AE4DD90471547ED166669315BDBFD7A31EDBC1767D376A5A` |
| **`Crunch.exe`** | Standalone Executable (~68 MB) | `9FA93083D29142058856687D8270B72DFC374EE64D820DF777FCFD92FD8EA1C6` |

### System Requirements
- **OS:** Windows 10 (version 1809 or later) / Windows 11 (64-bit)
- **Runtime:** Self-contained (no external .NET installation required)
- **Permissions:** Standard user permissions (Administrator elevation required only for protected system directories like `C:\Windows\Temp`)

---

## Running Crunch

1. Download **`Crunch-v1.0.0-win-x64.zip`** or `Crunch.exe` from the releases section.
2. Extract and run `Crunch.exe`. No installation required.
