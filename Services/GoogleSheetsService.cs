using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dungeons2.Services;

public enum GoogleSheetsActivity { Idle, Connecting, Disconnecting, Testing, Uploading }

public partial class GoogleSheetsService : ObservableObject
{
    public static GoogleSheetsService Instance { get; } = new();
    internal const string LogSheet = "Dungeons2 Floors";
    private readonly HttpClient http;
    private readonly GoogleOAuthService oauth;
    private readonly string queuePath;
    private readonly string uploadedPath;
    private HashSet<string> uploaded = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object queueGate = new();
    private readonly CancellationTokenSource lifetime = new();
    private List<PendingFloor> pending = [];
    private FileStream? instanceLock;
    private bool initialized;

    [ObservableProperty] public partial string Status { get; private set; } = "Not connected.";
    [ObservableProperty] public partial bool IsConnected { get; private set; }
    [ObservableProperty] public partial bool IsBusy { get; private set; }
    [ObservableProperty] public partial bool IsAvailable { get; private set; }
    [ObservableProperty] public partial bool HasError { get; private set; }
    [ObservableProperty] public partial bool HasNotice { get; private set; }
    [ObservableProperty] public partial GoogleSheetsActivity Activity { get; private set; }
    [ObservableProperty] public partial string SpreadsheetName { get; private set; } = "No spreadsheet selected";
    [ObservableProperty] public partial int PendingCount { get; private set; }
    [ObservableProperty] public partial int SelectedPendingCount { get; private set; }

    private GoogleSheetsService() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dungeons2", "GoogleSheets")) { }

