namespace Dungeons2
{
    [Flags]
    public enum Directions : byte
    {
        None,
        W = 1,
        E = 2,
        S = 4,
        N = 8
    }

    public record struct Room(bool Opened = false, Directions Doors = Directions.None, Directions Parent = Directions.None, int Key = -1, int SkillDoor = -1, bool IsBase = false, bool IsBoss = false)
    {
        public Room() : this(Key: -1, SkillDoor: -1) { }

        public readonly bool IsDeadEnd => Doors == Parent;

        public void Merge(Room other)
        {
            if (Opened)
                return;
            Opened = other.Opened;
            Doors = other.Doors;
            Parent = other.Parent;
            if (Key == -1)
                Key = other.Key;
            if (SkillDoor == -1)
                SkillDoor = other.SkillDoor;
            IsBase |= other.IsBase;
            IsBoss |= other.IsBoss;
        }
    }
}
