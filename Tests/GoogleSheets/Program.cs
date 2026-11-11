using Dungeons2;
using Dungeons2.Services;
using Dungeons2.ViewModels;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}

string testRoot = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);
// All test credentials and responses are synthetic; no Google account is contacted.
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "credentials.json"), """
    {"installed":{"client_id":"test-client","client_secret":"test-secret"}}
    """);
var tokens = new GoogleTokens("test-client", "test-access-token", "test-refresh-token",
    DateTimeOffset.UtcNow.AddHours(1), "test-sheet", "Test spreadsheet");
var store = new GoogleTokenStore(Path.Combine(testRoot, "tokens.dpapi"));
store.Save(tokens);
Check(store.Load() == tokens, "DPAPI round trip");
Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(testRoot, "tokens.dpapi"))).Contains(tokens.RefreshToken), "No plaintext token on disk");

string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
var url = new Uri(GooglePickerCallback.AuthorizationUrl("client", "http://127.0.0.1:1234/", "state", verifier));
var query = GooglePickerCallback.ParseQuery(url.Query);
Check(query["code_challenge"] == "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", "PKCE S256 known vector");
Check(query["scope"] == "https://www.googleapis.com/auth/drive.file", "Only drive.file requested");
Check(query["prompt"] == "consent" && query["trigger_onepick"] == "true", "Required Picker flags");
Check(query["mimetypes"] == "application/vnd.google-apps.spreadsheet" && query["include_granted_scopes"] == "false", "Picker scope and MIME isolation");
Check(GooglePickerCallback.ParseQuery("picked_file_ids=one%2Ctwo&code=a%2Bb")["code"] == "a+b", "Callback URL decoding");
try { GooglePickerCallback.ParseQuery("state=a&state=b"); throw new Exception("Duplicate state accepted"); }
catch (InvalidDataException) { assertions++; }

