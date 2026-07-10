namespace Dungeons2
{
    public class Map
    {
        public Map() { Clear(); }

        public Room[,] Rooms { get; } = new Room[8, 8];
        public Coord Base { get; set; } = Coord.Nothing;
        public Coord Boss { get; set; } = Coord.Nothing;
        public Coord?[] PlayerLocations { get; } = new Coord?[5];
        public int PlayerCount => PlayerLocations.Count(p => p.HasValue);

        static readonly Directions[] SingleDirections = [Directions.W, Directions.E, Directions.S, Directions.N];
        static Registry Registry => Registry.Instance;

        public void Clear()
        {
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                    Rooms[i, j] = new();
            for (int p = 0; p < PlayerLocations.Length; p++)
                PlayerLocations[p] = null;
        }

        /// <summary>
        /// Merges this map with another and returns true if there was a change.
        /// </summary>
        /// <param name="other">The other map.</param>
        /// <returns>True if there was a change; otherwise, false.</returns>
        public bool Merge(Map other)
        {
            bool changed = false;
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    changed |= Rooms[r, c].Merge(other.Rooms[r, c]);
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
            return changed;
        }

        public void AssignParents()
        {
            if (Base == Coord.Nothing)
                return;

            bool[,] visited = new bool[8, 8];

            void Visit(Coord c, Directions dir)
            {
                var (i, j) = c;
                if (visited[i, j])
                    return;
                visited[i, j] = true;
                Rooms[i, j].Parent = Flip(dir);
                foreach (var d in SingleDirections)
                    if (Rooms[i, j].Doors.HasFlag(d))
                        Visit(Add(c, d), d);
            }

            Visit(Base, Directions.None);
        }

        public IEnumerable<Room> Children(int i, int j)
        {
            foreach (var d in SingleDirections)
                if (Rooms[i, j].Doors.HasFlag(d) && Rooms[i, j].Parent != d)
                {
                    var (ii, jj) = Add(new(i, j), d);
                    yield return Rooms[ii, jj];
                }
        }

        public int DeadEndCount()
        {
            int count = 0;
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    if (Rooms[r, c].IsDeadEnd)
                        ++count;
            return count;
        }

        /// <summary>
        /// Only works for solos.
        /// </summary>
        /// <returns>Returns pair (dewks, denks).</returns>
        public (int Dewks, int Denks) DeadEndCountExtended()
        {
            int dewks = 0, denks = 0;
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    var room = Rooms[r, c];
                    if (room.IsDeadEnd)
                    {
                        if (room.Key == -1)
                            ++denks;
                        else ++dewks;
                    }
                }
            return (dewks, denks);
        }

        public int PossiblePuzzleCount()
        {
            int res = 0;
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                    if (Rooms[i, j].Opened && !Rooms[i, j].IsDeadEnd && Children(i, j).All(r => r.Lock == -1))
                        ++res;
            return res;
        }

        public (int Count, int Ticks) SkillDoorStats()
        {
            int count = 0, ticks = 0;
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                    if (Rooms[i, j].Opened && Rooms[i, j].SkillDoor != -1)
                    {
                        var entry = Registry.SkillDoors[Rooms[i, j].SkillDoor];
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
    }
}
