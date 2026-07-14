using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Dungeons2
{
    public class Map
    {
        [JsonConstructor]
        public Map(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            Rooms = new Room[rows * cols];
            for (int i = 0; i < Rooms.Length; ++i)
                Rooms[i] = new();
        }

        public Map(Map original) : this(original.Rows, original.Cols)
        {
            Array.Copy(original.Rooms, Rooms, Rooms.Length);
            Base = original.Base;
            Boss = original.Boss;
            original.PlayerLocations.CopyTo(PlayerLocations, 0);
        }

        public int Rows { get; }
        public int Cols { get; }
        public Room[] Rooms { get; }
        public Coord Base { get; set; } = Coord.Nothing;
        public Coord Boss { get; set; } = Coord.Nothing;
        public Coord?[] PlayerLocations { get; } = new Coord?[5];
        public int PlayerCount => PlayerLocations.Count(p => p.HasValue);

        static readonly Directions[] SingleDirections = [Directions.W, Directions.E, Directions.S, Directions.N];
        static Registry Registry => Registry.Instance;

        public int Index(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                throw new ArgumentOutOfRangeException(nameof(row), "Row or column is out of range.");
            return row * Cols + col;
        }

        public ref Room this[int row, int col] => ref Rooms[Index(row, col)];

        /// <summary>
        /// Merges this map with another and returns true if there was a change.
        /// </summary>
        /// <param name="other">The other map.</param>
        /// <returns>True if there was a change; otherwise, false.</returns>
        public (bool Changed, Coord[] NewRoomLocations) Merge(Map other)
        {
            bool changed = false;
            List<Coord> newRoomLocations = [];
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    ref var oldRoom = ref this[r, c];
                    ref var newRoom = ref other[r, c];
                    if (newRoom.Opened && !oldRoom.Opened)
                        newRoomLocations.Add(new Coord(r, c));
                    changed |= oldRoom.Merge(newRoom);
                }
            if (other.Base != Coord.Nothing && Base != other.Base)
            {
                Base = other.Base;
                changed = true;
            }
            if (other.Boss != Coord.Nothing && Boss != other.Boss)
            {
                Boss = other.Boss;
                changed = true;
            }
            changed |= !Enumerable.SequenceEqual(PlayerLocations, other.PlayerLocations);
            other.PlayerLocations.CopyTo(PlayerLocations, 0);
            return (changed, newRoomLocations.ToArray());
        }

        public void AssignParents()
        {
            if (Base == Coord.Nothing)
                return;

            bool[,] visited = new bool[Rows, Cols];

            void Visit(Coord c, Directions dir)
            {
                var (i, j) = c;
                if (visited[i, j])
                    return;
                visited[i, j] = true;
                this[i, j].Parent = Flip(dir);
                foreach (var d in SingleDirections)
                    if (this[i, j].Doors.HasFlag(d))
                        Visit(Add(c, d), d);
            }

            Visit(Base, Directions.None);
        }

        public IEnumerable<Room> Children(int i, int j)
        {
            foreach (var d in SingleDirections)
                if (this[i, j].Doors.HasFlag(d) && this[i, j].Parent != d)
                {
                    var (ii, jj) = Add(new(i, j), d);
                    yield return this[ii, jj];
                }
        }

        public int DeadEndCount()
        {
            int count = 0;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    if (this[r, c].IsDeadEnd)
                        ++count;
            return count;
        }

        /// <summary>
        /// Only works for solos.
        /// </summary>
        /// <returns>Returns pair (dewks, denks).</returns>
        public (int Dewks, int Denks) DeadEndCountExtended()
        {
            Debug.Assert(PlayerCount == 1, "DeadEndCountExtended only works for solos.");
            int dewks = 0, denks = 0;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    var room = this[r, c];
                    if (room.IsDeadEnd)
                    {
                        if (room.Key == -1)
                            ++denks;
                        else
                            ++dewks;
                    }
                }
            return (dewks, denks);
        }

        public int PossiblePuzzleCount()
        {
            int res = 0;
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Cols; j++)
                    if (this[i, j].Opened && !this[i, j].IsDeadEnd && Children(i, j).All(r => r.Lock == -1))
                        ++res;
            return res;
        }

        public int KeyCount()
        {
            int res = 0;
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Cols; j++)
                    if (this[i, j].Lock != -1)
                        ++res;
            return res;
        }

        public (int Count, int Ticks) SkillDoorStats()
        {
            int count = 0, ticks = 0;
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Cols; j++)
                    if (this[i, j].Opened && this[i, j].SkillDoor != -1)
                    {
                        var entry = Registry.SkillDoors[this[i, j].SkillDoor];
                        ++count;
                        ticks += entry.Ticks;
                    }
            return (count, ticks);
        }

        static Coord Add(Coord p, Directions d) => d switch
        {
            Directions.W => new(p.Row, p.Col - 1),
            Directions.E => new(p.Row, p.Col + 1),
            Directions.S => new(p.Row + 1, p.Col),
            Directions.N => new(p.Row - 1, p.Col),
            _ => throw new InvalidOperationException("Direction must be single.")
        };

        static Directions Flip(Directions d) => d switch
        {
            Directions.W => Directions.E,
            Directions.E => Directions.W,
            Directions.S => Directions.N,
            Directions.N => Directions.S,
            _ => Directions.None
        };

        public Map Clone() => new(this);
    }
}
