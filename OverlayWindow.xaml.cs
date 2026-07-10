using Dungeons2.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static Dungeons2.Interop;

namespace Dungeons2
{
    /// <summary>
    /// Interaction logic for Overlay.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private const double HighlightOpacity = 0.1;
        private IntPtr hwnd;
        private readonly DispatcherTimer trackerTimer;
        private readonly Rectangle?[,] activeRects = new Rectangle?[8, 8];
        private ulong currentKeyMask;
        private readonly DoubleAnimation opacityAnimation = new()
        {
            From = HighlightOpacity,
            To = 0.5,
            Duration = TimeSpan.FromMilliseconds(300),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(6),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        public OverlayWindow()
        {
            InitializeComponent();

            trackerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            trackerTimer.Tick += (sender, args) =>
            {
                TrackingLoop();
                UpdateText();
            };

            MapCaptureService.Instance.OnNewFloor += () => Dispatcher.InvokeAsync(ClearRects);
            MapCaptureService.Instance.OnMapChanged += () => Dispatcher.InvokeAsync(Update);
        }

        static DungeonStore DungeonStore => DungeonStore.Instance;

        /// <summary>
        /// Updates the overlay window. Must be called from UI thread.
        /// </summary>
        public void Update()
        {
            //string keys = string.Empty;
            //for (int k = 0; k < 64; ++k)
            //    if ((DungeonStore.KeyMask & (1UL << k)) != 0) keys += Registry.GetKeyName(k) + " ";
            //Debug.WriteLine("current keys = " + keys);
            for (int i = 0; i < 8; ++i)
                for (int j = 0; j < 8; ++j)
                {
                    int key = DungeonStore.Map.Rooms[i, j].Lock;
                    bool unlockable = key >= 0 && key < 64 && (DungeonStore.KeyMask & (1UL << key)) != 0 && !DungeonStore.Map.Rooms[i, j].Opened;
                    if (unlockable && activeRects[i, j] is null)
                    {
                        var rect = new Rectangle
                        {
                            Width = 26,
                            Height = 26,
                            Fill = new SolidColorBrush(Colors.White) { Opacity = HighlightOpacity },
                            Stroke = Brushes.White
                        };
                        Canvas.SetLeft(rect, 14 + 32 * j);
                        Canvas.SetTop(rect, 15 + 32 * i);
                        activeRects[i, j] = rect;
                        _ = HighlightCanvas.Children.Add(rect);

                        // Only flash if update is due to key found.
                        if ((currentKeyMask & (1UL << key)) == 0)
                            rect.Fill.BeginAnimation(Brush.OpacityProperty, opacityAnimation);
                    }
                    else if (!unlockable && activeRects[i, j] is not null)
                    {
                        HighlightCanvas.Children.Remove(activeRects[i, j]);
                        activeRects[i, j] = null;
                    }
                }
            currentKeyMask = DungeonStore.KeyMask;
        }

        public void ClearRects()
        {
            for (int i = 0; i < 8; ++i)
                for (int j = 0; j < 8; ++j)
                    if (activeRects[i, j] is not null)
                    {
                        HighlightCanvas.Children.Remove(activeRects[i, j]);
                        activeRects[i, j] = null;
                    }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            hwnd = new WindowInteropHelper(this).Handle;

            nint exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            _ = User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, exStyle | User32.WS_EX_TRANSPARENT);

            trackerTimer.Start();
            Update();
        }

        protected override void OnClosed(EventArgs e)
        {
            trackerTimer.Stop();

            base.OnClosed(e);
        }

