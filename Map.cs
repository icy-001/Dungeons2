using Windows.Graphics;

namespace Dungeons2
{
    public class Map
    {
        public Room[,] Rooms { get; } = new Room[8, 8];
        public PointInt32?[] PlayerLocations { get; } = new PointInt32?[5];
        public int PlayerCount => PlayerLocations.Count(p => p.HasValue);

        public void Clear()
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    Rooms[r, c] = new();
            for (int i = 0; i < PlayerLocations.Length; i++)
                PlayerLocations[i] = null;
        }

        public void Merge(Map other)
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    Rooms[r, c].Merge(other.Rooms[r, c]);
            other.PlayerLocations.CopyTo(PlayerLocations, 0);
        }
    }
}
