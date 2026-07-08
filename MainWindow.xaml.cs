using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
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
        private readonly GameCapture capture;
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
            _ = Directory.CreateDirectory(Settings.Default.MapSaveLocation);
            _ = Directory.CreateDirectory(Settings.Default.WinterfaceSaveLocation);

            capture = new();
            overlayWindow = new();
            overlayWindow.Show();

            InitializeGoogleSheets();
            Log("Connected to Google Sheets");
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
                    DungeonStore.KeyMask = 0;
                    DungeonStore.Active = false;
                }
            }
        }
        public static bool LogToSheet { get => Settings.Default.LogToSheet; set => Settings.Default.LogToSheet = value; }
        public static string SpreadsheetId { get => Settings.Default.SpreadsheetId; set => Settings.Default.SpreadsheetId = value; }

        static DungeonStore DungeonStore => DungeonStore.Instance;

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

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                //var lines = File.ReadAllLines("C:\\Temp\\data.csv");
                //var line = lines[600];
                //var tokens = line.Split(',');
                //var timestamp = DateTimeOffset.Parse(tokens[0]);
                //var rc = tokens[1];
                //var deadEnds = tokens[2];
                //var mapFile = @$"C:\Projects\Dungeons2\bin\Release\net10.0-windows10.0.26100.0\Maps\{timestamp:yyyy-MM-dd HH-mm-ss}.png";
                //Log($"{mapFile}, {File.Exists(mapFile)}, {rc}, {deadEnds}");

                //AnnotatedMapImage.Source = AnnotatedMapRenderer.DrawMap(280, 280, DungeonStore.Map);
                AnnotatedMapImage.Source = DungeonStore.MapImage;
            }
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
            _ = Dispatcher.InvokeAsync(() =>
            {
                LogTextBox.AppendText($"[{DateTimeOffset.Now:G}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private void DoCapture()
        {
            if (!capture.IsActive)
                return;

            using var keybagFrame = capture.CaptureMapKeybag();
            var mapKeybagImage = RawImageView.FromFrame(keybagFrame);
            MapKeybagReader reader = new(mapKeybagImage);

            bool hasMap = reader.HasMap();
            if (!hasMap)
            {
                bool reset = DateTimeOffset.Now - DungeonStore.LastActiveTime > TimeSpan.FromSeconds(6);
                // Check winterface
                using var winterfaceFrame = capture.CaptureWinterface();
                var winterfaceImage = RawImageView.FromFrame(winterfaceFrame);
                WinterfaceReader winterfaceReader = new(winterfaceImage);

                bool winterfaceSuccess = false;
                if (winterfaceReader.HasWinterface() && (FloorHistory.Count == 0 || FloorHistory[^1].Timestamp != DungeonStore.LastActiveTime))
                {
                    StringBuilder notesSB = new();
                    for (int i = 0; i < DungeonStore.RoomTicks.Count; ++i)
                    {
                        var (r, ticks) = DungeonStore.RoomTicks[i];
                        if (ticks >= 20)
                        {
                            if (notesSB.Length > 0)
                                _ = notesSB.Append(", ");
                            _ = notesSB.Append($"r{r}: {ticks}t");
                        }
                    }

                    var (skillCount, skillTicks) = DungeonStore.Map.SkillDoorStats();
                    var floorData = winterfaceReader.Read() with
                    {
                        Id = FloorHistory.Count + 1,
                        Timestamp = DungeonStore.LastActiveTime,
                        RoomCount = DungeonStore.RoomCount,
                        DeadEnds = DungeonStore.Map.DeadEndCount(),
                        SkillDoors = skillCount,
                        SkillDoorTicks = skillTicks,
                        PossiblePuzzleRooms = DungeonStore.Map.PossiblePuzzleCount(),
                        Notes = notesSB.ToString()
                    };
                    Log($"Floor {DungeonStore.FloorCounter} time={floorData.Time} | key count={BitOperations.PopCount(DungeonStore.KeyMask)}");
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
                            FloorHistory.Add(floorData);
                            File.AppendAllText("floors.txt", floorData.ToTabSeparatedString());
                            // Save map and winterface images
                            ImageWriter.SaveToPng(DungeonStore.MapImage, $"{Settings.Default.MapSaveLocation}\\{floorData.Timestamp:yyyy-MM-dd HH-mm-ss}.png");
                            ImageWriter.SaveCroppedToPng(winterfaceImage2, $"{Settings.Default.WinterfaceSaveLocation}\\{floorData.Timestamp:yyyy-MM-dd HH-mm-ss}.png", 0, 0, WinterfaceReader.WinterfaceWidth, WinterfaceReader.WinterfaceHeight);
                            if (Settings.Default.LogToSheet && floorData.Bonus == 1)
                                _ = AppendToGoogleSheets();
                        });
                    }
                    reset = true;
                }
                if (reset)
                {
                    if (DungeonStore.Active && !winterfaceSuccess)
                        Log("Out of floor or map turned off; resetting.");
                    DungeonStore.KeyMask = 0;
                    DungeonStore.Active = false;
                }
                return;
            }
            if (!DungeonStore.Active)
            {
                DungeonStore.Active = true;
                DungeonStore.KeyMask = 0;
                DungeonStore.RoomCount = 0;
                DungeonStore.FloorStartTime = DateTimeOffset.Now - TimeSpan.FromMilliseconds(1150); // Empirical
                DungeonStore.Map.Clear();
                DungeonStore.RoomTicks.Clear();
                DungeonStore.RoomTimer.Restart();
                Dispatcher.Invoke(overlayWindow.ClearRects);
                Log($"Start of floor {++DungeonStore.FloorCounter}");
            }
            DungeonStore.LastActiveTime = DateTimeOffset.Now;
            var (map, roomCount) = reader.ReadMap();
            bool mapChanged = DungeonStore.Map.Merge(map);
            ulong currentKeymask = reader.ReadKeybag();
            ulong keymaskDelta = currentKeymask & ~DungeonStore.KeyMask;
            // Since the 2026/7/6 update we can now track key locations in solos accurately.
            if (keymaskDelta != 0 && DungeonStore.Map.PlayerCount == 1)
            {
                var loc = DungeonStore.Map.PlayerLocations[0]!.Value;
                for (int k = 0; k < 64; ++k)
                    if ((keymaskDelta & 1UL << k) != 0)
                    {
                        Log($"Found key {Registry.GetKeyName(k)} in {loc.ToChessString()}");
                        DungeonStore.Map.Rooms[loc.Row, loc.Col].Key = k;
                    }
            }
            bool changed = mapChanged || keymaskDelta != 0;
            if (roomCount > DungeonStore.RoomCount)
            {
                DungeonStore.RoomCount = roomCount;
                int ticks = (int)Math.Round(DungeonStore.RoomTimer.Elapsed.TotalMilliseconds / 600);
                if (roomCount > 1)
                {
                    DungeonStore.RoomTicks.Add((roomCount, ticks));
                    Log($"Room {roomCount} took {ticks} ticks ({ticks * 0.6:F1} s)");
                }
                DungeonStore.RoomTimer.Restart();
            }
            if (changed)
            {
                DungeonStore.KeyMask |= currentKeymask;
                _ = Dispatcher.InvokeAsync(overlayWindow.Update);
            }

            DungeonStore.UpdateMapImage(mapKeybagImage);
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
                            dto.ToString("G"));
                    }
                }
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            _ = Process.Start("explorer.exe", e.Uri.OriginalString);
            e.Handled = true;
        }

        private void InitializeGoogleSheets()
        {
            if (!File.Exists("google-credentials.json"))
            {
                LogToSheet = false;
                LogSheetsCheckBox.IsEnabled = false;
                return;
            }

            string[] Scopes = [SheetsService.Scope.Spreadsheets];
            string serviceAccountKeyPath = "google-credentials.json";

            var credential = CredentialFactory.FromFile<ServiceAccountCredential>(serviceAccountKeyPath)
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
            string readRange = "B2:B";
            var getRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, readRange);
            var getResponse = await getRequest.ExecuteAsync();

            // Calculate the next empty row in Column B
            int nextEmptyRow = (getResponse.Values?.Count ?? 0) + 2;

            // 2. Use UPDATE to write exactly to that specific cell
            string exactUpdateRange = $"B{nextEmptyRow}";

            var updateRequest = sheetsService.Spreadsheets.Values.Update(
                new ValueRange { Values = [[FloorHistory[^1].Time]] }, spreadsheetId, exactUpdateRange);
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
