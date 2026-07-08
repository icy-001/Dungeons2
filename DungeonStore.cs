using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dungeons2
{
    /// <summary>
    /// Represents the state of the dungeon, including the map, key mask, and timing information.
    /// </summary>
    public class DungeonStore
    {
        public static DungeonStore Instance { get; } = new();

        /// <summary>
        /// Gets or sets the key mask representing the keys collected in the dungeon.
        /// </summary>
        public ulong KeyMask { get; set; }
        /// <summary>
        /// Gets or sets the map representing the layout of the dungeon.
        /// </summary>
        public Map Map { get; init; } = new();

        /// <summary>
        /// Gets or sets whether the dungeon is currently active.
        /// </summary>
        public bool Active { get; set; }
        public DateTimeOffset LastActiveTime { get; set; }
        public DateTimeOffset FloorStartTime { get; set; }
        public WriteableBitmap MapImage { get; set; } = new(MapKeybagReader.MapWidth, MapKeybagReader.MapHeight, 96, 96, PixelFormats.Bgra32, null);
        public int FloorCounter { get; set; }
        public int RoomCount { get; set; }
        public List<(int, int)> RoomTicks { get; } = [];
        public Stopwatch RoomTimer { get; } = new();

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
    }
}
