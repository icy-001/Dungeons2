using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Dungeons2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly CancellationTokenSource cts = new();
        private readonly DungeonStore dungeonStore = new();
        private readonly GameCapture capture = new();
        private readonly OverlayWindow overlayWindow;

        public MainWindow()
        {
            InitializeComponent();

            Registry.Initialize();
            Settings.Default = SettingsManager.Load();
            FontType.InitializeFonts();

            DataContext = this;
            CommandManager.AddPreviewExecutedHandler(HistoryGrid, OnPreviewCopy);

            // Create maps and winterfaces directory
            Directory.CreateDirectory(Settings.Default.MapSaveLocation);
            Directory.CreateDirectory(Settings.Default.WinterfaceSaveLocation);

            overlayWindow = new(dungeonStore);
            overlayWindow.Show();
        }

        public ObservableCollection<FloorData> FloorHistory { get; } = [];
        public ObservableCollection<ProcessWindow> RSWindows { get; } = [];
        public ProcessWindow? SelectedWindow
        {
            get; set
            {
                if (field != value)
                {
                    field = value;
                    Log($"Setting RS window to {value}");
                    capture?.SetTarget(new((ulong)(value?.Handle ?? IntPtr.Zero)));
                    overlayWindow.RsHwnd = value?.Handle ?? IntPtr.Zero;
                    dungeonStore.KeyMask = 0;
                    dungeonStore.InFloor = false;
                }
            }
        }

        public double TargetRpm { get => Settings.Default.TargetRpm; set { Settings.Default.TargetRpm = value; } }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnContentRendered(e);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            timer.Tick += (sender, e) => UpdateProcesses();
            timer.Start();
            UpdateProcesses();

            _ = MainLoopAsync();
        }

        private void UpdateProcesses()
        {
            var currentProcesses = Process.GetProcessesByName("rs2client")
                                              .Where(p => p.MainWindowHandle != IntPtr.Zero)
                                              .ToList();
            var currentHandles = currentProcesses.Select(p => p.MainWindowHandle).ToHashSet();
            for (int i = RSWindows.Count - 1; i >= 0; i--)
                if (!currentHandles.Contains(RSWindows[i].Handle))
                {
                    if (SelectedWindow == RSWindows[i])
                        SelectedWindow = null;
                    RSWindows.RemoveAt(i);
                }

            foreach (var proc in currentProcesses)
                if (!RSWindows.Any(w => w.Handle == proc.MainWindowHandle))
                    RSWindows.Add(new ProcessWindow(proc));

            if (SelectedWindow == null)
                RSWindowComboBox.SelectedIndex = 0;
        }

        protected override void OnClosed(EventArgs e)
        {
            Settings.Default.Save();
            base.OnClosed(e);
        }

        private void OnPreviewCopy(object sender, ExecutedRoutedEventArgs e)
        {
            // Only trigger if the command is Copy, and the user actually has rows selected
            if (e.Command == ApplicationCommands.Copy && HistoryGrid.SelectedItems.Count > 0)
            {
                // Dispatcher.InvokeAsync queues this block of code to run immediately 
                // AFTER the DataGrid finishes its default copy behavior.
                _ = Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (Clipboard.ContainsText(TextDataFormat.Text))
                        {
                            string plainText = Clipboard.GetText(TextDataFormat.Text);
                            Clipboard.Clear();
                            Clipboard.SetText(plainText);
                        }
                    }
                    catch (Exception)
                    {
                        // The Windows clipboard can occasionally be locked by other apps.
                        // We swallow the exception here to prevent crashing the app.
                    }

                }, DispatcherPriority.Background);
            }
        }

        private async Task MainLoopAsync()
        {
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(50));

            try
            {
                while (await timer.WaitForNextTickAsync(cts.Token))
                {
                    DoCapture();
                }
            }
            catch (OperationCanceledException) { }
        }

        private void Log(string message)
        {
            LogTextBox.AppendText($"[{DateTimeOffset.Now:g}] {message}\n");
            LogTextBox.ScrollToEnd();
        }

        private void DoCapture()
        {
            if (!capture.IsActive)
                return;

            var mapKeybagImage = RawImageView.FromFrame(capture.CaptureMapKeybag());
            MapKeybagReader reader = new(mapKeybagImage);

            bool hasMap = reader.HasMap();
            if (!hasMap)
            {
                bool reset = DateTimeOffset.Now - dungeonStore.LastActiveTime > TimeSpan.FromSeconds(6);
                // Check winterface
                var winterfaceImage = RawImageView.FromFrame(capture.CaptureWinterface());
                WinterfaceReader winterfaceReader = new(winterfaceImage);

                bool winterfaceSuccess = false;
                if (winterfaceReader.HasWinterface() && (FloorHistory.Count == 0 || FloorHistory[^1].Timestamp != dungeonStore.LastActiveTime))
                {
                    var floorData = winterfaceReader.Read() with
                    {
                        Id = FloorHistory.Count + 1,
                        Timestamp = dungeonStore.LastActiveTime,
                        RoomCount = dungeonStore.RoomCount(),
                        DeadEnds = dungeonStore.DeadEndCount()
                    };
                    if (floorData.TotalXP == 0)
                    {
                        Log("Caught winterface frame where total XP didn't show up; skipping");
                    }
                    else
                    {
                        winterfaceSuccess = true;
                        FloorHistory.Add(floorData);
                        // Save map and winterface images
                        ImageWriter.SaveToPng(dungeonStore.MapImage, $"{Settings.Default.MapSaveLocation}\\{floorData.Timestamp:yyyy-MM-dd HH-mm-ss}.png");
                        ImageWriter.SaveCroppedToPng(winterfaceImage, $"{Settings.Default.WinterfaceSaveLocation}\\{floorData.Timestamp:yyyy-MM-dd HH-mm-ss}.png", 0, 0, WinterfaceReader.WinterfaceWidth, WinterfaceReader.WinterfaceHeight);
                        Log($"Floor {dungeonStore.FloorCounter} time: {floorData.Time}");
                    }
                    reset = true;
                }
                if (reset)
                {
                    if (dungeonStore.InFloor && !winterfaceSuccess)
                        Log("Out of floor or map turned off; resetting.");
                    dungeonStore.KeyMask = 0;
                    dungeonStore.InFloor = false;
                }
                return;
            }
            dungeonStore.LastActiveTime = DateTimeOffset.Now;
            dungeonStore.Rooms = reader.ReadMap();
            if (!dungeonStore.InFloor)
            {
                dungeonStore.KeyMask = 0;
                dungeonStore.FloorStartTime = DateTimeOffset.Now - TimeSpan.FromMilliseconds(1150); // Empirical
                Log($"Start of floor {++dungeonStore.FloorCounter}");
            }
            dungeonStore.InFloor = true;
            ulong newKeymask = reader.ReadKeybag();
            ulong diff = newKeymask & ~dungeonStore.KeyMask;
            if (diff != 0)
            {
                string keys = string.Empty;
                int cnt = 0;
                for (int i = 0; i < 64; ++i)
                    if ((diff & (1UL << i)) != 0)
                    {
                        ++cnt;
                        keys += $"{Registry.GetKeyName(i)} ";
                    }
                string suffix = cnt == 1 ? string.Empty : "s";
                Log($"Found key{suffix}: {keys.Trim()}");
            }
            dungeonStore.KeyMask |= newKeymask;
            dungeonStore.MapImage.WritePixels(new(0, 0, MapKeybagReader.MapWidth, MapKeybagReader.MapHeight), mapKeybagImage.Pixels.ToArray(), mapKeybagImage.Width * 4, 0);
        }

        private void HistoryGrid_CopyingRowClipboardContent(object sender, DataGridRowClipboardEventArgs e)
        {
            for (int i = 0; i < e.ClipboardRowContent.Count; i++)
            {
                var cellContent = e.ClipboardRowContent[i];

                if (cellContent.Column is DataGridBoundColumn boundColumn &&
                    boundColumn.Binding is System.Windows.Data.Binding binding &&
                    binding.Path.Path == "Timestamp")
                {
                    if (cellContent.Content is DateTimeOffset dto)
                    {
                        e.ClipboardRowContent[i] = new DataGridClipboardCellContent(
                            e.Item,
                            cellContent.Column,
                            dto.ToString("g"));
                    }
                }
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = NumericRegex();
            e.Handled = regex.IsMatch(e.Text);
        }

        [GeneratedRegex("[^0-9.]+")]
        private static partial Regex NumericRegex();
    }
}
