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

    public struct Room() : IEquatable<Room>
    {
        public bool Opened { get; set; }
        public Directions OutgoingDoors { get; set; }
        public int Key { get; set; } = -1;
        public int SkillDoor { get; set; } = -1;
        public bool IsBase { get; set; }
        public bool IsBoss { get; set; }

        public readonly bool Equals(Room other) =>
            Opened == other.Opened &&
            OutgoingDoors == other.OutgoingDoors &&
            Key == other.Key &&
            SkillDoor == other.SkillDoor &&
            IsBase == other.IsBase &&
            IsBoss == other.IsBoss;

        public override readonly bool Equals(object? obj) => obj is Room other && Equals(other);
        public override readonly int GetHashCode() => HashCode.Combine(Opened, OutgoingDoors, Key, SkillDoor, IsBase, IsBoss);
        public static bool operator ==(Room left, Room right) => left.Equals(right);
        public static bool operator !=(Room left, Room right) => !(left == right);

        public void Merge(Room other)
        {
            if (Opened)
                return;
            Opened = other.Opened;
            OutgoingDoors = other.OutgoingDoors;
            if (Key == -1)
                Key = other.Key;
            if (SkillDoor == -1)
                SkillDoor = other.SkillDoor;
            IsBase |= other.IsBase;
            IsBoss |= other.IsBoss;
        }
    }
}