using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
using (var browser = new HttpClient())
{
    var opened = new TaskCompletionSource<string>();
    var callback = GooglePickerCallback.ReceiveAsync("client", verifier, deadline.Token, uri => opened.SetResult(uri));
    var auth = GooglePickerCallback.ParseQuery(new Uri(await opened.Task).Query);
    using var bad = await browser.GetAsync(auth["redirect_uri"] + "?state=wrong&code=forged", deadline.Token);
    Check(bad.StatusCode == HttpStatusCode.BadRequest && !callback.IsCompleted, "Reject mismatched OAuth state");
    using var good = await browser.GetAsync(auth["redirect_uri"] + "?state=" + auth["state"] + "&code=ok&picked_file_ids=test-sheet", deadline.Token);
    var result = await callback;
    Check(result.Parameters["picked_file_ids"] == "test-sheet", "Accept valid loopback callback");
    Check(good.StatusCode == HttpStatusCode.OK, "Browser receives generic success page");
}
using (var cancelled = new CancellationTokenSource())
{
    var receive = GooglePickerCallback.ReceiveAsync("client", verifier, cancelled.Token, _ => cancelled.Cancel());
    try { await receive; throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { assertions++; }
}

var fake = new FakeGoogle();
using var http = new HttpClient(fake);
var service = new GoogleSheetsService(http, testRoot);
service.Initialize();
var viewModel = new SettingsViewModel(service);
Check(viewModel.SheetsState == "Logging on" && viewModel.ConnectLabel == "Change spreadsheet…", "Connected UI actions");
Check(!viewModel.ShowQueue && !viewModel.ShowStatusDetail && !viewModel.CanRetrySheets, "Hide idle queue and redundant status");
Settings.Instance.LogToSheet = false;
Check(viewModel.SheetsState == "Logging paused" && viewModel.SheetsDescription.Contains("not queued"), "Paused UI explains new floors and retries");
Settings.Instance.LogToSheet = true;
Check(service.IsConnected && Settings.Instance.SpreadsheetId == "test-sheet", "Restored Picker-selected connection");
var writeGate = fake.HoldNextRequest = new TaskCompletionSource();
var writeTask = service.WriteDemoAsync();
Check(service.Activity == GoogleSheetsActivity.Testing && service.IsBusy && viewModel.SheetsState == "Testing write access…", "Write-test progress state");
Check(!viewModel.ShowConnect && !viewModel.ShowConnectedOptions && !viewModel.ConnectGoogleSheetsCommand.CanExecute(null), "Busy state prevents overlapping UI actions");
writeGate.SetResult();
await writeTask;
Check(!service.IsBusy && viewModel.ShowConnectedOptions, "Controls restored after write test");
Check(fake.LastWriteRange == "'Bob''s sheet'!B2" && fake.Writes == 1 && fake.LastValue == "Hello from Dungeons2!", "B2 on first worksheet and escaped title");
fake.B2 = "=IF(TRUE,\"\",\"\")";
await service.WriteDemoAsync();
Check(fake.Writes == 1 && service.Status.Contains("already contains"), "Do not overwrite formula returning empty string");
Check(viewModel.SheetsState == "Needs attention" && viewModel.ShowStatusDetail, "Error UI exposes actionable details");
await service.WriteValuesAsync("'Bob''s sheet'!C4:D4", new List<IList<object>> { new List<object> { "literal", 42 } });
Check(fake.Writes == 2, "Arbitrary-range asynchronous write");

var floor = new FloorData { Id = 7, FloorNumber = 42, Bonus = 1, Time = TimeSpan.FromSeconds(305), Timestamp = DateTimeOffset.Parse("2026-11-10T12:00:00Z") };
foreach (double bonus in new[] { 0.0, 0.5, 0.99, 1.01, 2.0 })
{
    await service.AppendFloorAsync(new FloorData { Id = 999, Bonus = bonus, Time = floor.Time, Timestamp = floor.Timestamp });
    Check(service.PendingCount == 0 && fake.Appends == 0, "Only exact Bonus == 1 floors are eligible: " + bonus);
}
using (var numericTime = JsonDocument.Parse((305.0 / 86400).ToString("R", System.Globalization.CultureInfo.InvariantCulture)))
    Check(GoogleSheetsService.MatchesFloorTime(numericTime.RootElement, "00:05:05"), "Numeric Sheets duration recognized on retry");
using (var formulaTime = JsonDocument.Parse("\"=TIME(0,5,5)\""))
    Check(!GoogleSheetsService.MatchesFloorTime(formulaTime.RootElement, "00:05:05"), "User formula remains protected even if it produces the same time");
fake.LoseNextAppendResponse = true;
var uploadGate = fake.HoldNextRequest = new TaskCompletionSource();
var uploadTask = service.AppendFloorAsync(floor);
Check(service.Activity == GoogleSheetsActivity.Uploading && viewModel.SheetsState == "Uploading…", "Automatic upload has a visible busy state");
uploadGate.SetResult();
await uploadTask;
Check(fake.Records.Count == 1 && service.PendingCount == 1, "Lost append response retains durable queue");
Check(File.ReadAllText(Path.Combine(testRoot, "pending-floors.json")).Contains("RecordId"), "Pending record persisted");
service.Stop();
service = new GoogleSheetsService(http, testRoot);
service.Initialize();
viewModel = new SettingsViewModel(service);
Check(viewModel.ShowQueue && viewModel.CanRetrySheets && service.SelectedPendingCount == 1, "Retry only selected-sheet uploads");
Settings.Instance.LogToSheet = false;
Check(!viewModel.RetrySheetsCommand.CanExecute(null) && viewModel.QueueSummary.Contains("Turn logging on"), "Paused retry explains why disabled");
Settings.Instance.LogToSheet = true;
await service.RetryPendingAsync();
Check(fake.Appends == 1 && service.PendingCount == 0, "Restart retry detects committed row without duplicate");
await service.AppendFloorAsync(floor);
Check(fake.Appends == 1, "Duplicate floor event ignored using local receipt");
fake.Status = HttpStatusCode.NotFound;
floor.Id++;
await service.AppendFloorAsync(floor);
Check(fake.Appends == 1 && service.PendingCount == 1, "Failed upload remains queued");
await service.RetryPendingAsync();
Check(service.PendingCount == 1 && service.Status.Contains("missing"), "Missing spreadsheet retains queue");
fake.Status = HttpStatusCode.Forbidden;
await service.RetryPendingAsync();
Check(service.PendingCount == 1 && service.Status.Contains("denied"), "Access denied retains queue");
fake.Status = HttpStatusCode.OK;
await service.RetryPendingAsync();
Check(fake.Appends == 2 && service.PendingCount == 0, "Retry resumes queued floor");
Check(fake.Records.All(row => row.GetArrayLength() == 1) && fake.Records[0][0].GetString() == "00:05:05", "Only duration is written; no identifiers, timestamp, or extra columns");
fake.UnauthorizedOnce = true;
await service.WriteValuesAsync("C1", new List<IList<object>> { new List<object> { "test" } });
Check(fake.Refreshes == 1, "401 triggers one token refresh and retry");
fake.RevokeOffline = true;
await service.DisconnectAsync();
Check(!service.IsConnected && !File.Exists(Path.Combine(testRoot, "tokens.dpapi")), "Offline disconnect clears local tokens");
Check(service.Status.Contains("Disconnected locally"), "Offline revocation status is explicit");
Check(viewModel.ConnectLabel == "Connect Google Sheets…" && !viewModel.ShowConnectedOptions && !viewModel.CanUseSheet, "Disconnected hides write and logging controls");
Check(viewModel.ShowStatusDetail, "Offline disconnect retains revocation guidance");
service.Stop();

store.Save(tokens with { ExpiresAt = DateTimeOffset.MinValue });
var authService = new GoogleOAuthService(http, store);
authService.Load();
await authService.AccessTokenAsync(default);
Check(store.Load()!.RefreshToken == "test-refresh-token", "Refresh without new refresh_token preserves original");
fake.Revoked = true;
try { await authService.AccessTokenAsync(default, true); throw new Exception("Revoked token accepted"); }
catch (InvalidOperationException e) { Check(e.Message.Contains("revoked"), "Revoked-token guidance"); }
fake.Revoked = false;
fake.BroadScope = true;
try { await authService.AccessTokenAsync(default, true); throw new Exception("Broad token accepted"); }
catch (InvalidOperationException e) { Check(e.Message.Contains("unexpected permissions"), "Reject broader token permissions"); }
store.Delete();
// Saved uploads for a different spreadsheet must not expose a nonfunctional retry button.
store.Save(tokens);
File.WriteAllText(Path.Combine(testRoot, "pending-floors.json"), JsonSerializer.Serialize(new[]
    { GoogleSheetsService.PendingFloor.From("different-sheet", floor) }));
service = new GoogleSheetsService(http, testRoot);
service.Initialize();
viewModel = new SettingsViewModel(service);
Check(viewModel.ShowQueue && !viewModel.ShowRetry && !viewModel.CanRetrySheets && viewModel.QueueSummary.Contains("another spreadsheet"), "Other-spreadsheet queue guidance");
var blockedService = new GoogleSheetsService(http, testRoot);
blockedService.Initialize();
var blockedViewModel = new SettingsViewModel(blockedService);
Check(blockedViewModel.SheetsState == "Setup needs attention" && blockedViewModel.ShowStatusDetail && !blockedViewModel.CanManageSheets, "Unavailable storage disables impossible actions");
blockedService.Stop();
service.Stop();
store.Delete();
Check(GoogleSheetsStartCell.Parse(" $c$5 ").Address == "C5", "Normalize starting cell");
Check(GoogleSheetsStartCell.Parse("Z1500").DataRange == "Z1500:Z" && GoogleSheetsStartCell.Parse("ZZZ1").DataRange == "ZZZ1:ZZZ", "Exactly one column including the final column");
foreach (string invalid in new[] { "", "A0", "A1:B2", "Sheet1!A2", "AAAA1", "A99999999", "1A", "A-1" })
    Check(!GoogleSheetsStartCell.TryParse(invalid, out _), "Reject invalid cell: " + invalid);
Check(GoogleSheetsStartCell.Parse("A1").Row == 1, "First-row data is supported");
Check(JsonSerializer.Deserialize<GoogleSheetsService.PendingFloor>("""{"SpreadsheetId":"old-sheet","FloorId":1,"FloorNumber":1,"Time":"00:05:00","Timestamp":"2026-11-10","RecordId":"legacy"}""")!.StartCell == "A2", "Legacy queued records retain original A2 destination");
store.Save(tokens);
service = new GoogleSheetsService(http, testRoot);
service.Initialize();
viewModel = new SettingsViewModel(service);
viewModel.StartCellInput = "C0";
Check(!viewModel.CanApplyStartCell && !viewModel.IsStartCellValid, "Invalid UI input cannot be applied");
viewModel.StartCellInput = "$c$5";
Check(Settings.Instance.SpreadsheetStartCell == "A2" && viewModel.CanApplyStartCell, "Draft does not change live destination");
viewModel.ApplySheetsStartCellCommand.Execute(null);
Check(Settings.Instance.SpreadsheetStartCell == "C5" && viewModel.DestinationSummary.Contains("C5"), "Apply saves normalized destination");
Settings.Instance.LogToSheet = true;
floor.Id++;
fake.LoseNextAppendResponse = true;
await service.AppendFloorAsync(floor);
Check(fake.LastAppendRange == "'Bob''s sheet'!C5" && service.PendingCount > 0, "Custom single-cell write and durable queue on first visible sheet");
viewModel.StartCellInput = "Z1500";
viewModel.ApplySheetsStartCellCommand.Execute(null);
int appendCount = fake.Appends;
service.Stop();
service = new GoogleSheetsService(http, testRoot);
service.Initialize();
await service.RetryPendingAsync();
Check(fake.Appends == appendCount && service.SelectedPendingCount == 0, "Retry after restart uses original cell and avoids duplicates after destination change");
floor.Id++;
await service.AppendFloorAsync(floor);
Check(fake.LastAppendRange == "'Bob''s sheet'!Z1500", "New floors use changed starting cell");
Check(fake.GridRows >= 1500 && fake.GridColumns == 26, "Expand rows without adding unnecessary columns");
fake.Cells["Z1501"] = "=IF(TRUE,\"\",\"\")";
fake.Cells["AA1502"] = "neighbor data";
floor.Id++;
await service.AppendFloorAsync(floor);
Check(fake.LastAppendRange == "'Bob''s sheet'!Z1502" && fake.Cells["Z1501"].StartsWith('=') && fake.Cells["AA1502"] == "neighbor data", "Append below existing formulas without touching adjacent column");
Check(!fake.Cells.ContainsKey("A1"), "No header cells are added");
fake.Cells["Z1504"] = "=AVERAGE(Z1500:Z1503)";
fake.Cells["Z1505"] = "Summary";
floor.Id++;
await service.AppendFloorAsync(floor);
Check(fake.LastAppendRange == "'Bob''s sheet'!Z1503", "Fill empty floor cell above populated summary rows");
floor.Id++;
await service.AppendFloorAsync(floor);
Check(fake.LastAppendRange == "'Bob''s sheet'!Z1506" && fake.Cells["Z1504"].StartsWith('=') && fake.Cells["Z1505"] == "Summary", $"Continue below summary: {fake.LastAppendRange}; {service.Status}");
service.Stop();
service = new GoogleSheetsService(http, testRoot);
service.Initialize();
int writesBeforeReplay = fake.Appends;
await service.AppendFloorAsync(floor);
Check(fake.Appends == writesBeforeReplay, "Local receipt survives restart and prevents completed-event replay");
service.Stop();
var legacyRecord = GoogleSheetsService.PendingFloor.From("test-sheet", floor) with { RecordId = "legacy-five-column-key", StartCell = "A2", FormatVersion = 0 };
fake.Cells["D2"] = legacyRecord.RecordId;
File.WriteAllText(Path.Combine(testRoot, "pending-floors.json"), JsonSerializer.Serialize(new[] { legacyRecord }));
service = new GoogleSheetsService(http, testRoot);
service.Initialize();
await service.RetryPendingAsync();
Check(fake.Appends == writesBeforeReplay && service.PendingCount == 0 && fake.Cells["D2"] == legacyRecord.RecordId, "Legacy lost-response queue recognized without rewriting historical multi-column data");
service.Stop();
store.Delete();
Console.WriteLine($"PASS: {assertions} Google Sheets assertions; no Google network requests made.");

sealed class FakeGoogle : HttpMessageHandler
{
    public List<JsonElement> Records { get; } = [];
    public int Writes, Appends, Refreshes;
    public string? B2, LastWriteRange, LastValue, LastAppendRange;
    public Dictionary<string, string> Cells { get; } = [];
    public int GridRows = 1000, GridColumns = 26;
    public bool LoseNextAppendResponse, UnauthorizedOnce, RevokeOffline, Revoked, BroadScope;
    public HttpStatusCode Status = HttpStatusCode.OK;
    public TaskCompletionSource? HoldNextRequest;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (HoldNextRequest is { } hold) { HoldNextRequest = null; await hold.Task.WaitAsync(ct); }
        string path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
        if (path == "/revoke")
        {
            if (RevokeOffline) throw new HttpRequestException("offline");
            return Json(new { });
        }
        if (path == "/token")
        {
            Refreshes++;
            if (Revoked) return new(HttpStatusCode.BadRequest);
            return Json(new { access_token = "refreshed", expires_in = 3600, scope = BroadScope ? "unexpected-scope" : GoogleOAuthService.Scope });
        }
        if (Status != HttpStatusCode.OK) return new(Status);
        if (UnauthorizedOnce) { UnauthorizedOnce = false; return new(HttpStatusCode.Unauthorized); }
        if (request.Headers.Authorization?.Scheme != "Bearer") throw new Exception("Missing bearer token");
        if (path.EndsWith(":batchUpdate"))
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            foreach (var update in body.RootElement.GetProperty("requests").EnumerateArray())
            {
                if (update.TryGetProperty("addSheet", out _)) throw new Exception("Floor logging must not create a dedicated worksheet");
                if (update.TryGetProperty("updateSheetProperties", out var resize))
                {
                    var grid = resize.GetProperty("properties").GetProperty("gridProperties");
                    GridRows = grid.GetProperty("rowCount").GetInt32();
                    GridColumns = grid.GetProperty("columnCount").GetInt32();
                }
            }
            return Json(new { });
        }
        int valuesStart = path.IndexOf("/values/", StringComparison.Ordinal);
        if (valuesStart < 0)
        {
            var sheets = new List<object> { new { properties = new { title = "Second", sheetId = 2, index = 2, sheetType = "GRID" } },
                new { properties = new { title = "Hidden first tab", sheetId = 3, index = 0, hidden = true, sheetType = "GRID" } },
                new { properties = new { title = "Bob's sheet", sheetId = 1, index = 1, sheetType = "GRID",
                    gridProperties = new { rowCount = GridRows, columnCount = GridColumns } } } };
            sheets.Add(new { properties = new { title = GoogleSheetsService.LogSheet, sheetId = 123, index = 3, sheetType = "GRID",
                gridProperties = new { rowCount = GridRows, columnCount = GridColumns } } });
            return Json(new { properties = new { title = "Test spreadsheet" }, sheets });
        }
        string range = path[(valuesStart + 8)..];
        if (request.Method == HttpMethod.Put)
        {
            Writes++;
            LastWriteRange = range;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            LastValue = body.RootElement.GetProperty("values")[0][0].ToString();
            if (request.RequestUri.Query.Contains("valueInputOption=USER_ENTERED"))
            {
                var row = body.RootElement.GetProperty("values")[0];
                if (row.GetArrayLength() != 1 || body.RootElement.GetProperty("values").GetArrayLength() != 1 || range.Contains(':'))
                    throw new Exception("Floor uploads must write exactly one cell");
                Appends++;
                LastAppendRange = range;
                Records.Add(row.Clone());
                Cells[range.Split('!')[1]] = LastValue;
                if (LoseNextAppendResponse) { LoseNextAppendResponse = false; throw new HttpRequestException("response lost"); }
            }
            return Json(new { });
        }
        if (request.Method != HttpMethod.Get) throw new Exception("Unexpected write API");
        if (range.EndsWith("!B2")) return Json(new { values = B2 is null ? Array.Empty<string[]>() : new[] { new[] { B2 } } });
        if (range.StartsWith("'Bob''s sheet'!") || range.StartsWith("'Dungeons2 Floors'!"))
        {
            string address = range.Split('!')[1];
            if (!address.Contains(':')) return Json(new { values = Cells.TryGetValue(address, out var value) ? new[] { new[] { ReadCell(value) } } : Array.Empty<object[]>() });
            var start = GoogleSheetsStartCell.Parse(address.Split(':')[0]);
            int last = Cells.Keys.Select(GoogleSheetsStartCell.Parse).Where(c => c.Column == start.Column && c.Row >= start.Row).Select(c => c.Row).DefaultIfEmpty(start.Row - 1).Max();
            return Json(new { values = Enumerable.Range(start.Row, last - start.Row + 1).Select(r => Cells.TryGetValue(new GoogleSheetsStartCell(start.Column, r).Address, out var value) ? new[] { value } : Array.Empty<string>()).ToArray() });
        }
        throw new Exception("Unexpected fake API request");
    }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    private static object ReadCell(string value) => TimeSpan.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var duration) ? duration.TotalDays : value;
}
