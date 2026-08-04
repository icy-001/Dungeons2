using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Dungeons2
{
    public class Settings : ObservableObject
    {
        public static Settings Instance { get; set; } = SettingsManager.Load();

        private int _mapX;
        public int MapX { get => _mapX; set => SetProperty(ref _mapX, value); }

        private int _mapY;
        public int MapY { get => _mapY; set => SetProperty(ref _mapY, value); }

        private int _hudX = 285;
        public int HudX { get => _hudX; set => SetProperty(ref _hudX, value); }

        private int _hudY = 21;
        public int HudY { get => _hudY; set => SetProperty(ref _hudY, value); }

        private int _winterfaceX = 704;
        public int WinterfaceX { get => _winterfaceX; set => SetProperty(ref _winterfaceX, value); }

        private int _winterfaceY = 373;
        public int WinterfaceY { get => _winterfaceY; set => SetProperty(ref _winterfaceY, value); }

        private string _mapSaveLocation = "Maps";
        public string MapSaveLocation { get => _mapSaveLocation; set => SetProperty(ref _mapSaveLocation, value); }

        private string _winterfaceSaveLocation = "Winterfaces";
        public string WinterfaceSaveLocation { get => _winterfaceSaveLocation; set => SetProperty(ref _winterfaceSaveLocation, value); }

        private double[] _targetRpms = [8.4, 13, 16, 18, 20];
        public double[] TargetRpms { get => _targetRpms; set => SetProperty(ref _targetRpms, value); }

        private bool _logToSheet;
        public bool LogToSheet { get => _logToSheet; set => SetProperty(ref _logToSheet, value); }

        private string _spreadsheetId = string.Empty;
        public string SpreadsheetId { get => _spreadsheetId; set => SetProperty(ref _spreadsheetId, value); }

        private string _spreadsheetColumn = "B";
        public string SpreadsheetColumn { get => _spreadsheetColumn; set => SetProperty(ref _spreadsheetColumn, value); }

        private int _spreadsheetStartRow = 1;
        public int SpreadsheetStartRow { get => _spreadsheetStartRow; set => SetProperty(ref _spreadsheetStartRow, value); }

        private bool _showKeydoors = true;
        public bool ShowKeydoors { get => _showKeydoors; set => SetProperty(ref _showKeydoors, value); }

        private bool _showKeyLocations = true;
        public bool ShowKeyLocations { get => _showKeyLocations; set => SetProperty(ref _showKeyLocations, value); }

        private bool _hideTrivialKeyblocks;
        public bool HideTrivialKeyblocks { get => _hideTrivialKeyblocks; set => SetProperty(ref _hideTrivialKeyblocks, value); }

        private bool _showRoomTicks = true;
        public bool ShowRoomTicks { get => _showRoomTicks; set => SetProperty(ref _showRoomTicks, value); }

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