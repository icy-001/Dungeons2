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
        private readonly HomeViewModel _homeViewModel = new();
        private readonly FloorHistoryViewModel _floorHistoryViewModel = new();
        private readonly LogViewModel _logViewModel = new();
        private readonly SettingsViewModel _settingsViewModel = new();

        [ObservableProperty]
        public partial object CurrentView { get; set; }

        public ObservableCollection<ProcessWindow> AvailableProcesses { get; } = [];

        [ObservableProperty]
        public partial ProcessWindow? SelectedProcess { get; set; }

        private readonly DispatcherTimer _processTimer;

        public MainViewModel()
        {
            CurrentView = _homeViewModel;
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
                "Home" => _homeViewModel,
                "FloorHistory" => _floorHistoryViewModel,
                "Log" => _logViewModel,
                "Settings" => _settingsViewModel,
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
                    AvailableProcesses[i].Dispose();
                    AvailableProcesses.RemoveAt(i);
                }
            }

            foreach (var proc in currentProcesses)
            {
                if (!AvailableProcesses.Any(w => w.Handle == proc.MainWindowHandle))
                    AvailableProcesses.Add(new ProcessWindow(proc));
                else
                    proc.Dispose();
            }

            if (SelectedProcess is null && AvailableProcesses.Count > 0)
                SelectedProcess = AvailableProcesses[0];
        }
    }
}