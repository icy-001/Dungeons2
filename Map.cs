using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Dungeons2
{
    public record struct ExtendedDeadEnds(int Dewks, int Denks);

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
        public Room[] Rooms { get; init; }
        public Coord Base { get; set; } = Coord.Nothing;
        public Coord Boss { get; set; } = Coord.Nothing;
        public Coord?[] PlayerLocations { get; init; } = new Coord?[5];
        [JsonIgnore]
        public int PlayerCount => PlayerLocations.Count(p => p.HasValue);

        static readonly Directions[] SingleDirections = [Directions.W, Directions.E, Directions.S, Directions.N];
        static Registry Registry => Registry.Instance;

        public int Index(int row, int col)
        {
            return row < 0 || row >= Rows || col < 0 || col >= Cols
                ? throw new ArgumentOutOfRangeException(nameof(row), "Row or column is out of range.")
                : row * Cols + col;
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
        public ExtendedDeadEnds DeadEndCountExtended()
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
            return new(dewks, denks);
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

        public ulong KeyMask()
        {
            ulong res = 0;
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Cols; j++)
                    if (this[i, j].Key != -1)
                        res |= 1UL << this[i, j].Key;
            return res;
        }

        public ulong LockMask()
        {
            ulong res = 0;
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Cols; j++)
                    if (this[i, j].Lock != -1)
                        res |= 1UL << this[i, j].Lock;
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

        public byte[,] ComputeUnopenedGrid()
        {
            byte[,] mystery = new byte[8, 8];
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var room = this[r, c];
                    if (!room.Opened) continue;
                    if (room.Doors.HasFlag(Directions.W) && c > 0 && !this[r, c - 1].Opened) mystery[r, c - 1] = 2;
                    if (room.Doors.HasFlag(Directions.E) && c < 7 && !this[r, c + 1].Opened) mystery[r, c + 1] = 1;
                    if (room.Doors.HasFlag(Directions.S) && r < 7 && !this[r + 1, c].Opened) mystery[r + 1, c] = 4;
                    if (room.Doors.HasFlag(Directions.N) && r > 0 && !this[r - 1, c].Opened) mystery[r - 1, c] = 3;
                }
            }
            return mystery;
        }

        public bool[,] ComputeTrivialKeyblockGrid()
        {
            var res = new bool[Rows, Cols];
            if (Base == Coord.Nothing || PlayerCount != 1)
                return res;

            var keyLocations = new Coord[64];
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Cols; j++)
                {
                    var room = this[i, j];
                    if (room.Key != -1)
                        keyLocations[room.Key] = new(i, j);
                }

            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Cols; j++)
                {
                    var room = this[i, j];
                    if (room.Lock == -1)
                        continue;

                    var keyLocation = keyLocations[room.Lock];
                    if (keyLocation == Coord.Nothing)
                        continue;

                    var current = new Coord(i, j);
                    while (current != Coord.Nothing)
                    {
                        if (current == keyLocation)
                        {
                            res[i, j] = true;
                            break;
                        }

                        if (current == Base)
                            break;

                        var currentRoom = this[current.Row, current.Col];
                        if (currentRoom.Parent == Directions.None)
                            break;

                        current = Add(current, currentRoom.Parent);
                    }
                }

            return res;
        }

        public bool[,] ComputeCriticalGrid()
        {
            var res = new bool[8, 8];
            if (Boss == Coord.Nothing || Base == Coord.Nothing || PlayerCount != 1 || KeyMask() == 0)
                return res;
            res[Base.Row, Base.Col] = true;
            var keyLocs = new Coord[64];
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                {
                    var room = this[i, j];
                    if (room.Key != -1)
                        keyLocs[room.Key] = new(i, j);
                }
            Stack<int> criticalKeys = [];
            void MakeCritical(Coord pos)
            {
                for (; !res[pos.Row, pos.Col] && this[pos.Row, pos.Col].Parent != Directions.None; pos = Add(pos, this[pos.Row, pos.Col].Parent))
                {
                    res[pos.Row, pos.Col] = true;
                    if (this[pos.Row, pos.Col].Lock != -1)
                        criticalKeys.Push(this[pos.Row, pos.Col].Lock);
                }
            }
            MakeCritical(Boss);
            while (criticalKeys.TryPop(out int key))
                MakeCritical(keyLocs[key]);
            return res;
        }

        public (int Count, int KeyCount) ComputeCriticalStats()
        {
            int count = 0, keyCount = 0;
            var criticalGrid = ComputeCriticalGrid();
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Cols; j++)
                    if (criticalGrid[i, j])
                    {
                        ++count;
                        if (this[i, j].Lock != -1)
                            ++keyCount;
                    }
            return (count, keyCount);
        }

        /// <summary>
        /// Computes the longest 1-way path length of the floor. Starts at 1 for base room.
        /// </summary>
        /// <returns>The depth of the map, which is the longest 1-way path length of the floor.</returns>
        public int ComputeDepth()
        {
            if (Boss == Coord.Nothing || Base == Coord.Nothing || PlayerCount != 1 || KeyMask() != LockMask())
                return 0;

            int res = 0;
            ulong keyMask = 0;

            Span<Coord> frontier = stackalloc Coord[64];
            Span<Coord> nextFrontier = stackalloc Coord[64];

            frontier[0] = Base;
            int frontierCount = 1;

            while (frontierCount > 0 && res < 64)
            {
                ++res;
                int nextCount = 0;
                ulong nextKeymask = keyMask;
                bool progressed = false;

                for (int i = 0; i < frontierCount; i++)
                {
                    var (r, c) = frontier[i];
                    var room = this[r, c];
                    if (room.Lock != -1 && (keyMask & (1UL << room.Lock)) == 0)
                    {
                        nextFrontier[nextCount++] = frontier[i];
                        continue;
                    }
                    progressed = true;
                    if (room.Key != -1)
                        nextKeymask |= 1UL << room.Key;
                    foreach (var d in SingleDirections)
                        if (room.Doors.HasFlag(d) && room.Parent != d)
                            nextFrontier[nextCount++] = Add(frontier[i], d);
                }

                if (!progressed)
                    break;

                var temp = frontier;
                frontier = nextFrontier;
                nextFrontier = temp;

                frontierCount = nextCount;
                keyMask = nextKeymask;
            }

            return res;
        }

        public static Coord Add(Coord p, Directions d) => d switch
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
