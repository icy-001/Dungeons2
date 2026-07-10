using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Windows.Data;

namespace Dungeons2.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        public HomeViewModel()
        {
            LiveRoomTicksView = CollectionViewSource.GetDefaultView(DungeonStore.RoomTicks);
            LiveRoomTicksView.Filter = item => ((RoomSplit)item).Ticks > 0;
        }

        [ObservableProperty]
        public partial FloorData? SelectedFloor { get; set; }

        public ICollectionView LiveRoomTicksView { get; }

        public static DungeonStore DungeonStore => DungeonStore.Instance;
    }
}
