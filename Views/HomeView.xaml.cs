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
                //var wint = new WinterfaceReader(SpriteLoader.LoadFromFile(@"C:\Projects\Dungeons2\bin\Release\net10.0-windows10.0.26100.0\Winterfaces\2026-07-22 21-14-48.png"));
                //var data = wint.Read();
                //MessageBox.Show(data.Bonus.ToString());
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