    internal GoogleSheetsService(HttpClient client, string directory)
    {
        http = client;
        oauth = new(client, new GoogleTokenStore(Path.Combine(directory, "tokens.dpapi")));
        queuePath = Path.Combine(directory, "pending-floors.json");
        uploadedPath = Path.Combine(directory, "uploaded-floors.json");
    }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(queuePath)!);
            // Only one application instance may own the queue and deduplication process.
            instanceLock = new FileStream(queuePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            oauth.Load();
            if (File.Exists(uploadedPath))
                uploaded = JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(uploadedPath)) ?? throw new JsonException();
            if (File.Exists(queuePath))
            {
                pending = JsonSerializer.Deserialize<List<PendingFloor>>(File.ReadAllText(queuePath)) ?? throw new JsonException();
                if (pending.Any(p => p is null || !GoogleOAuthService.IsFileId(p.SpreadsheetId ?? "") ||
                    string.IsNullOrEmpty(p.RecordId) || p.Time is null || p.Timestamp is null)) throw new JsonException();
            }
            PendingCount = pending.Count;
            UpdateSelection();
            IsAvailable = true;
            Status = IsConnected ? "Connected. Completed floors will be queued when logging is enabled." : "Select a spreadsheet to connect.";
            MapCaptureService.Instance.OnFloorCompleted += OnFloorCompleted;
            _ = RetryLoopAsync();
        }
        catch (Exception e)
        {
            // Preserve unreadable files for recovery, rather than replacing a damaged queue.
            instanceLock?.Dispose();
            instanceLock = null;
            HasError = true;
            Status = e is JsonException ? "Google Sheets local data is damaged. Back up and repair the GoogleSheets folder before restarting."
                : e is CryptographicException ? "Saved Google tokens cannot be decrypted. Remove tokens.dpapi and restart to reconnect."
                : "Google Sheets local storage is unavailable or another Dungeons2 instance owns it. Close the other instance and restart.";
        }
    }

    private void UpdateSelection()
    {
        IsConnected = oauth.Tokens is not null;
        SpreadsheetName = oauth.Tokens?.SpreadsheetName ?? "No spreadsheet selected";
        // A manually entered legacy ID is not proof of a Picker grant.
        Settings.Instance.SpreadsheetId = oauth.Tokens?.SpreadsheetId ?? "";
        Settings.Instance.SpreadsheetName = oauth.Tokens?.SpreadsheetName ?? "";
        Settings.Instance.Save();
        UpdatePendingCounts();
    }

    public Task ConnectAsync(CancellationToken ct) => RunUiOperationAsync(async token =>
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        Status = "Complete sign-in and select a spreadsheet in your browser. Cancel here if you close the browser.";
        var candidate = await oauth.PickAsync(deadline.Token);
        using var metadata = await RequestAsync(HttpMethod.Get, candidate.SpreadsheetId,
            "?fields=properties(title),sheets(properties(title,index,hidden,sheetType))", null, token, candidate.AccessToken);
        string name = metadata.RootElement.GetProperty("properties").GetProperty("title").GetString()!;
        FirstWorksheet(metadata.RootElement);
        oauth.Commit(candidate with { SpreadsheetName = name });
        UpdateSelection();
        Settings.Instance.LogToSheet = true;
        Settings.Instance.Save();
        Status = $"Connected to {name}. Eligible floor times will be logged to the first visible worksheet.";
    }, ct, GoogleSheetsActivity.Connecting);

    public Task DisconnectAsync() => RunUiOperationAsync(async ct =>
    {
        bool revoked = await oauth.DisconnectAsync(ct);
        Settings.Instance.LogToSheet = false;
        UpdateSelection();
        Status = revoked ? "Disconnected. Your spreadsheet data has not been changed."
            : "Disconnected locally. Google could not be reached; remove this app's access in your Google account to revoke the grant.";
    }, CancellationToken.None, GoogleSheetsActivity.Disconnecting);

    // Explicit demo, never an automatic side effect of selecting a real spreadsheet.
    public Task WriteDemoAsync() => RunUiOperationAsync(async ct =>
    {
        string id = SelectedId();
        using var metadata = await RequestAsync(HttpMethod.Get, id, "?fields=sheets(properties(title,index,hidden,sheetType))", null, ct);
        string range = QuoteSheet(FirstWorksheet(metadata.RootElement)) + "!B2";
        using var existing = await GetValuesAsync(id, range, ct, formulas: true);
        if (Rows(existing).Any(row => row.EnumerateArray().Any(cell => cell.ToString().Length > 0)))
            throw new InvalidOperationException("B2 already contains data or a formula; nothing was overwritten. Use a blank spreadsheet to test.");
        await WriteValuesCoreAsync(id, range, [["Hello from Dungeons2!"]], ct);
        Status = "Wrote Hello from Dungeons2! to B2 of the first worksheet.";
    }, CancellationToken.None, GoogleSheetsActivity.Testing);

    /// <summary>Write literal values to an A1 range in the Picker-selected spreadsheet.
    /// This explicit API replaces specified cells; automatic floor logging only appends.</summary>
    public async Task WriteValuesAsync(string range, IList<IList<object>> values, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(range);
        ArgumentNullException.ThrowIfNull(values);
        await gate.WaitAsync(ct);
        try { EnsureAvailable(); await WriteValuesCoreAsync(SelectedId(), range, values, ct); }
        finally { gate.Release(); }
    }

    private async Task WriteValuesCoreAsync(string id, string range, IList<IList<object>> values, CancellationToken ct, bool userEntered = false)
    {
        using var result = await RequestAsync(HttpMethod.Put, id,
            $"/values/{Uri.EscapeDataString(range)}?valueInputOption={(userEntered ? "USER_ENTERED" : "RAW")}", new { values }, ct);
    }

    private async void OnFloorCompleted(FloorData floor)
    {
        await AppendFloorAsync(floor);
    }

    public async Task AppendFloorAsync(FloorData floor)
    {
        if (!Settings.Instance.LogToSheet || floor.Bonus != 1 || oauth.Tokens is not { } selected) return;
        try
        {
            var record = PendingFloor.From(selected.SpreadsheetId, floor);
            // Do not delay persistence behind a browser sign-in or a slow network call.
            lock (queueGate)
            {
                if (!pending.Any(p => p.SpreadsheetId == record.SpreadsheetId && p.RecordId == record.RecordId))
                {
                    if (uploaded.Contains(UploadKey(record))) return;
                    pending.Add(record);
                    SaveQueue(); // Persist before touching the network.
                }
            }
            await RetryPendingAsync();
        }
        catch (Exception e) { ReportError(e); } // Never crash the capture loop.
    }

    public Task RetryPendingAsync() => !Settings.Instance.LogToSheet || !IsConnected || SelectedPendingCount == 0
        ? Task.CompletedTask
        : RunUiOperationAsync(FlushAsync, lifetime.Token, GoogleSheetsActivity.Uploading);

    private async Task RetryLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(lifetime.Token))
                if (Settings.Instance.LogToSheet && IsConnected && SelectedPendingCount > 0 && !IsBusy)
                    await RetryPendingAsync();
        }
        catch (OperationCanceledException) { }
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        if (!Settings.Instance.LogToSheet || oauth.Tokens is null) return;
        string id = SelectedId();
        PendingFloor[] records;
        lock (queueGate) records = pending.Where(p => p.SpreadsheetId == id).ToArray();
        if (records.Length == 0) return;
        foreach (var original in records)
        {
            var record = original;
            if (!uploaded.Contains(UploadKey(record)))
            {
                var start = GoogleSheetsStartCell.Parse(record.StartCell);
                var sheet = await ResolveFloorSheetAsync(id, record, ct);
                string sheetTitle = sheet.GetProperty("title").GetString()!;
                int sheetId = sheet.GetProperty("sheetId").GetInt32();
                await EnsureGridSizeAsync(id, sheet, start, ct);
                bool alreadyWritten = false;
                if (record.FormatVersion == 0 && start.Column + 3 <= 18278)
                {
                    // Old five-column queues may have committed before their response was lost.
                    // Read their old key column once, but never write any new metadata to Sheets.
                    string column = GoogleSheetsStartCell.ColumnName(start.Column + 3);
                    using var legacy = await GetValuesAsync(id, QuoteSheet(sheetTitle) + "!" + column + start.Row + ":" + column, ct);
                    alreadyWritten = Rows(legacy).Any(row => row.GetArrayLength() > 0 && row[0].ToString() == record.RecordId);
                }
                if (!alreadyWritten)
                {
                    if (record.TargetCell is null)
                    {
                        using var existing = await GetValuesAsync(id, QuoteSheet(sheetTitle) + "!" + start.DataRange, ct, formulas: true);
                        // Fill gaps before summary rows, but never replace a formula,
                        // including formulas whose displayed result is an empty string.
                        int occupiedRows = Rows(existing).TakeWhile(row =>
                            row.GetArrayLength() > 0 && row[0].ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
                            && row[0].ToString().Length > 0).Count();
                        int nextRow = checked(start.Row + occupiedRows);
                        record = record with { TargetCell = new GoogleSheetsStartCell(start.Column, nextRow).Address, TargetSheetId = sheetId, FormatVersion = 2 };
                        lock (queueGate)
                        {
                            pending[pending.IndexOf(original)] = record;
                            SaveQueue(); // Pin the exact cell before sending a write, including across restarts.
                        }
                    }
                    var target = GoogleSheetsStartCell.Parse(record.TargetCell);
                    await EnsureGridSizeAsync(id, sheet, target, ct);
                    string range = QuoteSheet(sheetTitle) + "!" + target.Address;
                    using var cell = await GetValuesAsync(id, range, ct, formulas: true);
                    var existingValue = Rows(cell).SelectMany(row => row.EnumerateArray()).FirstOrDefault();
                    bool empty = existingValue.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || existingValue.ToString().Length == 0;
                    if (!empty && !MatchesFloorTime(existingValue, record.Time))
                        throw new InvalidOperationException($"The reserved upload cell {target.Address} now contains different data. Nothing was overwritten. Restore that cell to blank or its expected floor time before retrying.");
                    if (empty)
                        await WriteValuesCoreAsync(id, range, [[record.Time]], ct, userEntered: true);
                    // A matching value also confirms a write whose response was lost.
                }
                uploaded.Add(UploadKey(record));
            }
            SaveUploaded(); // Commit the receipt even if a previous attempt failed to save it.
            lock (queueGate)
            {
                pending.Remove(record);
                SaveQueue();
            }
        }
        Status = $"Floor records saved to {SpreadsheetName}.";
    }

    private async Task<JsonElement> ResolveFloorSheetAsync(string id, PendingFloor record, CancellationToken ct)
    {
        using var metadata = await RequestAsync(HttpMethod.Get, id,
            "?fields=sheets(properties(sheetId,title,index,hidden,sheetType,gridProperties))", null, ct);
        var sheets = metadata.RootElement.GetProperty("sheets").EnumerateArray().Select(s => s.GetProperty("properties"));
        JsonElement sheet;
        if (record.TargetSheetId is int targetId)
            sheet = sheets.FirstOrDefault(p => p.GetProperty("sheetId").GetInt32() == targetId);
        else if (record.FormatVersion < 2)
            sheet = sheets.FirstOrDefault(p => p.GetProperty("title").GetString() == LogSheet);
        else
            sheet = FirstVisibleWorksheet(metadata.RootElement);
        if (sheet.ValueKind == JsonValueKind.Undefined)
            throw new InvalidOperationException("The worksheet for this saved upload is missing. Restore it before retrying; no other tab was changed.");
        return sheet.Clone();
    }

    private async Task EnsureGridSizeAsync(string id, JsonElement properties, GoogleSheetsStartCell start, CancellationToken ct)
    {
        var grid = properties.GetProperty("gridProperties");
        int rows = grid.GetProperty("rowCount").GetInt32();
        int columns = grid.GetProperty("columnCount").GetInt32();
        if (rows < start.Row || columns < start.Column)
        {
            using var expanded = await RequestAsync(HttpMethod.Post, id, ":batchUpdate", new
            {
                requests = new[] { new { updateSheetProperties = new
                {
                    properties = new { sheetId = properties.GetProperty("sheetId").GetInt32(),
                        gridProperties = new { rowCount = Math.Max(rows, start.Row), columnCount = Math.Max(columns, start.Column) } },
                    fields = "gridProperties.rowCount,gridProperties.columnCount"
                } } }
            }, ct);
        }
    }

    internal static bool MatchesFloorTime(JsonElement value, string time)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double serial) &&
            TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out var duration))
            return Math.Abs(serial - duration.TotalDays) < 1e-9;
        return value.ValueKind == JsonValueKind.String && value.GetString() == time;
    }

    private Task<JsonDocument> GetValuesAsync(string id, string range, CancellationToken ct, bool formulas = false) =>
        RequestAsync(HttpMethod.Get, id, "/values/" + Uri.EscapeDataString(range) + (formulas ? "?valueRenderOption=FORMULA&dateTimeRenderOption=SERIAL_NUMBER" : ""), null, ct);

    internal static IEnumerable<JsonElement> Rows(JsonDocument document) =>
        document.RootElement.TryGetProperty("values", out var values) ? values.EnumerateArray().ToArray() : [];

    internal static string QuoteSheet(string title) => "'" + title.Replace("'", "''") + "'";

    internal static string FirstWorksheet(JsonElement metadata) => FirstVisibleWorksheet(metadata).GetProperty("title").GetString()!;

    private static JsonElement FirstVisibleWorksheet(JsonElement metadata)
    {
        var first = metadata.GetProperty("sheets").EnumerateArray().Select(s => s.GetProperty("properties"))
            .Where(p => !p.TryGetProperty("hidden", out var hidden) || !hidden.GetBoolean())
            .OrderBy(p => p.GetProperty("index").GetInt32()).FirstOrDefault();
        if (first.ValueKind == JsonValueKind.Undefined || first.TryGetProperty("sheetType", out var type) && type.GetString() != "GRID")
            throw new InvalidOperationException("The first visible worksheet must be an editable grid.");
        return first;
    }

    private async Task<JsonDocument> RequestAsync(HttpMethod method, string id, string suffix, object? body,
        CancellationToken ct, string? candidateToken = null)
    {
        for (int attempt = 0; ; attempt++)
        {
            string token = candidateToken ?? await oauth.AccessTokenAsync(ct, forceRefresh: attempt > 0);
            using var request = new HttpRequestMessage(method, "https://sheets.googleapis.com/v4/spreadsheets/" + Uri.EscapeDataString(id) + suffix);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0 && candidateToken is null) continue;
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Google authorization is no longer valid. Select the spreadsheet again.",
                    HttpStatusCode.Forbidden => "Google denied access. Check that Sheets and Picker APIs are enabled, you can edit this spreadsheet, and select it again through Google Picker.",
                    HttpStatusCode.NotFound => "The spreadsheet is missing or no longer accessible. Select it again or choose another spreadsheet.",
                    HttpStatusCode.TooManyRequests => "Google's rate limit was reached. Queued floors will retry later.",
                    _ => $"Google Sheets request failed (HTTP {(int)response.StatusCode}). Queued floors are retained; try again later."
                });
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        }
    }

    private string SelectedId() => oauth.Tokens?.SpreadsheetId ?? throw new InvalidOperationException("Select a spreadsheet first.");

    private void EnsureAvailable()
    {
        if (instanceLock is null) throw new InvalidOperationException("Google Sheets local storage is unavailable. Check the status and restart the application.");
    }

    private void SaveQueue()
    {
        UpdatePendingCounts();
        File.WriteAllText(queuePath + ".tmp", JsonSerializer.Serialize(pending));
        File.Move(queuePath + ".tmp", queuePath, overwrite: true);
    }

    private static string UploadKey(PendingFloor floor) => floor.SpreadsheetId + ":" + floor.RecordId;

    private void SaveUploaded()
    {
        File.WriteAllText(uploadedPath + ".tmp", JsonSerializer.Serialize(uploaded));
        File.Move(uploadedPath + ".tmp", uploadedPath, overwrite: true);
    }

    private void UpdatePendingCounts()
    {
        lock (queueGate)
        {
            PendingCount = pending.Count;
            SelectedPendingCount = pending.Count(p => p.SpreadsheetId == oauth.Tokens?.SpreadsheetId);
        }
    }

    private async Task RunUiOperationAsync(Func<CancellationToken, Task> operation, CancellationToken ct, GoogleSheetsActivity activity)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
        ct = linked.Token;
        bool acquired = false;
        try
        {
            await gate.WaitAsync(ct);
            acquired = true;
            EnsureAvailable();
            IsBusy = true;
            Activity = activity;
            HasError = false;
            HasNotice = false;
            Status = activity switch
            {
                GoogleSheetsActivity.Disconnecting => "Disconnecting and removing saved access…",
                GoogleSheetsActivity.Testing => "Checking B2 before writing the test message…",
                GoogleSheetsActivity.Uploading => "Uploading saved floor records…",
                _ => "Opening Google sign-in…"
            };
            await operation(ct);
            HasNotice = activity is GoogleSheetsActivity.Testing or GoogleSheetsActivity.Disconnecting;
        }
        catch (Exception e) { ReportError(e); }
        finally
        {
            if (acquired) { Activity = GoogleSheetsActivity.Idle; IsBusy = false; gate.Release(); }
        }
    }

    private void ReportError(Exception error)
    {
        HasError = error is not OperationCanceledException;
        HasNotice = !HasError;
        // Never log raw OAuth/API exceptions, response bodies, or request URIs.
        string message = error switch
        {
            OperationCanceledException => "Cancelled or timed out. Your spreadsheet selection is unchanged and pending records are retained.",
            HttpRequestException => "Unable to reach Google. Check your network; pending floor records will retry later.",
            InvalidOperationException => error.Message,
            _ when FileSafety.IsFileError(error) => "Unable to save Google Sheets local data. Check disk space and permissions.",
            _ => "Google Sheets operation failed. Check credentials.json and reconnect; pending floor records are retained."
        };
        if (Status != message) LogService.Instance.Log("Google Sheets: " + message);
        Status = message;
    }

    public void Stop()
    {
        lifetime.Cancel();
        MapCaptureService.Instance.OnFloorCompleted -= OnFloorCompleted;
        instanceLock?.Dispose();
    }

    internal sealed record PendingFloor(string SpreadsheetId, int FloorId, int FloorNumber, string Time, string Timestamp, string RecordId, string StartCell = "A2", string? TargetCell = null, int FormatVersion = 0, int? TargetSheetId = null)
    {
        public static PendingFloor From(string spreadsheetId, FloorData floor)
        {
            string timestamp = floor.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{floor.Id}|{timestamp}|{floor.FloorNumber}|{floor.Time.Ticks}")));
            return new(spreadsheetId, floor.Id, floor.FloorNumber, floor.Time.ToString("c", CultureInfo.InvariantCulture), timestamp, key,
                GoogleSheetsStartCell.Parse(Settings.Instance.SpreadsheetStartCell).Address, FormatVersion: 2);
        }
    }
}
