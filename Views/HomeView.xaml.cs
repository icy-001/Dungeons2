using Dungeons2.Services;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace Dungeons2.Views
{
    /// <summary>
    /// Interaction logic for HomeView.xaml
    /// </summary>
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.D)
            {
                foreach (var floor in DungeonStore.Instance.FloorHistory)
                {
                    if (floor.Map is null)
                        continue;
                    for (int i = 0; i < 8; ++i)
                        for (int j = 0; j < 8; ++j)
                        {
                            var room = floor.Map[i, j];
                            if (room.Opened && room.Parent == Directions.None && !room.IsBase)
                            {
                                LogService.Instance.Log($"{floor.Timestamp:G} - Floor {floor.FloorNumber} - Room ({i}, {j}) has no parent.");
                            }
                        }
                }
            }
        }

        private void FloorsHyperlink_Click(object sender, System.Windows.RoutedEventArgs e) => _ = Process.Start(new ProcessStartInfo { FileName = "floors.txt", UseShellExecute = true });

        private void RoomTickHistogramScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
                return;

            if (e.Delta > 0)
                scrollViewer.LineLeft();
            else
                scrollViewer.LineRight();

            e.Handled = true;
        }
    }
}
