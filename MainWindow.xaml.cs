using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
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
        private SheetsService? sheetsService;

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

            InitializeGoogleSheets();
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

        public static double TargetRpm { get => Settings.Default.TargetRpm; set => Settings.Default.TargetRpm = value; }
        public static bool LogToSheet { get => Settings.Default.LogToSheet; set => Settings.Default.LogToSheet = value; }
        public static string SpreadsheetId { get => Settings.Default.SpreadsheetId; set => Settings.Default.SpreadsheetId = value; }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnContentRendered(e);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            timer.Tick += (sender, e) => UpdateProcesses();
            timer.Start();
            UpdateProcesses();

            _ = Task.Run(MainLoopAsync);
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
                    DoCapture();
            }
            catch (OperationCanceledException) { }
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"[{DateTimeOffset.Now:G}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
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
                        RoomCount = dungeonStore.RoomCount,
                        DeadEnds = dungeonStore.DeadEndCount()
                    };
                    if (floorData.TotalXP == 0)
                    {
                        Log("Caught winterface frame where total XP didn't show up; skipping");
                    }
                    else
                    {
                        winterfaceSuccess = true;
                        RawImage winterfaceImage2 = new(winterfaceImage.Pixels.ToArray(), winterfaceImage.Width, winterfaceImage.Height);
                        Dispatcher.Invoke(() =>
                        {
                            Log($"Floor {dungeonStore.FloorCounter} time: {floorData.Time}");
                            FloorHistory.Add(floorData);
                            File.AppendAllText("floors.txt", $"{floorData.Id}\t{floorData.Timestamp:G}\t{floorData.Teammates}\t{floorData.FloorNumber}\t{floorData.Size}\t{floorData.Time}\t{floorData.Bonus}\t{floorData.MobsKilledMod}\t{floorData.Boost}\t{floorData.TotalMod}\t{floorData.TotalXP}\t{floorData.RoomCount}\t{floorData.DeadEnds}\t{floorData.Notes}\n");
                            // Save map and winterface images
                            ImageWriter.SaveToPng(dungeonStore.MapImage, $"{Settings.Default.MapSaveLocation}\\{floorData.Timestamp:yyyy-MM-dd HH-mm-ss}.png");
                            ImageWriter.SaveCroppedToPng(winterfaceImage2, $"{Settings.Default.WinterfaceSaveLocation}\\{floorData.Timestamp:yyyy-MM-dd HH-mm-ss}.png", 0, 0, WinterfaceReader.WinterfaceWidth, WinterfaceReader.WinterfaceHeight);
                            if (Settings.Default.LogToSheet)
                                _ = AppendToGoogleSheets();
                        });
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
            if (!dungeonStore.InFloor)
            {
                dungeonStore.InFloor = true;
                dungeonStore.KeyMask = 0;
                dungeonStore.RoomCount = 0;
                dungeonStore.FloorStartTime = DateTimeOffset.Now - TimeSpan.FromMilliseconds(1150); // Empirical
                dungeonStore.RoomTimer.Restart();
                Log($"Start of floor {++dungeonStore.FloorCounter}");
            }
            dungeonStore.LastActiveTime = DateTimeOffset.Now;
            var (rooms, roomCount) = reader.ReadMap();
            bool changed = false;
            if (roomCount > dungeonStore.RoomCount)
            {
                changed = true;
                dungeonStore.Rooms = rooms;
                dungeonStore.RoomCount = roomCount;
                var ticks = Math.Round(dungeonStore.RoomTimer.Elapsed.TotalMilliseconds / 600);
                if (roomCount > 1)
                    Log($"Room {roomCount} took {ticks} ticks ({ticks * 0.6:F1} s)");
                dungeonStore.RoomTimer.Restart();
            }
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
                //Log($"Found key{suffix}: {keys.Trim()}");
            }
            if (dungeonStore.KeyMask != newKeymask)
            {
                changed = true;
                dungeonStore.KeyMask |= newKeymask;
            }
            if (changed)
                overlayWindow.Update();

            var pixels = mapKeybagImage.Pixels.ToArray();
            var stride = mapKeybagImage.Width * 4;
            Dispatcher.Invoke(() =>
            {
                dungeonStore.MapImage.WritePixels(new(0, 0, MapKeybagReader.MapWidth, MapKeybagReader.MapHeight), pixels, stride, 0);
            });
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

        [GeneratedRegex("[^0-9.]+")]
        private static partial Regex NumericRegex();

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = NumericRegex();
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start("explorer.exe", e.Uri.OriginalString);
            e.Handled = true;
        }
        private void InitializeGoogleSheets()
        {
            if (!File.Exists("google-credentials.json"))
            {
                LogSheetsCheckBox.IsEnabled = false;
                return;
            }

            string[] Scopes = [SheetsService.Scope.Spreadsheets];
            string serviceAccountKeyPath = "google-credentials.json";

            GoogleCredential credential = CredentialFactory.FromFile<ServiceAccountCredential>(serviceAccountKeyPath)
                .ToGoogleCredential()
                .CreateScoped(Scopes);

            sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "WPF Sheets Integration"
            });
        }
        private async Task AppendToGoogleSheets()
        {
            if (sheetsService is null || FloorHistory.Count == 0)
                return;

            string spreadsheetId = Settings.Default.SpreadsheetId;
            // 1. Read ONLY Column B to find how many rows are occupied
            string readRange = "B3:B";
            var getRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, readRange);
            var getResponse = await getRequest.ExecuteAsync();

            // Calculate the next empty row in Column B
            int nextEmptyRow = (getResponse.Values?.Count ?? 0) + 3;

            // 2. Use UPDATE to write exactly to that specific cell
            string exactUpdateRange = $"B{nextEmptyRow}";

            var valueRange = new ValueRange
            {
                Values = [[FloorHistory[^1].Time]]
            };

            var updateRequest = sheetsService.Spreadsheets.Values.Update(valueRange, spreadsheetId, exactUpdateRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            try
            {
                var response = await updateRequest.ExecuteAsync();
                Log($"Data added to range: {response.UpdatedRange}");
            }
            catch (Exception ex)
            {
                Log($"Error occurred while appending to Google Sheets: {ex.Message}");
            }
        }
    }
}