        /// <summary>
        /// Moves the overlay window if the game window moves.
        /// </summary>
        private void TrackingLoop()
        {
            if (MapCaptureService.Instance.TargetWindowHandle == IntPtr.Zero || !DungeonStore.IsActive)
            {
                if (Visibility != Visibility.Hidden)
                    Visibility = Visibility.Hidden;
                return;
            }

            IntPtr activeHwnd = User32.GetForegroundWindow();
            bool isGameActive = activeHwnd == MapCaptureService.Instance.TargetWindowHandle || activeHwnd == hwnd;

            if (!isGameActive)
            {
                if (Visibility != Visibility.Hidden)
                    Visibility = Visibility.Hidden;
                return;
            }

            if (Visibility != Visibility.Visible)
                Visibility = Visibility.Visible;

            var topLeft = new POINT { X = Settings.Instance.MapX, Y = Settings.Instance.MapY };
            _ = User32.ClientToScreen(MapCaptureService.Instance.TargetWindowHandle, ref topLeft);
            _ = User32.SetWindowPos(
                hwnd,
                IntPtr.Zero,
                topLeft.X,
                topLeft.Y,
                0,
                0,
                User32.SWP_NOSIZE | User32.SWP_NOACTIVATE | User32.SWP_NOZORDER);
        }

        private void UpdateText()
        {
            if (!DungeonStore.IsActive)
            {
                StatusBorder.Visibility = Visibility.Hidden;
                return;
            }
            StatusBorder.Visibility = Visibility.Visible;
            int c = DungeonStore.RoomCount;
            int d = DungeonStore.Map.DeadEndCount();
            var elapsed = DateTimeOffset.Now - DungeonStore.FloorStartTime;
            if (elapsed.TotalMilliseconds < 0)
                return;
            double rpm = CalculateRpm(c, elapsed.TotalMinutes);
            double targetRpm = DungeonStore.Map.PlayerCount > 0 && DungeonStore.Map.PlayerCount <= Settings.Instance.TargetRpms.Length
                ? Settings.Instance.TargetRpms[DungeonStore.Map.PlayerCount - 1]
                : 0;
            double ratio = rpm / targetRpm;
            int ticks = (int)Math.Ceiling((double)DungeonStore.RoomTimer.ElapsedMilliseconds / 600);
            RoomsRun.Text = $"{c,2}";
            DERun.Text = $"{d,2}";
            DELabel.Text = d == 1 ? "dead end" : "dead ends";
            TicksRun.Text = $"{ticks,2}t";
            TicksRun.Foreground = ticks < 10 ? Brushes.Lime : ticks < 20 ? Brushes.Yellow : Brushes.Red;
            RpmRun.Text = $"{rpm,2:F1}";
            RpmRun.Foreground = RpmRatioToBrush(ratio);
            HistoryRun.Text = string.Join(" • ", from x in DungeonStore.RoomTicks.TakeLast(4) select x.Item2.ToString().PadLeft(2));
        }

        static SolidColorBrush RpmRatioToBrush(double ratio)
        {
            if (ratio == 0)
                return Brushes.White;
            double t = Math.Clamp((ratio - 0.8) / (1.2 - 0.8), 0, 1);
            double hue = t * 120.0;
            return new SolidColorBrush(HslToRgb(hue, 0.6, 0.6));
        }

        static Color HslToRgb(double h, double s, double l)
        {
            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double x = c * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
            double m = l - c / 2.0;

            double rPrime = 0;
            double gPrime = 0;
            double bPrime = 0;

            if (h is >= 0 and < 60)
            {
                rPrime = c;
                gPrime = x;
            }
            else if (h is >= 60 and <= 120)
            {
                rPrime = x;
                gPrime = c;
            }

            byte r = (byte)Math.Round((rPrime + m) * 255.0);
            byte g = (byte)Math.Round((gPrime + m) * 255.0);
            byte b = (byte)Math.Round((bPrime + m) * 255.0);

            return Color.FromRgb(r, g, b);
        }

        static double CalculateRpm(int roomCount, double elapsedMinutes)
        {
            const double baseMinutes = 2.0 / 60;
            return elapsedMinutes < baseMinutes ? 0 : (roomCount - 1) / (elapsedMinutes - baseMinutes);
        }
    }
}
