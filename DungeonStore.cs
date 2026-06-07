using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dungeons2
{
    public class DungeonStore
    {
        public ulong KeyMask { get; set; }
        public Room[,] Rooms = new Room[8, 8];

        public bool InFloor { get; set; }
        public DateTimeOffset LastActiveTime { get; set; }
        public DateTimeOffset FloorStartTime { get; set; }
        public WriteableBitmap MapImage { get; set; } = new(MapKeybagReader.MapWidth, MapKeybagReader.MapHeight, 96, 96, PixelFormats.Bgra32, null);
        public int FloorCounter { get; set; }
        public int RoomCount { get; set; }
        public Stopwatch RoomTimer { get; } = new();

        public int DeadEndCount()
        {
            int count = 0;
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    if (Rooms[r, c].Direction is Direction.W or
                        Direction.E or
                        Direction.S or
                        Direction.N)
                        ++count;
            return count;
        }
    }
}
