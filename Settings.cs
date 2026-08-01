using System.IO;
using System.Text.Json;

namespace Dungeons2
{
    public class Settings
    {
        public static Settings Instance { get; set; } = SettingsManager.Load();

        public int MapX { get; set; }
        public int MapY { get; set; }
        public int HudX { get; set; } = 285;
        public int HudY { get; set; } = 21;
        public int WinterfaceX { get; set; } = 704;
        public int WinterfaceY { get; set; } = 373;
        public string MapSaveLocation { get; set; } = "Maps";
        public string WinterfaceSaveLocation { get; set; } = "Winterfaces";
        public double[] TargetRpms { get; set; } = [8.4, 13, 16, 18, 20];
        public bool LogToSheet { get; set; } = false;
        public string SpreadsheetId { get; set; } = string.Empty;
        public string SpreadsheetColumn { get; set; } = "B";
        public int SpreadsheetStartRow { get; set; } = 1;
        public bool ShowKeydoors { get; set; } = true;
        public bool ShowKeyLocations { get; set; } = true;
        public bool HideTrivialKeyblocks { get; set; } = false;
        public bool ShowRoomTicks { get; set; } = true;

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