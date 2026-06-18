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
        private IntPtr hwnd;
        private readonly DispatcherTimer trackerTimer;
        private readonly DungeonStore dungeonStore;
        private readonly Rectangle?[] activeRects = new Rectangle?[64];
        private ulong currentKeyMask;
        private readonly DoubleAnimation opacityAnimation = new()
        {
            From = 0.05,
            To = 0.5,
            Duration = TimeSpan.FromMilliseconds(300),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(6),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        public OverlayWindow(DungeonStore store)
        {
            InitializeComponent();
            dungeonStore = store;

            trackerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            trackerTimer.Tick += (sender, args) =>
            {
                TrackingLoop();
                UpdateRpm();
            };
        }

        public IntPtr RsHwnd
        {
            get; set
            {
                if (field != value)
                {
                    field = value;
                    TrackingLoop();
                }
            }
        }

        /// <summary>
        /// Updates the overlay window. Must be called from UI thread.
        /// </summary>
        public void Update()
        {
            for (int i = 0; i < 8; ++i)
                for (int j = 0; j < 8; ++j)
                {
                    int key = dungeonStore.Map.Rooms[i, j].Key;
                    if (key == -1)
                        continue;
                    bool unlockable = !dungeonStore.Map.Rooms[i, j].Opened && (dungeonStore.KeyMask & (1UL << key)) != 0;
                    if (unlockable && activeRects[key] is null)
                    {
                        var fill = new SolidColorBrush(Colors.White)
                        {
                            Opacity = 0.05
                        };
                        var rect = new Rectangle
                        {
                            Width = 26,
                            Height = 26,
                            Fill = fill,
                            Stroke = Brushes.White
                        };

                        Canvas.SetLeft(rect, 14 + 32 * j);
                        Canvas.SetTop(rect, 15 + 32 * i);
                        _ = HighlightCanvas.Children.Add(rect);
                        activeRects[key] = rect;

                        if ((currentKeyMask & (1UL << key)) == 0)
                            // Only flash if update is due to key found.
                            fill.BeginAnimation(Brush.OpacityProperty, opacityAnimation);
                    }
                    if (!unlockable && activeRects[key] is not null)
                    {
                        HighlightCanvas.Children.Remove(activeRects[key]);
                        activeRects[key] = null;
                    }
                }
            currentKeyMask = dungeonStore.KeyMask;
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
            if (RsHwnd == IntPtr.Zero || !dungeonStore.InFloor)
            {
                if (Visibility != Visibility.Hidden)
                    Visibility = Visibility.Hidden;
                return;
            }

            IntPtr activeHwnd = User32.GetForegroundWindow();
            bool isGameActive = activeHwnd == RsHwnd || activeHwnd == hwnd;

            if (!isGameActive)
            {
                if (Visibility != Visibility.Hidden)
                    Visibility = Visibility.Hidden;
                return;
            }

            if (Visibility != Visibility.Visible)
                Visibility = Visibility.Visible;

            var topLeft = new POINT { X = Settings.Default.MapX, Y = Settings.Default.MapY };
            _ = User32.ClientToScreen(RsHwnd, ref topLeft);
            _ = User32.SetWindowPos(
                hwnd,
                IntPtr.Zero,
                topLeft.X,
                topLeft.Y,
                0,
                0,
                User32.SWP_NOSIZE | User32.SWP_NOACTIVATE | User32.SWP_NOZORDER);
        }

        private void UpdateRpm()
        {
            if (!dungeonStore.InFloor)
            {
                StatusBorder.Visibility = Visibility.Hidden;
                return;
            }
            StatusBorder.Visibility = Visibility.Visible;
            int c = dungeonStore.RoomCount;
            int d = dungeonStore.DeadEndCount();
            var elapsed = DateTimeOffset.Now - dungeonStore.FloorStartTime;
            if (elapsed.TotalMilliseconds < 0)
                return;
            double rpm = CalculateRpm(c, elapsed.TotalMinutes);
            double targetRpm = dungeonStore.Map.PlayerCount <= Settings.Default.TargetRpms.Length
                ? Settings.Default.TargetRpms[dungeonStore.Map.PlayerCount - 1]
                : 0;
            double ratio = rpm / targetRpm;
            int ticks = (int)Math.Ceiling((double)dungeonStore.RoomTimer.ElapsedMilliseconds / 600);
            StatusText.Text = $"Rooms: {c,-2}  •  Dead ends: {d,-2}  •  RPM: {rpm:F1}\nTicks since last room: {ticks}";
            StatusBorder.Background = RpmRatioToBrush(ratio);
        }

        static SolidColorBrush RpmRatioToBrush(double ratio)
        {
            if (ratio == 0)
                return Brushes.Black;
            double t = Math.Clamp((ratio - 0.8) / (1.2 - 0.8), 0, 1);
            double hue = t * 120.0;
            return new SolidColorBrush(HslToRgb(hue, 0.6, 0.25));
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
            double baseMinutes = 2.0 / 60;
            return elapsedMinutes < baseMinutes ? 0 : (roomCount - 1) / (elapsedMinutes - baseMinutes);
        }
    }
}
