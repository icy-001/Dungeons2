# Dungeons2

A real-time overlay for the dungeoneering in Runescape 3. It automatically captures the game screen, reads the in-game minimap and HUD, and provides players with a live annotated map, floor analytics, and performance tracking.

> ⚠️ Early development (v0.1.0)

---

## Features

- **Auto-Calibration** — Automatically detects map and HUD positions by searching for calibration markers on screen
- **Live Overlay** — Transparent, click-through overlay that follows the game window, showing room tick counts, RPM, dead ends, and unlockable doors
- **Real-Time Map Capture** — Hooks into the game process using Windows Graphics Capture API
- **Annotated Map** — Renders a full map with opened/locked/unknown rooms, critical path highlighting, key locations, skill doors, player positions, and tick indicators
- **Critical Path Analysis** — Computes the critical path from base to boss, identifying mandatory rooms and keys
- **Room Tick Tracking** — Measures time spent per room in ticks (1 tick = 0.6 seconds)
- **Floor History** — Persists completed floors to `floors.jsonl` and `floors.txt`, with map images saved as PNGs
- **Analytics Dashboard** — Histograms for room ticks, key counts, critical rooms, depth, dead ends; scatter plots for correlations
- **Google Sheets Integration** — Optionally logs floor completion times to a Google Sheets spreadsheet

---

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Windows 10 (build 26100) or later
- Visual Studio 2022+ or VS Code with C# Dev Kit (optional)

---

## Build & Run

```bash
# Clone the repository
git clone https://github.com/your-username/Dungeons2.git
cd Dungeons2

# Build (Debug mode)
dotnet build
# Build (Release mode)
dotnet build -c Release

# Run
dotnet run
```

---

## Google Sheets Setup

1. Create a Google Cloud service account and download the credentials JSON key
2. Rename it to `google-credentials.json` and place it in the application directory
3. Enable `LogToSheet` in settings and provide your `SpreadsheetId`

---

## Tech Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 10.0 | Runtime platform |
| WPF | (UseWPF) | Desktop UI framework |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM source generators |
| Vortice.Direct3D11 | 3.8.3 | DirectX 11 screen capture |
| Google.Apis.Sheets.v4 | 1.75.0 | Google Sheets integration |
| System.Text.Json | (built-in) | Data serialization |

---

## Output Files

| File | Description |
|------|-------------|
| `floors.jsonl` | Floor completion data (JSON Lines format) |
| `floors.txt` | Floor summary (tab-separated values) |
| `errors.log` | Application error log |
| `Maps/*.png` | Saved map screenshots |
| `Winterfaces/*.png` | Saved end-of-floor screenshots |

---

## License

MIT
