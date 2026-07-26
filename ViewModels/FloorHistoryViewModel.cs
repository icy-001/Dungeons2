using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;

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
        public partial string SelectedTeamSizeFilter { get; set; } = "No filter";

        public FloorHistoryViewModel()
        {
            FilteredFloorHistory = CollectionViewSource.GetDefaultView(FloorHistory);
            FilteredFloorHistory.Filter = FilterFloorHistory;
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
            string filePath = Path.Combine(Settings.Instance.WinterfaceSaveLocation, $"{floor.Timestamp:yyyy-MM-dd HH-mm-ss}.png");
            if (File.Exists(filePath))
                _ = Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
            else
                _ = MessageBox.Show("Winterface file not found");
        }
    }
}
