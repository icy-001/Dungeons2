using Dungeons2.Services;
using System.IO;
using System.Windows;

namespace Dungeons2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _ = Directory.CreateDirectory(Settings.Instance.MapSaveLocation);
            _ = Directory.CreateDirectory(Settings.Instance.WinterfaceSaveLocation);

            Registry.Instance.Initialize();
            GoogleSheetsService.Instance.Initialize();
            MapCaptureService.Instance.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Settings.Instance.Save();
            GameCapture.Instance.Dispose();
            base.OnExit(e);
        }
    }
}
