using Dungeons2.Services;
using System.Windows;
using System.Windows.Controls;

namespace Dungeons2.Views
{
    /// <summary>
    /// Interaction logic for LogView.xaml
    /// </summary>
    public partial class LogView : UserControl
    {
        public LogView()
        {
            InitializeComponent();
            LogTextBox.Text = string.Join(Environment.NewLine, LogService.Instance.Logs.Select(log => $"[{log.Item1:G}] {log.Item2}")) + Environment.NewLine;
            LogService.Instance.OnLogReceived += HandleNewLog;
        }

        private void HandleNewLog((DateTimeOffset, string) logEntry)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LogTextBox.AppendText($"[{logEntry.Item1:G}] {logEntry.Item2}\n");
                LogTextBox.ScrollToEnd();
            });
        }
    }
}
