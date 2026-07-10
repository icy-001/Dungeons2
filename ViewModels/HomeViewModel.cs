using CommunityToolkit.Mvvm.ComponentModel;
using Dungeons2.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace Dungeons2.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        public HomeViewModel()
        {
            MapCaptureService.Instance.OnFloorCompleted += (floorData) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    FloorHistory.Add(floorData);
                });
            };
        }

        public ObservableCollection<FloorData> FloorHistory { get; } = [];

        [ObservableProperty]
        public partial FloorData? SelectedFloor { get; set; }

        public static DungeonStore DungeonStore => DungeonStore.Instance;
    }
}
