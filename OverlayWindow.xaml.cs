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
        private ulong currentKeyMask;
        private readonly Rectangle?[] activeRects = new Rectangle?[64];

        public OverlayWindow(DungeonStore store)
        {
            InitializeComponent();
            dungeonStore = store;

            trackerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            hwnd = new WindowInteropHelper(this).Handle;

            nint exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            _ = User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, exStyle | User32.WS_EX_TRANSPARENT);

            trackerTimer.Start();

            dungeonStore.KeyMaskChanged += OnStateChanged;
            dungeonStore.RoomsChanged += OnStateChanged;

            OnStateChanged(this, EventArgs.Empty);
        }

        protected override void OnClosed(EventArgs e)
        {
            trackerTimer.Stop();

            dungeonStore.KeyMaskChanged -= OnStateChanged;
            dungeonStore.RoomsChanged -= OnStateChanged;

            base.OnClosed(e);
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                ulong requiredKeys = 0;

                for (int i = 0; i < 8; ++i)
                {
                    for (int j = 0; j < 8; ++j)
                    {
                        int key = dungeonStore.Rooms[i, j].Key;
                        if (key != -1 && (dungeonStore.KeyMask & (1UL << key)) != 0)
                        {
                            requiredKeys |= 1UL << key;

                            if (activeRects[key] is null)
                            {
                                var rect = new Rectangle
                                {
                                    Width = 27,
                                    Height = 27,
                                    Stroke = Brushes.LightGreen,
                                    StrokeThickness = 1,
                                    Tag = key
                                };

                                Canvas.SetLeft(rect, 12 + 32 * j);
                                Canvas.SetTop(rect, 15 + 32 * i);
                                _ = HighlightCanvas.Children.Add(rect);
                                activeRects[key] = rect;

                                if ((currentKeyMask & (1UL << key)) == 0)
                                {
                                    var thicknessAnim = new DoubleAnimation
                                    {
                                        From = 1,
                                        To = 4,
                                        Duration = TimeSpan.FromMilliseconds(300),
                                        AutoReverse = true,
                                        RepeatBehavior = new RepeatBehavior(6),
                                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                                    };

                                    rect.BeginAnimation(Shape.StrokeThicknessProperty, thicknessAnim);
                                }
                            }
                        }
                    }
                }

                var keysToRemove = new List<int>();
                for (int k = 0; k < 64; ++k)
                    if (activeRects[k] is not null && (requiredKeys & (1UL << k)) == 0)
                        keysToRemove.Add(k);

                foreach (int k in keysToRemove)
                {
                    HighlightCanvas.Children.Remove(activeRects[k]);
                    activeRects[k] = null;
                }

                currentKeyMask = dungeonStore.KeyMask;
            });
        }

        /// <summary>
        /// Moves the overlay window if the game window moves.
        /// </summary>
        private void TrackingLoop()
        {
            if (RsHwnd == IntPtr.Zero)
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
            int c = dungeonStore.RoomCount();
            int d = dungeonStore.DeadEndCount();
            var elapsed = DateTimeOffset.Now - dungeonStore.FloorStartTime;
            if (elapsed.TotalMilliseconds < 0)
                return;
            double rpm = CalculateRpm(c, elapsed.TotalMinutes);
            double ratio = rpm / Settings.Default.TargetRpm;
            StatusText.Text = $"Rooms: {c,-2}  •  Dead ends: {d,-2}  •  RPM: {rpm:F1}";
            StatusBorder.Background = RpmRatioToBrush(ratio);
        }

        static SolidColorBrush RpmRatioToBrush(double ratio)
        {
            if (ratio == 0)
                return Brushes.Black;
            if (ratio < 0.8)
                return Brushes.Maroon;
            if (ratio > 1.2)
                return Brushes.DarkGreen;
            double factor = (ratio - 0.8) / (1.2 - 0.8);

            byte rStart = 135;
            byte gStart = 0;
            byte bStart = 0;

            byte rEnd = 0;
            byte gEnd = 135;
            byte bEnd = 0;

            byte r = (byte)(rStart + (rEnd - rStart) * factor);
            byte g = (byte)(gStart + (gEnd - gStart) * factor);
            byte b = (byte)(bStart + (bEnd - bStart) * factor);

            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        static double CalculateRpm(int roomCount, double elapsedMinutes)
        {
            double baseMinutes = 2.0 / 60;
            if (elapsedMinutes < baseMinutes)
                return 0;
            return (roomCount - 1) / (elapsedMinutes - baseMinutes);
        }
    }
}
