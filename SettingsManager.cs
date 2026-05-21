using System.IO;
using System.Text.Json;

namespace Dungeons2
{
    public class Settings
    {
        public int MapX { get; set; }
        public int MapY { get; set; }
        public int KeybagX { get; set; } = 290;
        public int KeybagY { get; set; } = 73;
        public int WinterfaceX { get; set; } = 704;
        public int WinterfaceY { get; set; } = 373;
        public string MapSaveLocation { get; set; } = "Maps";
        public string WinterfaceSaveLocation { get; set; } = "Winterfaces";

        public static Settings Default { get; set; } = new();

        public void Save()
        {
            SettingsManager.Save(this);
        }
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
                return new Settings();

            string json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<Settings>(json) ?? new();
        }
    }
}