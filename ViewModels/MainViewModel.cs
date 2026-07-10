using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dungeons2.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;

namespace Dungeons2.ViewModels
{
    public class LogViewModel : ObservableObject { }

    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial object CurrentView { get; set; }

        public ObservableCollection<ProcessWindow> AvailableProcesses { get; } = [];

        [ObservableProperty]
        public partial ProcessWindow? SelectedProcess { get; set; }

        private readonly DispatcherTimer _processTimer;

        public MainViewModel()
        {
            CurrentView = new HomeViewModel();
            _processTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _processTimer.Tick += (s, e) => UpdateProcesses();
            _processTimer.Start();

            UpdateProcesses();
        }

        partial void OnSelectedProcessChanged(ProcessWindow? value)
        {
            if (value != null)
            {
                LogService.Instance.Log("Selected process changed to " + value.ToString());
                MapCaptureService.Instance.TargetWindowHandle = value.Handle;
            }
        }

        [RelayCommand]
        private void Navigate(string viewName)
        {
            CurrentView = viewName switch
            {
                "Home" => new HomeViewModel(),
                "Log" => new LogViewModel(),
                "Settings" => new SettingsViewModel(),
                _ => CurrentView
            };
        }

        private void UpdateProcesses()
        {
            var currentProcesses = Process.GetProcessesByName("rs2client")
                .Where(p => p.MainWindowHandle != IntPtr.Zero)
                .ToList();

            var currentHandles = currentProcesses.Select(p => p.MainWindowHandle).ToHashSet();

            for (int i = AvailableProcesses.Count - 1; i >= 0; i--)
            {
                if (!currentHandles.Contains(AvailableProcesses[i].Handle))
                {
                    if (SelectedProcess == AvailableProcesses[i])
                        SelectedProcess = null;
                    AvailableProcesses.RemoveAt(i);
                }
            }

            foreach (var proc in currentProcesses)
                if (!AvailableProcesses.Any(w => w.Handle == proc.MainWindowHandle))
                    AvailableProcesses.Add(new ProcessWindow(proc));

            if (SelectedProcess is null && AvailableProcesses.Count > 0)
                SelectedProcess = AvailableProcesses[0];
        }
    }
}