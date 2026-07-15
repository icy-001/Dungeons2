using CommunityToolkit.Mvvm.ComponentModel;
using Dungeons2.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dungeons2
{
    /// <summary>
    /// Represents the state of the dungeon, including the map, key mask, and timing information.
    /// </summary>
    public partial class DungeonStore : ObservableObject
    {
        public static DungeonStore Instance { get; } = new();

        /// <summary>
        /// Gets or sets the key mask representing the keys collected in the dungeon.
        /// </summary>
        public ulong KeyMask { get; set; }
        /// <summary>
        /// Gets or sets the map representing the layout of the dungeon.
        /// </summary>
        public Map Map { get; set; } = new(8, 8);

        public bool IsActive { get; set; }
        [ObservableProperty]
        public partial bool IsPartial { get; set; }
        [ObservableProperty]
        public partial bool IsMapFound { get; set; }
        [ObservableProperty]
        public partial bool IsKeybagFound { get; set; }
        public DateTimeOffset LastActiveTime { get; set; }
        public DateTimeOffset FloorStartTime { get; set; }
        public WriteableBitmap MapImage { get; set; } = new(MapHudReader.MapWidth, MapHudReader.MapHeight, 96, 96, PixelFormats.Bgra32, null);
        public RenderTargetBitmap AnnotatedMapImage { get; set; } = new(MapHudReader.MapWidth, MapHudReader.MapHeight, 96, 96, PixelFormats.Pbgra32);
        public int FloorCounter { get; set; }
        public int RoomCount { get; set; }
        public ObservableCollection<RoomSplit> RoomTicks { get; } = [];
        public Stopwatch RoomStopwatch { get; } = new();
        public ObservableCollection<FloorData> FloorHistory { get; } = [];

        private DungeonStore()
        {
            MapCaptureService.Instance.OnFloorCompleted += (floorData) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    FloorHistory.Add(floorData);
                });
            };

            if (System.IO.File.Exists("floors.jsonl"))
                foreach (string line in System.IO.File.ReadLines("floors.jsonl"))
                {
                    var floorData = System.Text.Json.JsonSerializer.Deserialize<FloorData>(line);
                    if (floorData is not null)
                        FloorHistory.Add(floorData);
                }
        }

        public void UpdateMapImage(RawImageView image)
        {
            IntPtr pBackBuffer = IntPtr.Zero;
            int destWidth = 0;
            int destHeight = 0;
            int destStride = 0;

            Application.Current.Dispatcher.Invoke(() =>
            {
                MapImage.Lock();
                pBackBuffer = MapImage.BackBuffer;
                destWidth = MapImage.PixelWidth;
                destHeight = MapImage.PixelHeight;
                destStride = MapImage.BackBufferStride;
            });

            int sourceStride = image.Width * sizeof(uint);

            int copyWidth = Math.Min(image.Width, destWidth);
            int copyHeight = Math.Min(image.Height, destHeight);
            int bytesToCopyPerRow = copyWidth * sizeof(uint);

            unsafe
            {
                fixed (uint* pSourceBase = image.Pixels)
                {
                    byte* pSourceBytes = (byte*)pSourceBase;
                    byte* pDestBytes = (byte*)pBackBuffer.ToPointer();

                    for (int y = 0; y < copyHeight; y++)
                    {
                        byte* pSourceRow = pSourceBytes + y * sourceStride;
                        byte* pDestRow = pDestBytes + y * destStride;

                        Buffer.MemoryCopy(pSourceRow, pDestRow, destStride, bytesToCopyPerRow);
                    }
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                MapImage.AddDirtyRect(new Int32Rect(0, 0, copyWidth, copyHeight));
                MapImage.Unlock();
            });
        }

        public void UpdateAnnotatedMapImage()
        {
            const int offX = 12, offY = 12;
            DrawingVisual visual = new();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Black, null, new(0, 0, MapHudReader.MapWidth, MapHudReader.MapHeight));
                AnnotatedMapRenderer.DrawMap(dc, offX, offY, Map);
                AnnotatedMapRenderer.DrawRoomTicks(dc, offX, offY, RoomTicks);
            }
            AnnotatedMapImage.Render(visual);
        }
    }
}
