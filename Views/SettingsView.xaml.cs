using Dungeons2.Services;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dungeons2.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            SetVersionText();
        }

        private void SetVersionText()
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            VersionTextBlock.Text = $"v{version?.ToString(3) ?? "0.0.0"}";
        }

        private void AutoDetectCalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            if (AutoCalibrationService.TryAutoDetect(out int mapX, out int mapY, out int hudX, out int hudY))
            {
                Settings.Instance.MapX = mapX;
                Settings.Instance.MapY = mapY;
                Settings.Instance.HudX = hudX;
                Settings.Instance.HudY = hudY;
                Settings.Instance.Save();

                CalibrationStatusText.Text = $"Calibrated: map ({mapX}, {mapY}), HUD ({hudX}, {hudY})";
                CalibrationStatusText.Foreground = Brushes.LightGreen;
            }
            else
            {
                CalibrationStatusText.Text = "Calibration failed. Make sure the game is visible and the map/HUD markers are on screen.";
                CalibrationStatusText.Foreground = Brushes.OrangeRed;
            }
        }
    }
}
