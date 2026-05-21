using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dungeons2
{
    public class DungeonStore
    {
        public ulong KeyMask { get; set { if (field != value) { field = value; KeyMaskChanged?.Invoke(this, EventArgs.Empty); } } }
        public Room[,] Rooms
        {
            get; set
            {
                if (!AreGridsEqual(field, value)) { field = value; RoomsChanged?.Invoke(this, EventArgs.Empty); }
            }
        } = new Room[8, 8];

        public event EventHandler? KeyMaskChanged;
        public event EventHandler? RoomsChanged;
        public bool InFloor;
        public DateTimeOffset LastActiveTime;
        public DateTimeOffset FloorStartTime;
        public WriteableBitmap MapImage = new(MapKeybagReader.MapWidth, MapKeybagReader.MapHeight, 96, 96, PixelFormats.Bgra32, null);
        public int FloorCounter;

        public int RoomCount()
        {
            int count = 0;
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    if (Rooms[r, c].Direction != Direction.None)
                        ++count;
            return count;
        }

        public int DeadEnds()
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

        static bool AreGridsEqual(Room[,] a, Room[,] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;

            int rows = a.GetLength(0);
            int cols = a.GetLength(1);

            if (rows != b.GetLength(0) || cols != b.GetLength(1)) return false;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (a[r, c] != b[r, c]) return false;
                }
            }

            return true;
        }
    }
}
