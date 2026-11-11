# Dungeons2

A real-time overlay for Dungeoneering in RuneScape 3. It automatically captures the game window, reads the in-game minimap and HUD, and provides players with a live annotated map, floor analytics, and performance tracking.

> ⚠️ Early development (v0.2.5)

---

## Features

- **Auto-Calibration** — Detects map and HUD positions from on-screen markers; also automatically locates the end-of-floor winterface
- **Live Overlay** — Transparent, click-through overlay that follows the game window, showing room tick counts, RPM, dead ends, and unlockable doors
- **Real-Time Map Capture** — Captures the game window using the Windows Graphics Capture API, without injecting into the game process
- **Annotated Map** — Renders a full map with opened/locked/unknown rooms, critical path highlighting, key locations, skill doors, player positions, and tick indicators
- **Critical Path Analysis** — Computes the critical path from base to boss, identifying mandatory rooms and keys
- **Room Tick Tracking** — Measures time spent per room in ticks (1 tick = 0.6 seconds)
- **Floor History** — Persists completed floors to `floors.jsonl` and `floors.txt`, saves map and winterface PNGs, and shows the saved winterface in floor details when available
- **Analytics Dashboard** — Theme-based room-tick histograms and modeled floor-time CDFs with hover readouts and average recorded floor times; a separate miscellaneous section covers key counts, critical rooms, depth, and dead ends
- **Google Sheets Integration** — Optionally logs floor completion times to a Google Sheets spreadsheet

---

## Requirements

### Running a published build

- Windows with Windows Graphics Capture support. The project's declared API minimum is Windows 10 build **19041**; **26100** is the compile-time Windows SDK target, not the minimum OS build. Use a Windows release covered by the [.NET 10 OS support policy](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md); API compatibility alone does not imply current Microsoft support.
- The included publish profile produces a **self-contained Windows x64** build: no separate .NET SDK or runtime installation is needed for that output. Framework-dependent builds require the **.NET 10 Desktop Runtime**.
- Google Sheets is optional; its OAuth configuration is described below.

### Building from source

