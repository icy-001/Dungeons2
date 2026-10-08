using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Dungeons2.Services;

namespace Dungeons2.ViewModels
{
    public partial class FloorHistoryViewModel : ObservableObject
    {
        public ObservableCollection<FloorData> FloorHistory => DungeonStore.Instance.FloorHistory;
        public ICollectionView FilteredFloorHistory { get; }
        public ObservableCollection<string> TeamSizeFilterOptions { get; } = new()
        {
            "No filter",
            "Solo",
            "Duo",
            "Trio",
            "4-man",
            "5-man"
        };

        [ObservableProperty]
        public partial FloorData? SelectedFloor { get; set; }

        [ObservableProperty]
        public partial BitmapSource? SelectedWinterfaceImage { get; private set; }

        [ObservableProperty]
        public partial string SelectedTeamSizeFilter { get; set; } = "No filter";

        public FloorHistoryViewModel()
        {
            FilteredFloorHistory = CollectionViewSource.GetDefaultView(FloorHistory);
            FilteredFloorHistory.Filter = FilterFloorHistory;
            PropertyChangedEventManager.AddHandler(Settings.Instance, OnWinterfaceLocationChanged, nameof(Settings.WinterfaceSaveLocation));
        }

        partial void OnSelectedFloorChanged(FloorData? value) => LoadWinterfaceImage();

        private void OnWinterfaceLocationChanged(object? sender, PropertyChangedEventArgs e) => LoadWinterfaceImage();

        private static string WinterfacePath(FloorData floor) =>
            Path.Combine(Settings.Instance.WinterfaceSaveLocation, $"{floor.Timestamp:yyyy-MM-dd HH-mm-ss}.png");

        private void LoadWinterfaceImage()
        {
            SelectedWinterfaceImage = null;
            if (SelectedFloor is null) return;
            try
            {
                string path = WinterfacePath(SelectedFloor);
                if (!File.Exists(path)) return;
                using var stream = File.OpenRead(path);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                SelectedWinterfaceImage = image;
            }
            catch (Exception e) when (FileSafety.IsFileError(e) || e is System.Runtime.InteropServices.COMException)
            {
                LogService.Instance.LogError($"Unable to load winterface preview: {e.Message}");
            }
        }

        partial void OnSelectedTeamSizeFilterChanged(string value)
        {
            FilteredFloorHistory.Refresh();
        }

        private bool FilterFloorHistory(object item)
        {
            if (item is not FloorData floor)
                return false;

            return SelectedTeamSizeFilter switch
            {
                "No filter" => true,
                "Solo" => floor.PartySize == 1,
                "Duo" => floor.PartySize == 2,
                "Trio" => floor.PartySize == 3,
                "4-man" => floor.PartySize == 4,
                "5-man" => floor.PartySize == 5,
                _ => true
            };
        }

        [RelayCommand]
        private void OpenWinterface(FloorData? floor)
        {
            if (floor is null)
                return;
            string filePath = WinterfacePath(floor);
            if (File.Exists(filePath))
                _ = Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
            else
                _ = MessageBox.Show("Winterface file not found");
        }
    }
}
