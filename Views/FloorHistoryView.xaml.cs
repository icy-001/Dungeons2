using System.Diagnostics;
using System.Windows.Controls;

namespace Dungeons2.Views
{
    public partial class FloorHistoryView : UserControl
    {
        public FloorHistoryView()
        {
            InitializeComponent();
        }

        private void FloorsHyperlink_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _ = Process.Start(new ProcessStartInfo { FileName = "floors.txt", UseShellExecute = true });
        }
    }
}
