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
                // Debug
                var img = SpriteLoader.LoadFromFile(@"C:\Projects\Dungeons2\bin\Debug\net10.0-windows10.0.26100.0\Winterfaces\2026-06-18 02-05-35.png");
                var wint = new WinterfaceReader(img);
                wint.Read();
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