- Windows and the [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Optional IDE: **Visual Studio 2026 (18.0+)**, with a version compatible with your installed SDK, or VS Code with C# Dev Kit. See [Microsoft's .NET installation guidance](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

---

## Build & Run

```bash
# Clone the repository
git clone https://github.com/icy-001/Dungeons2.git
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

1. In your Google Cloud project, enable **Google Sheets API** and **Google Picker API**. Configure the Google Auth consent screen, audience, and test users (while the app is in Testing).
2. Create an OAuth client of type **Desktop app**, download its JSON configuration, and name it `credentials.json`. Place it in the project root for development (build/publish copies it) or next to `Dungeons2.exe` for an installed app. It must have an `installed` object, not `web` or `service_account`. The file is Git-ignored; do not commit real credentials or tokens.
3. Open **Settings → Google Sheets → Connect Google Sheets…**. Sign in in your system browser, choose one editable Google spreadsheet in Picker, and grant access. Use **Change spreadsheet…** to select a different destination later. No backend, API key, service account, or manually entered spreadsheet ID is needed.
4. Logging is enabled after connection. **Only floors with `Bonus == 1` are logged**, matching the original eligibility rule. Their completion times are written to one column of the **first visible worksheet**, not a dedicated tab. Set **Starting cell** (for example `A2` or `C5`) and click **Apply**. Times start there if empty, or append below existing data in that column. Floor times use `USER_ENTERED` so Sheets interprets them as durations just as before (including its locale and cell-format rules), rather than storing literal text. No headers, floor IDs, timestamps, or duplicate-tracking columns are written. Adjacent columns and other tabs are untouched; existing data from earlier versions is not deleted or converted.

5. Optionally expand **Optional write test** and click **Write test message to B2** to write `Hello from Dungeons2!` to the first visible worksheet, which must be an editable grid. The test refuses to overwrite existing values or formulas. Run it on a test spreadsheet without simultaneous editors. The service also exposes `WriteValuesAsync(range, values, cancellationToken)` for explicit arbitrary-range writes using literal (`RAW`) values.

Starting-cell edits are validated and only take effect when applied. Changes affect new floor completions, not saved uploads: each pending record retains its original spreadsheet and starting cell, including across restarts. The first visible worksheet is resolved when an upload is first prepared; its sheet ID and destination cell are then pinned for retries, so reordering or renaming tabs cannot redirect an in-flight write. Previously queued uploads from the dedicated-tab implementation keep their old destination. Changing the start does not move previously uploaded records. The optional B2 write test remains separate from the floor-data starting cell.

The only requested OAuth scope is `https://www.googleapis.com/auth/drive.file`. Authorization uses the documented desktop Picker flow (`prompt=consent`, `trigger_onepick=true`, spreadsheet MIME filter, `picked_file_ids` callback), PKCE S256, random state, and an ephemeral loopback callback. Selecting a different spreadsheet opens Picker again. Knowing a spreadsheet ID alone never enables access.

Tokens are encrypted with **Windows DPAPI CurrentUser** at `%LOCALAPPDATA%\Dungeons2\GoogleSheets\tokens.dpapi`. They cannot be transferred to a different Windows user/machine. Access tokens refresh automatically; expired/revoked authorization requires selecting a spreadsheet again. An OAuth app left in Google's Testing mode may require periodic consent; configure production publishing before distributing broadly.

Pending floor records are stored in `pending-floors.json` in the same folder and retried every 30 seconds while logging is enabled. Before writing, the exact destination cell is persisted locally. Retries reuse that cell and recognize an already-written matching time (including Sheets' numeric duration representation) after a lost response, rather than adding another row. Completed-record receipts are kept locally in `uploaded-floors.json`, not in Sheets. A local file lock prevents competing Dungeons2 instances from uploading the same queue. Do not delete these files, edit/move cells with pending uploads, or have concurrent writers use the same destination column: this is not a cross-machine transaction. A reserved cell containing a different value or formula stops the upload without overwriting it. Old five-column queue entries are checked against their historical record-ID column once to avoid replaying an upload that already succeeded; all new writes contain time only.

**Disconnect** stops logging, deletes local tokens, and attempts to revoke Google's grant. If offline, the UI instructs you to remove access in your Google account. Pending records remain locally; reconnect to their original spreadsheet to upload them. Cancelling sign-in leaves the previous connection intact. Errors and pending counts are shown in Settings; authorization codes, tokens, and API response bodies are not logged.

Official references: [desktop/mobile Picker](https://developers.google.com/workspace/drive/picker/guides/desktop-mobile-picker), [desktop OAuth and PKCE](https://developers.google.com/identity/protocols/oauth2/native-app), [Sheets value updates](https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets.values/update).

Run offline integration checks with `dotnet run --project Tests/GoogleSheets/GoogleSheets.Tests.csproj`. Tests use fake Google responses and a real local loopback socket/DPAPI, never a live Google account. A real browser sign-in and test-spreadsheet write remain a manual end-to-end check.

---

## Tech Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 10.0 | Runtime platform |
| WPF | (UseWPF) | Desktop UI framework |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM source generators |
| Vortice.Direct3D11 | 3.8.3 | DirectX 11 screen capture |
| Google Sheets REST API | v4 | OAuth-authorized spreadsheet integration |
| System.Text.Json | (built-in) | Data serialization |

---

## Output Files

| File | Description |
|------|-------------|
| `floors.jsonl` | Floor completion data (JSON Lines format) |
| `floors.txt` | Floor summary (tab-separated values) |
| `errors.log` | Application error log |
| `Maps/*.png` | Saved map screenshots (default location; configurable) |
| `Winterfaces/*.png` | Saved end-of-floor screenshots (default location; configurable) |

Floor logs and `errors.log` are relative to the application's working directory. Settings are stored in `%LOCALAPPDATA%\Dungeons2\settings.json`; Google Sheets tokens, pending uploads, and upload receipts are stored in the `GoogleSheets` subfolder as described above.

---

## License

MIT
