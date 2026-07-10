using System.Diagnostics;
using System.Windows.Controls;

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

        private void FloorsHyperlink_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _ = Process.Start(new ProcessStartInfo { FileName = "floors.txt", UseShellExecute = true });
        }
    }
}
