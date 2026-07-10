using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System.IO;

namespace Dungeons2.Services
{
    public class GoogleSheetsService
    {
        public static GoogleSheetsService Instance { get; } = new();

        public bool Enabled { get; private set; }

        private SheetsService? sheetsService;

        public void Initialize()
        {
            if (!File.Exists("google-credentials.json"))
            {
                Settings.Instance.LogToSheet = false;
                return;
            }

            string[] Scopes = [SheetsService.Scope.Spreadsheets];
            string serviceAccountKeyPath = "google-credentials.json";

            var credential = CredentialFactory.FromFile<ServiceAccountCredential>(serviceAccountKeyPath)
                .ToGoogleCredential()
                .CreateScoped(Scopes);

            sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Dungeons Sheets Integration"
            });

            MapCaptureService.Instance.OnFloorCompleted += async (floor) =>
            {
                if (Settings.Instance.LogToSheet && floor.Bonus == 1)
                    await Append(floor);
            };
            Enabled = true;
        }

        public async Task Append(FloorData floor)
        {
            if (sheetsService is null)
                return;

            string spreadsheetId = Settings.Instance.SpreadsheetId;
            string column = Settings.Instance.SpreadsheetColumn;
            int row = Settings.Instance.SpreadsheetStartRow;
            // 1. Read ONLY Column B to find how many rows are occupied
            string readRange = $"{column}{row}:{column}";
            var getRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, readRange);
            var getResponse = await getRequest.ExecuteAsync();

            // Calculate the next empty row in Column B
            int nextEmptyRow = (getResponse.Values?.Count ?? 0) + row;

            // 2. Use UPDATE to write exactly to that specific cell
            string exactUpdateRange = $"{column}{nextEmptyRow}";

            var updateRequest = sheetsService.Spreadsheets.Values.Update(
                new ValueRange { Values = [[floor.Time]] }, spreadsheetId, exactUpdateRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            try
            {
                var response = await updateRequest.ExecuteAsync();
                LogService.Instance.Log($"Data added to range: {response.UpdatedRange}");
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Error occurred while appending to Google Sheets: {ex.Message}");
            }
        }
    }
}
