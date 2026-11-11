// Isolate the real integration services from capture hardware and the user's settings.
namespace Dungeons2
{
    public sealed class Settings : System.ComponentModel.INotifyPropertyChanged
    {
        public static Settings Instance { get; } = new();
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private bool logToSheet = true;
        public bool LogToSheet
        {
            get => logToSheet;
            set { logToSheet = value; PropertyChanged?.Invoke(this, new(nameof(LogToSheet))); }
        }
        public string SpreadsheetId { get; set; } = "";
        public string SpreadsheetName { get; set; } = "";
        private string spreadsheetStartCell = "A2";
        public string SpreadsheetStartCell
        {
            get => spreadsheetStartCell;
            set { spreadsheetStartCell = value; PropertyChanged?.Invoke(this, new(nameof(SpreadsheetStartCell))); }
        }
        public void Save() { }
    }
    public sealed class FloorData
    {
        public int Id { get; set; }
        public int FloorNumber { get; set; }
        public double Bonus { get; set; } = 1;
        public TimeSpan Time { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}
namespace Dungeons2.Services
{
    public sealed class MapCaptureService
    {
        public static MapCaptureService Instance { get; } = new();
        public event Action<FloorData>? OnFloorCompleted;
        public void Complete(FloorData floor) => OnFloorCompleted?.Invoke(floor);
    }
    public sealed class LogService
    {
        public static LogService Instance { get; } = new();
        public void Log(string message) { }
        public void LogError(string message) { }
    }
}
