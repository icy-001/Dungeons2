using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Dungeons2.Views
{
    public partial class FloorHistoryView : UserControl
    {
        public FloorHistoryView()
        {
            InitializeComponent();
            Loaded += FloorHistoryView_Loaded;
        }

        private void FloorHistoryView_Loaded(object sender, RoutedEventArgs e)
        {
            ScrollToLastItem();
        }

        private void ScrollToLastItem()
        {
            if (FloorHistoryListBox.Items.Count == 0)
                return;

            if (FloorHistoryListBox.SelectedIndex == -1)
                FloorHistoryListBox.SelectedIndex = FloorHistoryListBox.Items.Count - 1;
            FloorHistoryListBox.ScrollIntoView(FloorHistoryListBox.SelectedItem);
            FloorHistoryListBox.UpdateLayout();
        }

        private void FloorsHyperlink_Click(object sender, RoutedEventArgs e) => _ = Process.Start(new ProcessStartInfo { FileName = "floors.txt", UseShellExecute = true });
    }
}
