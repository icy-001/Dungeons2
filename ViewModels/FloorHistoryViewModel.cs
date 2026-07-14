using CommunityToolkit.Mvvm.ComponentModel;

namespace Dungeons2.ViewModels
{
    public partial class FloorHistoryViewModel : ObservableObject
    {
        public DungeonStore DungeonStore => DungeonStore.Instance;

        [ObservableProperty]
        public partial FloorData? SelectedFloor { get; set; }
    }
}
