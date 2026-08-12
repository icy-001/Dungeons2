using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Text.Json;

namespace Dungeons2
{
    public partial class Settings : ObservableObject
    {
        public static Settings Instance { get; set; } = SettingsManager.Load();

        [ObservableProperty] public partial int MapX { get; set; }
        [ObservableProperty] public partial int MapY { get; set; }
        [ObservableProperty] public partial int HudX { get; set; } = 285;
        [ObservableProperty] public partial int HudY { get; set; } = 21;
        [ObservableProperty] public partial int WinterfaceX { get; set; } = 704;
        [ObservableProperty] public partial int WinterfaceY { get; set; } = 373;
        [ObservableProperty] public partial string MapSaveLocation { get; set; } = "Maps";
        [ObservableProperty] public partial string WinterfaceSaveLocation { get; set; } = "Winterfaces";
        [ObservableProperty] public partial double[] AverageRpms { get; set; } = [8.4, 13, 16, 18, 20];
        [ObservableProperty] public partial bool LogToSheet { get; set; }
        [ObservableProperty] public partial string SpreadsheetId { get; set; } = string.Empty;
        [ObservableProperty] public partial string SpreadsheetColumn { get; set; } = "B";
        [ObservableProperty] public partial int SpreadsheetStartRow { get; set; } = 1;
        [ObservableProperty] public partial bool ShowKeydoors { get; set; } = true;
        [ObservableProperty] public partial bool ShowKeyLocations { get; set; } = true;
        [ObservableProperty] public partial bool HideTrivialKeyblocks { get; set; } = false;
        [ObservableProperty] public partial bool ShowRoomTicks { get; set; } = true;
        [ObservableProperty] public partial bool ShowPlayerVisitedOverlay { get; set; } = true;

        public void Save() => SettingsManager.Save(this);
    }

    public static class SettingsManager
    {
        static readonly JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };

        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dungeons2"
        );

        private static readonly string SettingsFile = Path.Combine(AppDataPath, "settings.json");

        public static void Save(Settings data)
        {
            _ = Directory.CreateDirectory(AppDataPath);
            string json = JsonSerializer.Serialize(data, jsonSerializerOptions);
            File.WriteAllText(SettingsFile, json);
        }

        public static Settings Load()
        {
            if (!File.Exists(SettingsFile))
                return new();

            string json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<Settings>(json) ?? new();
        }
    }
}
