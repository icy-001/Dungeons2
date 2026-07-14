using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Dungeons2.ViewModels
{
    public partial class FloorHistoryViewModel : ObservableObject
    {
        public ObservableCollection<FloorData> FloorHistory => DungeonStore.Instance.FloorHistory;
        [ObservableProperty]
        public partial FloorData? SelectedFloor { get; set; }

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
