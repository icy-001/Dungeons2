using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Text.Json;
using Dungeons2.Services;

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
            => SaveTo(data, SettingsFile);

        internal static void SaveTo(Settings data, string path)
        {
            FileSafety.Try("Saving settings", () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(data, jsonSerializerOptions));
                if (File.Exists(path))
                {
                    // Keep the last valid settings, not a corrupted primary file.
                    bool valid = false;
                    try { valid = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) != null; }
                    catch (JsonException) { }
                    File.Replace(temporary, path, valid ? path + ".bak" : null);
                }
                else
                    File.Move(temporary, path);
            });
        }

        public static Settings Load() => LoadFrom(SettingsFile);

        internal static Settings LoadFrom(string path)
        {
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(candidate));
                    if (settings is null) throw new JsonException("Settings were null.");
                    settings.MapSaveLocation = string.IsNullOrWhiteSpace(settings.MapSaveLocation) ? "Maps" : settings.MapSaveLocation;
                    settings.WinterfaceSaveLocation = string.IsNullOrWhiteSpace(settings.WinterfaceSaveLocation) ? "Winterfaces" : settings.WinterfaceSaveLocation;
                    settings.AverageRpms ??= [8.4, 13, 16, 18, 20];
                    return settings;
                }
                catch (Exception e) when (e is JsonException || FileSafety.IsFileError(e))
                {
                    LogService.Instance.LogError($"Unable to load {candidate}: {e.Message}");
                }
            }
            return new();
        }
    }
}
